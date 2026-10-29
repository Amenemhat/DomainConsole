"""Fail closed on missing release acceptance; never changes GitHub issue states."""
import hashlib
import json
from pathlib import Path


def require(value, message):
    if not value:
        raise ValueError(message)


def text(value):
    return isinstance(value, str) and bool(value.strip())


def batch_scope(registry, plan, version):
    """Check the entire allocation before selecting a release; never omit safety."""
    require(plan.get('schema') == 1, 'Batch schema must be 1')
    specs = {row['issue']: row for row in registry['issues']}
    require(len(specs) == len(registry['issues']), 'Duplicate registered issue')
    batches = plan.get('batches', [])
    versions = [b.get('version') for b in batches]
    require(all(text(v) for v in versions) and len(set(versions)) == len(versions), 'Invalid batch versions')
    assigned = []
    for batch in batches:
        issues = batch.get('issues', [])
        require(isinstance(issues, list) and bool(issues) and all(type(n) is int for n in issues), 'Invalid batch issues')
        require(len(set(issues)) == len(issues) and set(issues) <= set(specs), 'Duplicate or unknown batch issue')
        ids = batch.get('condition_ids', [])
        expected = [c['id'] for n in issues for c in specs[n].get('conditions', [])]
        require(bool(expected) and len(set(expected)) == len(expected), 'Invalid registered batch conditions')
        require(isinstance(ids, list) and len(set(ids)) == len(ids) and set(ids) == set(expected), 'Batch conditions differ from registry')
        assigned.extend(issues)
    require(len(set(assigned)) == len(assigned) and set(assigned) == set(specs), 'Missing or multiply assigned issues')
    require(set(plan.get('mandatory_scenarios_each_release', [])) == set(registry['mandatory_scenarios']), 'Batch plan changes mandatory regressions')
    require(version in versions, 'Release has no approved batch')
    return set(next(b for b in batches if b['version'] == version)['issues'])


def validate_acceptance(report, registry, request, plan=None):
    require(report.get('schema') == 1, 'Acceptance schema must be 1')
    for key in ('version', 'commit', 'run_id', 'sha256'):
        require(report.get(key) == request[key], f'Acceptance {key} differs from release')
    require(text(report.get('reviewer')) and text(report.get('reviewed_at')), 'Reviewer and review time required')
    expected = {x['issue'] for x in registry['issues']}
    require(len(expected) == len(registry['issues']), 'Duplicate issue in requirement registry')
    specifications = {x['issue']: x for x in registry['issues']}
    if plan is not None:
        expected = batch_scope(registry, plan, request['version'])
    rows = report.get('issues', [])
    require(isinstance(rows, list), 'Issue rows required')
    numbers = [x.get('issue') for x in rows]
    require(all(type(n) is int for n in numbers), 'Integer issue numbers required')
    require(len(set(numbers)) == len(numbers), 'Duplicate issue rows')
    require(expected == set(numbers), 'Acceptance and registered issue sets differ')
    by_number = {x['issue']: x for x in rows}
    closable = set()
    for row in rows:
        require(text(row.get('current_requirement')), 'Current interpretation required')
        status = row.get('status')
        if plan is not None:
            require(status == 'passed', 'Every selected batch issue must pass')
        require(status in ('passed', 'deferred', 'superseded'), 'Unverified/failed/pending requirement blocks release')
        if status == 'passed':
            conditions = specifications[row['issue']].get('conditions', [])
            require(isinstance(conditions, list) and bool(conditions), 'Active conditions must be classified in registry before acceptance')
            require(all(isinstance(c, dict) and text(c.get('id')) and text(c.get('requirement')) for c in conditions), 'Invalid registered condition')
            condition_ids = [c['id'] for c in conditions]
            require(len(set(condition_ids)) == len(condition_ids), 'Duplicate condition in registry')
            checks = row.get('checks', [])
            require(isinstance(checks, list) and bool(checks), 'Per-requirement checks required')
            for check in checks:
                require(all(text(check.get(k)) for k in ('requirement', 'scenario', 'expected', 'actual', 'evidence')), 'Incomplete check evidence')
                require(check.get('result') == 'passed', 'Check did not pass')
                require(check.get('commit') == request['commit'], 'Check belongs to another source commit')
            checked_ids = [c.get('condition_id') for c in checks]
            require(all(text(i) for i in checked_ids), 'Every check must identify its registered condition')
            require(set(checked_ids) == set(condition_ids), 'Active conditions omitted or unknown conditions claimed')
            closable.add(row['issue'])
        elif status == 'deferred':
            require(text(row.get('reason')) and text(row.get('tracking_issue')), 'Deferred work needs reason and open tracking issue')
            require(row.get('keep_open') is True, 'Deferred issue must remain open')
        else:
            successor = by_number.get(row.get('replacement_issue'), {})
            require(successor.get('status') == 'passed', 'Replacement must pass acceptance')
            require(text(row.get('decision_evidence')), 'Supersession must cite an agreed decision')
    scenarios = report.get('scenarios', [])
    ids = [x.get('id') for x in scenarios]
    require(len(set(ids)) == len(ids), 'Duplicate mandatory scenarios')
    require(set(registry['mandatory_scenarios']) <= set(ids), 'Mandatory regression scenarios omitted')
    for check in scenarios:
        require(check.get('result') == 'passed', 'Mandatory regression scenario not passed')
        require(check.get('commit') == request['commit'], 'Regression evidence belongs to another source')
        require(all(text(check.get(k)) for k in ('expected', 'actual', 'evidence')), 'Regression evidence missing')
    closure = report.get('close_issues', [])
    require(isinstance(closure, list) and all(type(n) is int for n in closure), 'Invalid closure list')
    require(len(set(closure)) == len(closure) and set(closure) <= closable, 'Cannot close deferred, superseded or unverified issues as completed')
    return sorted(closable)


def load_acceptance(request):
    registry_bytes = Path('docs/acceptance/requirements.json').read_bytes()
    registry = json.loads(registry_bytes)
    path = Path('docs/acceptance') / (request['version'] + '.json')
    report = json.loads(path.read_text(encoding='utf-8'))
    require(report.get('registry_sha256') == hashlib.sha256(registry_bytes).hexdigest(), 'Acceptance uses a different requirement inventory')
    plan_bytes = Path('docs/acceptance/release-batches.json').read_bytes()
    require(report.get('batch_plan_sha256') == hashlib.sha256(plan_bytes).hexdigest(), 'Acceptance uses a different batch plan')
    return validate_acceptance(report, registry, request, json.loads(plan_bytes))
