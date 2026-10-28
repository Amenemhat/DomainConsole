import copy
import unittest
from unittest import mock
from acceptance_gate import validate_acceptance
from publish_release import main

class AcceptanceTests(unittest.TestCase):
    def setUp(self):
        self.request = dict(version='0.47', commit='a'*40, run_id=7, sha256='b'*64)
        self.registry = dict(issues=[dict(issue=1,conditions=[dict(id='1.single',requirement='Only one collector')])], mandatory_scenarios=['single-instance'])
        self.check = dict(condition_id='1.single',requirement='Only one collector', scenario='Two launches', expected='One process', actual='One process observed', evidence='Windows run / test log', result='passed', commit='a'*40)
        self.report = dict(schema=1, **self.request, reviewer='Reviewer', reviewed_at='2026-10-28T12:00:00+03:00', issues=[dict(issue=1, current_requirement='One collector per endpoint', status='passed', checks=[self.check])], scenarios=[dict(id='single-instance', **self.check)], close_issues=[1])
    def validate(self):
        return validate_acceptance(self.report, self.registry, self.request)
    def test_complete(self):
        self.assertEqual(self.validate(), [1])
    def test_one_check_cannot_accept_multiple_conditions(self):
        self.registry['issues'][0]['conditions'].append(dict(id='1.cancel',requirement='Cancel only the collector'))
        with self.assertRaises(ValueError): self.validate()
        extra=copy.deepcopy(self.check);extra['condition_id']='1.cancel'
        self.report['issues'][0]['checks'].append(extra)
        self.assertEqual(self.validate(),[1])
    def test_unclassified_registry_cannot_be_certified(self):
        self.registry['issues'][0].pop('conditions')
        with self.assertRaises(ValueError): self.validate()
    def test_unknown_condition_or_issue_rejected(self):
        self.check['condition_id']='invented'
        with self.assertRaises(ValueError): self.validate()
        self.check['condition_id']='1.single'
        self.report['issues'].append(dict(issue=999,current_requirement='Extra',status='deferred',reason='Pending',tracking_issue='#999',keep_open=True))
        with self.assertRaises(ValueError): self.validate()
    def test_identity_mismatch(self):
        for key in self.request:
            with self.subTest(key=key):
                r=copy.deepcopy(self.report); r[key]='different'
                with self.assertRaises(ValueError): validate_acceptance(r,self.registry,self.request)
    def test_missing_or_duplicate_issue(self):
        for rows in ([],self.report['issues']*2):
            self.report['issues']=rows
            with self.assertRaises(ValueError): self.validate()
    def test_unverified_or_incomplete(self):
        for status in ('pending','failed','unknown'):
            self.report['issues'][0]['status']=status
            with self.assertRaises(ValueError): self.validate()
        self.report['issues'][0]['status']='passed'
        self.report['issues'][0]['checks']=[]
        with self.assertRaises(ValueError): self.validate()
    def test_old_evidence(self):
        self.check['commit']='c'*40
        with self.assertRaises(ValueError): self.validate()
    def test_deferred_cannot_close(self):
        row=self.report['issues'][0]; row.update(status='deferred',reason='Not implemented',tracking_issue='#114',keep_open=True)
        with self.assertRaises(ValueError): self.validate()
        self.report['close_issues']=[]
        self.assertEqual(self.validate(),[])
    def test_supersession_needs_validated_successor_and_decision(self):
        self.registry['issues'].append(dict(issue=2))
        self.report['issues'].append(dict(issue=2,current_requirement='Old rule',status='superseded',replacement_issue=1,decision_evidence='Issue comment accepting new rule'))
        self.validate()
        self.report['issues'][1]['replacement_issue']=3
        with self.assertRaises(ValueError): self.validate()
    def test_mandatory_safety_not_deferrable(self):
        for status in ('failed','deferred','pending'):
            self.report['scenarios'][0]['result']=status
            with self.assertRaises(ValueError): self.validate()
        self.report['scenarios']=[]
        with self.assertRaises(ValueError): self.validate()
    def test_publication_stops_before_network_without_acceptance(self):
        request=dict(self.request,publish=True)
        with mock.patch('publish_release.Path.read_text', return_value=__import__('json').dumps(request)), mock.patch('publish_release.load_acceptance',side_effect=ValueError('Missing acceptance')), mock.patch('publish_release.GitHub') as api:
            with self.assertRaises(ValueError): main()
            api.assert_not_called()

if __name__ == '__main__': unittest.main()
