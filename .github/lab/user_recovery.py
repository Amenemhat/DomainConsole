"""Drive real AD and WinRM interruptions on the isolated lab network."""
import json
import os
import shutil
import subprocess
import time

from packaged_gui import PackagedConsole


def exercise(root, ps, member_ip, password, evidence, save):
    client = shutil.which("xfreerdp3") or shutil.which("xfreerdp")
    if not client:
        raise RuntimeError("FreeRDP is required")
    evidence["package"] = json.loads((root / "package.json").read_text())
    expected = "DCLAB\\RecoveryFixture59"
    ps("DCAD", f"$p=ConvertTo-SecureString '{password}' -AsPlainText -Force;"
       "New-ADUser -Server 'dcad.dc057.test' -Name RecoveryFixture59 -SamAccountName RecoveryFixture59 "
       "-GivenName 'Directory' -Surname 'Fixture' -AccountPassword $p -Enabled $true", True)
    ps("CLONE1", rf"""
$group=Get-LocalGroup -SID 'S-1-5-32-555'
Add-LocalGroupMember -Group $group -Member '{expected}'
Set-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server' -Name fDenyTSConnections -Value 0
Enable-NetFirewallRule -Name RemoteDesktop-UserMode-In-TCP
Start-Service TermService
wevtutil.exe sl Microsoft-Windows-TaskScheduler/Operational /e:true
if($LASTEXITCODE){{throw 'Task execution auditing could not be enabled'}}
""")
    def set_winrm_loss(enabled):
        code = "New-NetFirewallRule -Name DCLabWinRMLoss -Direction Inbound -Action Block -Protocol TCP -LocalPort 5985 -RemoteAddress 10.77.57.10 | Out-Null"
        if not enabled:
            code = "Remove-NetFirewallRule -Name DCLabWinRMLoss"
        ps("CLONE1", code)

    console = PackagedConsole(root, ps, password, host="DCWSUS")
    with (root / "rdp-recovery-fixture.log").open("w") as log:
        rdp = subprocess.Popen(["xvfb-run", "-a", client, "/v:" + member_ip, "/d:DCLAB",
                                "/u:RecoveryFixture59", "/p:" + password, "/cert:ignore", "/size:1024x768"],
                               stdout=log, stderr=log, start_new_session=True)
        try:
            until = time.monotonic() + 100
            while time.monotonic() < until:
                if rdp.poll() is not None:
                    raise RuntimeError("The RDP test session did not open")
                reply = ps("CLONE1", "quser.exe;exit 0").lower()
                if "recoveryfixture59" in reply and "active" in reply:
                    break
                time.sleep(4)
            else:
                raise TimeoutError("Expected an active RDP session before testing failures")
            evidence["checks"]["real_rdp_session"] = True
            save()

            # Scope each failure to the console host. The runners and other domain services keep working.
            ps("DCWSUS", "New-NetFirewallRule -Name DCLabDirectoryLoss -Direction Outbound -Action Block "
               "-Protocol TCP -RemotePort 389,636,3268,3269 -RemoteAddress 10.77.57.11 | Out-Null")
            set_winrm_loss(True)
            console.start({"Host": "clone1.dc057.test", "Account": expected}, script="user-recovery.ps1")
            console.wait("winrm-without-history.done", 130)
            set_winrm_loss(False)
            console.mark("test-ad-loss.done")
            console.wait("ad-unavailable.done", 145)
            ps("DCWSUS", "Remove-NetFirewallRule -Name DCLabDirectoryLoss")
            console.mark("restore-directory.done")
            console.wait("directory-recovered.done", 250)
            set_winrm_loss(True)
            console.mark("test-winrm-with-history.done")
            console.wait("winrm-with-history.done", 155)
            set_winrm_loss(False)
            console.mark("restore-winrm.done")
            console.wait("gui-evidence.json", 150)
            evidence["checks"]["packaged_ui"] = json.loads(console.remote(
                r"Get-Content -LiteralPath C:\DomainConsoleLab\gui-evidence.json -Encoding UTF8 -Raw"))
            save()
            tasks = json.loads(ps("CLONE1", r"""
$tasks=@(Get-ScheduledTask -TaskName 'DomainConsoleCSharp-*' -ErrorAction SilentlyContinue)
$started=@(Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-TaskScheduler/Operational';Id=100} -ErrorAction SilentlyContinue |
 Where-Object {(([xml]$_.ToXml()).Event.EventData.Data | Where-Object Name -eq TaskName).'#text' -eq '\DomainConsoleCSharp-ManualReboot59'})
@{Tasks=@($tasks.TaskName); Active=@($tasks|Where-Object State -in 'Running','Queued').Count; Checks=$started.Count} | ConvertTo-Json -Compress
"""))
            evidence["checks"]["client_tasks"] = tasks
            save()
            if tasks["Tasks"] != ["DomainConsoleCSharp-ManualReboot59"] or tasks["Active"] or tasks["Checks"] != 3:
                raise RuntimeError("Expected one reused task, exactly three explicit reads and no active task at completion")
        finally:
            if console.server:
                console.server.shutdown()
            if rdp.poll() is None:
                os.killpg(rdp.pid, 15)
                rdp.wait(timeout=10)
    print("PASS: actual AD and WinRM failure and recovery on the exact packaged console.", flush=True)
