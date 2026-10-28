import base64
from contextlib import redirect_stdout
import json
import os
from pathlib import Path
import tempfile
from unittest import mock
import hashlib
import io
import unittest
import zipfile
from publish_release import main, validate_package, validate_request, validate_run


class ReleaseGuards(unittest.TestCase):
    def request(self):
        return {'publish': False, 'version': '0.24', 'run_id': 1,
                'commit': 'a' * 40, 'sha256': 'b' * 64}

    def package(self, extra=None):
        result = io.BytesIO()
        with zipfile.ZipFile(result, 'w') as archive:
            archive.writestr('DomainConsole.exe', b'fixture')
            archive.writestr('Agent/DomainConsole.Agent.exe', b'fixture')
            if extra:
                # Preserve the malicious ZIP entry verbatim on Windows too.
                # ZipInfo(extra) otherwise normalizes backslashes to slashes.
                entry = zipfile.ZipInfo()
                entry.filename = extra
                archive.writestr(entry, b'not allowed')
        return result.getvalue()

    def test_explicit_publication_and_exact_identity_required(self):
        self.assertFalse(validate_request(self.request())['publish'])
        for key, value in [('publish', 'false'), ('version', '../other'), ('commit', 'main'),
                           ('run_id', -1), ('sha256', '')]:
            request = self.request()
            request[key] = value
            with self.assertRaises(ValueError):
                validate_request(request)

    def test_only_successful_expected_main_build(self):
        run = {'status': 'completed', 'conclusion': 'success', 'head_sha': 'a' * 40,
               'head_branch': 'main', 'event': 'push',
               'head_repository': {'full_name': 'Amenemhat/DomainConsole'},
               'path': '.github/workflows/build-windows.yml'}
        validate_run(run, self.request(), 'Amenemhat/DomainConsole')
        for key, value in [('conclusion', 'failure'), ('head_sha', 'c' * 40),
                           ('head_branch', 'other'), ('event', 'pull_request'),
                           ('path', '.github/workflows/other.yml'),
                           ('head_repository', {'full_name': 'foreign/repository'})]:
            invalid = dict(run)
            invalid[key] = value
            with self.assertRaises(ValueError):
                validate_run(invalid, self.request(), 'Amenemhat/DomainConsole')

    def test_preview_never_calls_publication_api(self):
        data = self.package()
        request = self.request()
        request['sha256'] = hashlib.sha256(data).hexdigest()
        run = {'status': 'completed', 'conclusion': 'success', 'head_sha': 'a' * 40,
               'head_branch': 'main', 'event': 'push', 'html_url': 'https://example.invalid/build',
               'head_repository': {'full_name': 'Amenemhat/DomainConsole'},
               'path': '.github/workflows/build-windows.yml'}
        client = mock.Mock()
        client.repo_call.side_effect = [run, {'content': base64.b64encode(
            b'<Project><PropertyGroup><Version>0.24.0</Version></PropertyGroup></Project>').decode()},
            {'artifacts': [{'id': 1, 'name': 'DomainConsole-Windows-x64', 'expired': False,
                            'digest': 'sha256:' + request['sha256']}]}]
        client.download_artifact.return_value = data
        previous = Path.cwd()
        with tempfile.TemporaryDirectory() as directory:
            try:
                os.chdir(directory)
                Path('.github').mkdir()
                Path('.github/release-request.json').write_text(json.dumps(request))
                Path('docs/releases').mkdir(parents=True)
                Path('docs/releases/0.24.md').write_text('# DomainConsole 0.24\nTest')
                with mock.patch('publish_release.GitHub', return_value=client), mock.patch.dict(
                        os.environ, {'GITHUB_REPOSITORY': 'Amenemhat/DomainConsole', 'GH_TOKEN': 'fixture',
                                     'GITHUB_STEP_SUMMARY': ''}), redirect_stdout(io.StringIO()):
                    main()
            finally:
                os.chdir(previous)
        client.upload.assert_not_called()
        self.assertEqual(client.repo_call.call_count, 3)
        for call in client.repo_call.call_args_list:
            self.assertNotIn('method', call.kwargs)

    def test_checked_complete_package(self):
        data = self.package()
        digest = hashlib.sha256(data).hexdigest()
        self.assertEqual(digest, validate_package(data, digest))
        with self.assertRaises(ValueError):
            validate_package(data, '0' * 64)
        truncated = data[:-20]
        with self.assertRaises(zipfile.BadZipFile):
            validate_package(truncated, hashlib.sha256(truncated).hexdigest())

    def test_user_data_and_unexpected_paths_rejected(self):
        for path in ['Data/history.json', '../escape', '/absolute', 'Agent\\unexpected.exe']:
            with self.subTest(path=path):
                data = self.package(path)
                with self.assertRaises(ValueError):
                    validate_package(data, hashlib.sha256(data).hexdigest())


if __name__ == '__main__':
    unittest.main()
