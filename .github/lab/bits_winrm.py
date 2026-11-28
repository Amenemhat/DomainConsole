"""Confirm that BITS on a disposable Server 2019 can be read and resumed via WinRM."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import re
import threading
import time


HOST = "10.77.57.1"
GUEST = "10.77.57.10"
ORIGIN = f"http://{HOST}:8765"
DATA = b"DomainConsole BITS test\n" * 120000


class Content(BaseHTTPRequestHandler):
    def serve_file(self, headers_only):
        if self.client_address[0] != GUEST or self.path not in ("/Content/AA/selected.cab", "/Content/AA/peer.cab"):
            self.send_error(404)
            return
        start, end = 0, len(DATA) - 1
        header = self.headers.get("Range")
        if header:
            match = re.fullmatch(r"bytes=(\d+)-(\d*)", header)
            if not match or int(match[1]) > end:
                self.send_error(416)
                return
            start = int(match[1])
            if match[2]:
                end = min(end, int(match[2]))
        self.send_response(206 if header else 200)
        self.send_header("Content-Length", str(end - start + 1))
        self.send_header("Content-Type", "application/octet-stream")
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Last-Modified", "Tue, 01 Sep 2026 00:00:00 GMT")
        if header:
            self.send_header("Content-Range", f"bytes {start}-{end}/{len(DATA)}")
        self.end_headers()
        if not headers_only:
            try:
                for offset in range(start, end + 1, 16384):
                    self.wfile.write(DATA[offset:min(end + 1, offset + 16384)])
                    time.sleep(.15)
            except (BrokenPipeError, ConnectionResetError):
                pass

    def do_HEAD(self):
        self.serve_file(True)

    def do_GET(self):
        self.serve_file(False)


def literal(s):
    return "'" + s.replace("'", "''") + "'"


def bounded(console, body):
    text = (console / "ManualReboot59.cs").read_text(encoding="utf-8")
    template = re.search(r'ManualRebootTemplate59="""\n(.*?)\n""";', text, re.S).group(1)
    worker = (
        "$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';try{$value=& {\n" + body
        + "\n}|ConvertFrom-Json -ErrorAction Stop;$value|ConvertTo-Json -Depth 12 -Compress"
        + "|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'result.tmp') -Encoding UTF8;"
        + "Move-Item -LiteralPath (Join-Path $PSScriptRoot 'result.tmp') -Destination "
        + "(Join-Path $PSScriptRoot 'result.json') -Force}catch{[IO.File]::WriteAllText"
        + "((Join-Path $PSScriptRoot 'error.txt'),$_.Exception.Message);exit 1}"
    )
    return template.replace("@@DIRECTORY@@", literal("BitsControl187")).replace("@@BODY@@", literal(worker))


def exercise(ps, system, evidence, save):
    console = Path(os.environ["GITHUB_WORKSPACE"]) / "candidate/DomainConsole-CSharp/Console"
    code = (console / "BitsRecovery187.cs").read_text(encoding="utf-8")
    if 'BitsActivitySystemScript187()=>BoundedSystemTask59(LiveUpdateScript,"BitsControl187")' not in code:
        raise RuntimeError("Candidate does not route BITS preflight through the shared SYSTEM task")
    activity_code = (console / "LiveActivity.cs").read_text(encoding="utf-8")
    activity = re.search(r'LiveUpdateScript="""\n(.*?)\n""";', activity_code, re.S).group(1)
    resume = (console / "Polling42/bits-resume187.ps1").read_text(encoding="utf-8-sig")
    checks = evidence["checks"]
    evidence["candidate"] = os.environ["APP_COMMIT"]
    checks["os"] = json.loads(ps("DCWSUS", r"""
$os=Get-CimInstance Win32_OperatingSystem
@{Caption=$os.Caption;Build=$os.BuildNumber;PowerShell=$PSVersionTable.PSVersion.ToString()
  Session=(Get-Process -Id $PID).SessionId;Account=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value}|ConvertTo-Json -Compress
"""))
    save()
    if checks["os"]["Build"] != "17763" or checks["os"]["Account"] == "S-1-5-18":
        raise RuntimeError("Expected genuine WinRM into disposable Server 2019 under an administrator")
    seed = r"""
