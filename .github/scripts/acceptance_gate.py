"""Fail closed on missing release acceptance; never changes GitHub issue states."""
import hashlib
import json
from pathlib import Path


def require(value, message):
    if not value:
        raise ValueError(message)


def text(value):
    return isinstance(value, str) and bool(value.strip())


def validate_acceptance(report, registry, request):
    require(report.get('schema') == 1, 'Acceptance schema must be 1')
    for key in ('version', 'commit', 'run_id', 'sha256'):
        require(report.get(key) == request[key], f'Acceptance {key} differs from release')
    require(text(report.get('reviewer')) and text(report.get('reviewed_at')), 'Reviewer and review time required')
    expected = {x['issue'] for x in registry['issues']}
    rows = report.get('issues', [])
    require(isinstance(rows, list), 'Issue rows required')
    numbers = [x.get('issue') for x in rows]
    require(all(type(n) is int for n in numbers), 'Integer issue numbers required')
    require(len(set(numbers)) == len(numbers), 'Duplicate issue rows')
    require(expected <= set(numbers), 'Historical requirements omitted from acceptance')
    by_number = {x['issue']: x for x in rows}
    closable = set()
    for row in rows:
        require(text(row.get('current_requirement')), 'Current interpretation required')
        status = row.get('status')
        require(status in ('passed', 'deferred', 'superseded'), 'Unverified/failed/pending requirement blocks release')
        if status == 'passed':
            checks = row.get('checks', [])
            require(isinstance(checks, list) and bool(checks), 'Per-requirement checks required')
            for check in checks:
                require(all(text(check.get(k)) for k in ('requirement', 'scenario', 'expected', 'actual', 'evidence')), 'Incomplete check evidence')
                require(check.get('result') == 'passed', 'Check did not pass')
                require(check.get('commit') == request['commit'], 'Check belongs to another source commit')
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
    return validate_acceptance(report, registry, request)
