import copy
import unittest
from acceptance_gate import batch_scope, validate_acceptance
import test_acceptance_gate

class BatchTests(unittest.TestCase):
    def setUp(self):
        base=test_acceptance_gate.AcceptanceTests();base.setUp()
        self.report,self.registry,self.request=base.report,base.registry,base.request
        self.registry['issues'].append(dict(issue=2,conditions=[dict(id='2.other',requirement='Later feature')]))
        self.plan=dict(schema=1,batches=[dict(version='0.47',issues=[1],condition_ids=['1.single']),dict(version='0.48',issues=[2],condition_ids=['2.other'])],mandatory_scenarios_each_release=['single-instance'])
    def validate(self):
        return validate_acceptance(self.report,self.registry,self.request,self.plan)
    def test_complete_batch(self):
        self.assertEqual(self.validate(),[1])
    def test_invalid_allocation(self):
        for change in ('missing','duplicate','condition','scenario','version','unknown'):
            with self.subTest(change=change):
                plan=copy.deepcopy(self.plan)
                if change=='missing':plan['batches'].pop()
                if change=='duplicate':plan['batches'][1]['issues']=[1]
                if change=='condition':plan['batches'][0]['condition_ids']=[]
                if change=='scenario':plan['mandatory_scenarios_each_release']=[]
                if change=='version':plan['batches'][1]['version']='0.47'
                if change=='unknown':plan['batches'][0]['issues']=[99]
                with self.assertRaises(ValueError):validate_acceptance(self.report,self.registry,self.request,plan)
    def test_no_deferred_selected_issues(self):
        self.report['issues'][0].update(status='deferred',reason='Later',tracking_issue='#1',keep_open=True)
        self.report['close_issues']=[]
        with self.assertRaises(ValueError):self.validate()
    def test_out_of_scope_closure(self):
        self.report['close_issues']=[2]
        with self.assertRaises(ValueError):self.validate()
    def test_mandatory_still_required(self):
        self.report['scenarios']=[]
        with self.assertRaises(ValueError):self.validate()
    def test_real_allocation(self):
        import json
        from pathlib import Path
        root=Path(__file__).resolve().parents[2]
        registry=json.loads((root/'docs/acceptance/requirements.json').read_text(encoding="utf-8"))
        plan=json.loads((root/'docs/acceptance/release-batches.json').read_text(encoding="utf-8"))
        self.assertEqual(batch_scope(registry,plan,'0.48'),set(range(113,119)))

if __name__=='__main__':unittest.main()
