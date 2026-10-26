import hashlib
import json
import unittest
from pathlib import Path
from lookup import lookup, unsigned32

ROOT=Path(__file__).resolve().parent

class ReferenceTests(unittest.TestCase):
    def test_normalization(self):
        self.assertEqual(unsigned32(-2146498168),0x800F0988)
        self.assertEqual(lookup('-2146498168','hresult')['entry']['code'],'0x800F0988')
        self.assertEqual(lookup('1455','win32')['entry']['code'],'0x800705AF')
        for invalid in (True,2**32,-2**31-1,'CBS 0x800F0988'):
            with self.assertRaises(ValueError): unsigned32(invalid)

    def test_namespaces(self):
        self.assertEqual(lookup('1','exit','cmd.exe')['status'],'program_contract_required')
        self.assertEqual(lookup('1','hresult')['entry']['symbols'],['S_FALSE'])
        self.assertEqual(lookup('1','wua-result')['message_ru'],'Выполняется')
        self.assertEqual(lookup('182','event')['status'],'provider_required')
        self.assertEqual(lookup('3010','exit','msiexec.exe')['status'],'program_contract_required')
        with self.assertRaises(ValueError): lookup('1','guess')

    def test_examples(self):
        e=lookup('0x800f0988','hresult')['entry']
        self.assertIn('PSFX_E_INVALID_DELTA_COMBINATION',e['symbols'])
        self.assertIn('дельта',e['card']['meaning_ru'])
        self.assertEqual(lookup('0x80200059','hresult')['entry']['card']['diagnostic_profile'],'cost')
        self.assertEqual(lookup('0x80242014','hresult')['entry']['card']['severity'],'reboot')
        self.assertEqual(lookup('0x00240005','hresult')['entry']['card']['severity'],'reboot')
        self.assertNotEqual(lookup('0x800106ba','hresult')['entry']['card']['diagnostic_profile'],
                            lookup('0x800706ba','hresult')['entry']['card']['diagnostic_profile'])
        self.assertEqual(lookup('0xDEADBEEF','hresult')['status'],'unknown')

    def test_wof_file_corruption(self):
        entry=lookup('-2147020448','hresult')['entry']
        self.assertEqual(entry['code'],'0x80071160')
        self.assertEqual(entry['symbols'],['ERROR_WOF_FILE_RESOURCE_TABLE_CORRUPT'])
        self.assertEqual(entry['win32_value'],4448)
        self.assertEqual(lookup('4448','win32')['entry']['code'],'0x80071160')
        self.assertEqual(entry['card']['diagnostic_profile'],'wof')
        self.assertFalse(entry['card']['automatic_repair_allowed'])
        self.assertIn('По одному коду нельзя',entry['card']['possible_causes_ru'][0])
        self.assertEqual(lookup('-65536','exit','powershell.exe')['status'],'program_contract_required')
        self.assertEqual(lookup('0xFFFF0000','hresult')['status'],'unknown')

    def test_integrity_and_coverage(self):
        data=json.loads((ROOT/'catalog.json').read_text())
        entries=data['entries']
        self.assertEqual(len(entries),len({e['code'] for e in entries}))
        self.assertEqual(data['counts']['russian_cards'],sum('card' in e for e in entries))
        self.assertGreater(len(entries),4000)
        for e in entries:
            self.assertTrue(e['sources'],e['code'])
            self.assertTrue(all(s.startswith('https://learn.microsoft.com/') or s.startswith('https://github.com/microsoft/') for s in e['sources']))
            if 'card' in e:
                self.assertFalse(e['card']['automatic_repair_allowed'])
                self.assertTrue(e['card']['checks_ru'])
                self.assertNotIn('Подробности смотрите в выводе и журналах',e['card']['meaning_ru'])
        manifest=json.loads((ROOT/'manifest.json').read_text())
        for name,digest in manifest['sha256'].items():
            self.assertEqual(hashlib.sha256((ROOT/name).read_bytes()).hexdigest(),digest)

if __name__=='__main__': unittest.main()
