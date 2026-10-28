"""Publish only an explicitly requested, already tested Windows artifact."""
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import re
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from acceptance_gate import load_acceptance

API = 'https://api.github.com'


def validate_request(value):
    if type(value.get('publish')) is not bool:
        raise ValueError('publish must be explicitly true or false')
    if not re.fullmatch(r'\d+\.\d+(?:\.\d+)?', value.get('version', '')):
        raise ValueError('Invalid version')
    if not re.fullmatch(r'[0-9a-f]{40}', value.get('commit', '')):
        raise ValueError('Exact source commit required')
    if type(value.get('run_id')) is not int or value['run_id'] <= 0:
        raise ValueError('Successful source run required')
    if not re.fullmatch(r'[0-9a-f]{64}', value.get('sha256', '')):
        raise ValueError('Checksum of the verified artifact required')
    return value


def validate_run(run, request, repository):
    if (run.get('status'), run.get('conclusion')) != ('completed', 'success'):
        raise ValueError('Source build has not passed')
    if run.get('head_sha') != request['commit']:
        raise ValueError('Source build commit differs from requested commit')
    if run.get('head_branch') != 'main' or run.get('event') not in ('push', 'workflow_dispatch'):
        raise ValueError('Only a trusted main build can be released')
    if run.get('head_repository', {}).get('full_name', '').lower() != repository.lower():
        raise ValueError('Foreign build is not allowed')
    if run.get('path', '').split('@')[0] != '.github/workflows/build-windows.yml':
        raise ValueError('Unexpected source workflow')