$policy='@@ORIGIN@@'
$path='HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
New-Item -Path $path -Force|Out-Null
New-Item -Path (Join-Path $path 'AU') -Force|Out-Null
New-ItemProperty -LiteralPath $path -Name WUServer -PropertyType String -Value $policy -Force|Out-Null
New-ItemProperty -LiteralPath $path -Name WUStatusServer -PropertyType String -Value $policy -Force|Out-Null
New-ItemProperty -LiteralPath (Join-Path $path 'AU') -Name UseWUServer -PropertyType DWord -Value 1 -Force|Out-Null
Import-Module BitsTransfer -ErrorAction Stop
$jobs=@(foreach($file in 'selected','peer'){
    $label=if($file -eq 'selected'){'WU Client Download'}else{'DC lab peer'}
    Start-BitsTransfer -Source "$policy/Content/AA/$file.cab" -Destination "C:\DomainConsoleLab\$file.cab" -DisplayName $label -Asynchronous -Suspended:($file -eq 'peer') -Priority Foreground -ProxyUsage NoProxy
})
$end=[DateTime]::UtcNow.AddSeconds(30)
do{
    $transfer=Get-BitsTransfer -JobId $jobs[0].JobId
    if($transfer.BytesTransferred -gt 0){break}
    Start-Sleep -Milliseconds 400
}while([DateTime]::UtcNow -lt $end)
if($transfer.BytesTransferred -le 0 -or $transfer.BytesTransferred -ge $transfer.BytesTotal){throw 'No partial transfer available'}
Suspend-BitsTransfer -BitsJob $transfer -Confirm:$false -ErrorAction Stop
@{Id=$transfer.JobId.ToString();Peer=$jobs[1].JobId.ToString()
  Service=(Get-CimInstance Win32_Service -Filter "Name='BITS'").ProcessId}|ConvertTo-Json -Compress |
    Set-Content -LiteralPath C:\DomainConsoleLab\bits-fixture.json -Encoding utf8
""".replace("@@ORIGIN@@", ORIGIN)
    with ThreadingHTTPServer((HOST, 8765), Content) as server:
        threading.Thread(target=server.serve_forever, daemon=True).start()
        try:
            print("Start one partially received SYSTEM BITS download and its suspended peer", flush=True)
            system("DCWSUS", "bits-winrm-fixture", seed, minutes=3)
            fixture = json.loads(ps("DCWSUS", r"Get-Content C:\DomainConsoleLab\bits-fixture.json -Raw"))
            direct = json.loads(ps("DCWSUS", activity))
            checks["direct_winrm_wua"] = direct["Limitation"]
            save()
            if "80070005" not in direct["Limitation"]:
                raise RuntimeError("Expected the WinRM WUA access failure reported by the operator")
            print("Direct WinRM WUA denied; acquire the same data with the candidate SYSTEM task", flush=True)
            fresh = json.loads(ps("DCWSUS", bounded(console, activity)))
            checks["system_preflight"] = fresh
            save()
            downloads = [x for x in fresh["Downloads"] if x["Id"] == fixture["Id"]]
            if fresh["Limitation"] or fresh["InstallerBusy"] or fresh["InstallationEvidence"] or len(downloads) != 1:
                raise RuntimeError("SYSTEM preflight did not return a clean snapshot of the same job")
            before = downloads[0]
            if before["State"] != "Suspended" or not before["SystemOwned"] or before["Received"] <= 0:
                raise RuntimeError("SYSTEM snapshot did not confirm the original partial transfer")
            request = {k: before[k] for k in ("Id", "State", "Received", "Total", "RemoteName", "LocalName", "ModifiedUtc")}
            request.update(PolicySource=fresh["PolicySource"], Automatic=False)
            checks["resume"] = json.loads(ps("DCWSUS", bounded(console, resume.replace("@@REQUEST@@", literal(json.dumps(request))))))
            sample = json.loads(ps("DCWSUS", rf"""
Import-Module BitsTransfer
$jobs=@(Get-BitsTransfer -AllUsers)
$selected=$jobs|Where-Object JobId -eq ([guid]'{fixture['Id']}')
$peer=$jobs|Where-Object JobId -eq ([guid]'{fixture['Peer']}')
$tasks=@(Get-ScheduledTask -TaskName 'DomainConsoleCSharp-*')
@{{Id=[string]$selected.JobId;Bytes=[long]$selected.BytesTransferred;State=[string]$selected.JobState
 PeerState=[string]$peer.JobState;PeerBytes=[long]$peer.BytesTransferred
 Service=(Get-CimInstance Win32_Service -Filter "Name='BITS'").ProcessId
 Tasks=@($tasks.TaskName);Active=@($tasks|Where-Object State -in 'Running','Queued').Count}}|ConvertTo-Json -Compress
"""))
            checks["after"] = sample
            save()
            if (sample["Id"] != fixture["Id"] or sample["Bytes"] <= before["Received"] or sample["PeerState"] != "Suspended"
                    or sample["PeerBytes"] != 0 or sample["Service"] != fixture["Service"] or sample["Active"]
                    or sample["Tasks"] != ["DomainConsoleCSharp-BitsControl187"]):
                raise RuntimeError("Resume, byte growth, or bounded resource expectations failed on the guest")
            print("PASS same GUID grew via WinRM and one reusable SYSTEM task; peer and service intact", flush=True)
        finally:
            server.shutdown()
