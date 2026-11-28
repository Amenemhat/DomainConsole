"""Drive the packaged WPF client on the isolated domain controller through UI Automation."""
import base64
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import shutil
import threading
import time


class PackagedConsole:
    def __init__(self, root, ps, domain_password):
        self.root = root
        self.ps = ps
        self.password = domain_password
        self.server = None

    def remote(self, script):
        return self.ps("DCAD", script, True)

    def start(self, before, script="gui.ps1"):
        files = {
            "/DomainConsole.zip": self.root / "package.zip",
            "/gui.ps1": Path(__file__).with_name(script),
            "/window-discovery.ps1": Path(__file__).with_name("window-discovery.ps1"),
            "/observations.json": self.root / "observations.json",
        }
        files["/observations.json"].write_text(json.dumps(before))

        class Files(BaseHTTPRequestHandler):
            def do_GET(self):
                if self.client_address[0] != "10.77.57.11" or self.path not in files:
                    self.send_error(404)
                    return
                source = files[self.path]
                self.send_response(200)
                self.send_header("Content-Length", str(source.stat().st_size))
                self.end_headers()
                with source.open("rb") as stream:
                    shutil.copyfileobj(stream, self.wfile)

        self.server = ThreadingHTTPServer(("10.77.57.1", 8765), Files)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        with files["/DomainConsole.zip"].open("rb") as archive:
            digest = hashlib.file_digest(archive, "sha256").hexdigest()
        self.remote(r"""
$p='C:\DomainConsoleLab'
$web=New-Object Net.WebClient
$web.Proxy=$null
foreach($f in 'DomainConsole.zip','observations.json','gui.ps1','window-discovery.ps1'){$web.DownloadFile("http://10.77.57.1:8765/$f","$p\$f")}
if((Get-FileHash "$p\DomainConsole.zip" -Algorithm SHA256).Hash -ne 'HASH'){throw 'Artifact changed in transit'}
Expand-Archive -LiteralPath "$p\DomainConsole.zip" -DestinationPath "$p\package"
# Windows PowerShell 5 requires a BOM to read Russian UI Automation labels.
foreach($script in 'gui.ps1','window-discovery.ps1'){
 $text=[IO.File]::ReadAllText("$p\$script",[Text.Encoding]::UTF8)
 [IO.File]::WriteAllText("$p\$script",$text,(New-Object Text.UTF8Encoding($true)))
}
$action=New-ScheduledTaskAction -Execute powershell.exe -Argument '-NoProfile -STA -ExecutionPolicy Bypass -File C:\DomainConsoleLab\gui.ps1'
Register-ScheduledTask -TaskName DCLab-Console -Action $action -User 'DCLAB\Administrator' -Password 'PASSWORD' -RunLevel Highest -Force|Out-Null
Start-ScheduledTask DCLab-Console
""".replace("HASH",digest).replace("PASSWORD",self.password))

    def wait(self, file, timeout):
        end = time.monotonic() + timeout
        while time.monotonic() < end:
            state = self.remote(r"$p='C:\DomainConsoleLab';if(Test-Path ($p+'\gui-error.txt')){'ERROR '+(Get-Content -Encoding UTF8 -Raw ($p+'\gui-error.txt'))}elseif(Test-Path ($p+'\FILE')){'OK'}".replace("FILE",file))
            if state.startswith("ERROR"):
                raise RuntimeError(state[:1500])
            if state == "OK":
                return
            time.sleep(5)
        raise TimeoutError("Packaged console: "+file)

    def mark(self, file):
        self.remote("Set-Content 'C:\\DomainConsoleLab\\"+file+"' 'True'")

    def finish(self):
        self.mark("server-reports.done")
        self.wait("gui-evidence.json", 940)
        return json.loads(self.remote(r"Get-Content -Encoding UTF8 -Raw C:\DomainConsoleLab\gui-evidence.json"))