def validate_package(data, expected_digest):
    digest = hashlib.sha256(data).hexdigest()
    if digest != expected_digest:
        raise ValueError('Archive checksum mismatch')
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        names = archive.namelist()
        if not {'DomainConsole.exe', 'Agent/DomainConsole.Agent.exe'}.issubset(names):
            raise ValueError('Package is missing required executables')
        if any(n.startswith(('Data/', '/')) or '..' in Path(n).parts or '\\' in n for n in names):
            raise ValueError('Package contains user data or unexpected paths')
        if archive.testzip() is not None:
            raise ValueError('Archive integrity check failed')
    return digest


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class GitHub:
    def __init__(self, repository, token):
        if not re.fullmatch(r'[\w.-]+/[\w.-]+', repository):
            raise ValueError('Invalid repository')
        self.repository = repository
        self.token = token

    def call(self, endpoint, method='GET', value=None, allow_missing=False):
        data = None if value is None else json.dumps(value).encode()
        req = urllib.request.Request(API + endpoint, data=data, method=method,
            headers={'Authorization': 'Bearer ' + self.token, 'Accept': 'application/vnd.github+json',
                     'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'DomainConsole-release'})
        try:
            with urllib.request.urlopen(req, timeout=60) as response:
                return json.load(response)
        except urllib.error.HTTPError as error:
            if allow_missing and error.code == 404:
                return None
            raise RuntimeError(f'GitHub API {method} failed: HTTP {error.code}') from None

    def repo_call(self, endpoint, **kwargs):
        return self.call('/repos/' + self.repository + endpoint, **kwargs)

    def download_artifact(self, artifact_id):
        endpoint = API + '/repos/' + self.repository + f'/actions/artifacts/{artifact_id}/zip'
        request = urllib.request.Request(endpoint, headers={
            'Authorization': 'Bearer ' + self.token, 'User-Agent': 'DomainConsole-release'})
        try:
            urllib.request.build_opener(NoRedirect).open(request, timeout=60)
        except urllib.error.HTTPError as error:
            if error.code not in (301, 302, 303, 307, 308):
                raise RuntimeError(f'Artifact download failed: HTTP {error.code}') from None
            location = error.headers['Location']
        else:
            raise RuntimeError('Expected a signed artifact redirect')
        if urllib.parse.urlparse(location).scheme != 'https':
            raise ValueError('Artifact redirect must use HTTPS')
        # Deliberately start a fresh request: never forward the API token to blob storage.
        with urllib.request.urlopen(location, timeout=120) as response:
            return response.read()

    def upload(self, release_id, name, data, mime):
        url = ('https://uploads.github.com/repos/' + self.repository + f'/releases/{release_id}/assets?'
               + urllib.parse.urlencode({'name': name}))
        request = urllib.request.Request(url, data=data, method='POST', headers={
            'Authorization': 'Bearer ' + self.token, 'Content-Type': mime,
            'User-Agent': 'DomainConsole-release'})
        with urllib.request.urlopen(request, timeout=180) as response:
            return json.load(response)


def main():
    request = validate_request(json.loads(Path('.github/release-request.json').read_text()))
    # Publication requires an explicit review of the exact artifact, not merely green CI.
    # Preview remains available for preparing evidence; it cannot publish or close issues.
    if request['publish']:
        load_acceptance(request)
    repository = os.environ['GITHUB_REPOSITORY']
    client = GitHub(repository, os.environ['GH_TOKEN'])
    run = client.repo_call(f"/actions/runs/{request['run_id']}")
    validate_run(run, request, repository)
    source = client.repo_call('/contents/DomainConsole-CSharp/Console/DomainConsole.csproj?ref=' + request['commit'])
    project = ET.fromstring(base64.b64decode(source['content']))
    version = project.findtext('.//Version', '')
    parts = lambda v: tuple(map(int, (v.split('.') + ['0', '0'])[:3]))
    if parts(version) != parts(request['version']):
        raise ValueError('Requested version differs from tested source')
    artifacts = client.repo_call(f"/actions/runs/{request['run_id']}/artifacts?per_page=100")['artifacts']
    matches = [a for a in artifacts if a['name'] == 'DomainConsole-Windows-x64' and not a['expired']]
    if len(matches) != 1:
        raise ValueError('Exactly one nonexpired Windows package required')
    artifact = matches[0]
    if artifact.get('digest') and artifact['digest'] != 'sha256:' + request['sha256']:
        raise ValueError('Source artifact digest differs from verified package')
    data = None
    for attempt in range(3):
        try:
            data = client.download_artifact(artifact['id'])
            digest = validate_package(data, request['sha256'])
            break
        except (ValueError, zipfile.BadZipFile, urllib.error.URLError, OSError):
            if attempt == 2:
                raise
            time.sleep(2)
    notes = Path('docs/releases', request['version'] + '.md').read_text(encoding='utf-8')
    if not notes.startswith('# DomainConsole ' + request['version'] + '\n'):
        raise ValueError('Versioned release notes required')
    tag = 'v' + request['version']
    name = 'DomainConsole-' + request['version'] + '-Windows-x64.zip'
    checksum_name = 'SHA256SUMS.txt'
    checksum = (digest + '  ' + name + '\n').encode()
    summary = f"## {tag}\n\nSource: `{request['commit']}`\n\nBuild: {run['html_url']}\n\nSHA-256: `{digest}`\n\n"
    if not request['publish']:
        summary += 'Preview verified. No tag or release was created.\n'
    else:
        ref = client.repo_call('/git/ref/tags/' + tag, allow_missing=True)
        if ref:
            obj = ref['object']
            if obj['type'] == 'tag':
                obj = client.repo_call('/git/tags/' + obj['sha'])['object']
            if obj['type'] != 'commit' or obj['sha'] != request['commit']:
                raise ValueError('Existing tag points to a different commit; refusing to move it')
        release = client.repo_call('/releases/tags/' + tag, allow_missing=True)
        if release is None:
            release = client.repo_call('/releases', method='POST', value={
                'tag_name': tag, 'target_commitish': request['commit'], 'name': 'DomainConsole ' + request['version'],
                'body': notes, 'draft': True, 'prerelease': False})
        expected = [(name, data, 'application/zip'), (checksum_name, checksum, 'text/plain')]
        assets = {a['name']: a for a in release.get('assets', [])}
        for asset_name, contents, mime in expected:
            if asset_name in assets:
                if assets[asset_name].get('digest') != 'sha256:' + hashlib.sha256(contents).hexdigest():
                    raise ValueError('Existing release asset differs; refusing to overwrite it')
            elif not release['draft']:
                raise ValueError('Published release is incomplete; refusing to modify it silently')
            else:
                client.upload(release['id'], asset_name, contents, mime)
        if release['draft']:
            release = client.repo_call('/releases/' + str(release['id']), method='PATCH', value={
                'draft': False, 'make_latest': 'true'})
        summary += 'Release: ' + release['html_url'] + '\n'
    print(summary)
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as stream:
            stream.write(summary)


if __name__ == '__main__':
    main()
