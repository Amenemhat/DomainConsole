"""Drive real AD and WinRM interruptions on the isolated lab network."""
import json

from packaged_gui import PackagedConsole
from session_matrix58 import exercise as exercise_sessions


def exercise(root, ps, password, evidence, save):
    evidence["package"] = json.loads((root / "package.json").read_text())
    expected = "DCLAB\\RdpDomain58"
    # Use the member and session reader from the passing local/domain RDP scenario.
    ps("DCWSUS", "wevtutil.exe sl Microsoft-Windows-TaskScheduler/Operational /e:true;"
       "if($LASTEXITCODE){throw 'Task execution auditing could not be enabled'}")
    exercise_sessions(root, ps, "10.77.57.10", password, evidence, save,
                      when_domain_session_active=lambda: observe_failures(root, ps, password, expected, evidence, save))


def observe_failures(root, ps, password, expected, evidence, save):
    def set_winrm_loss(enabled):
        code = "New-NetFirewallRule -Name DCLabWinRMLoss -Direction Inbound -Action Block -Protocol TCP -LocalPort 5985 -RemoteAddress 10.77.57.21 | Out-Null"
        if not enabled:
            code = "Remove-NetFirewallRule -Name DCLabWinRMLoss"
        ps("DCWSUS", code)

    console = PackagedConsole(root, ps, password, host="CLONE1")
    try:
        # Scope each failure to the console host. The runners and other domain services keep working.
        ps("CLONE1", "New-NetFirewallRule -Name DCLabDirectoryLoss -Direction Outbound -Action Block "
           "-Protocol TCP -RemotePort 389,636,3268,3269 -RemoteAddress 10.77.57.11 | Out-Null")
        set_winrm_loss(True)
        console.start({"Host": "dcwsus.dc057.test", "Account": expected}, script="user-recovery.ps1")
        console.wait("winrm-without-history.done", 130)
        set_winrm_loss(False)
        console.mark("test-ad-loss.done")
        console.wait("ad-unavailable.done", 145)
        ps("CLONE1", "Remove-NetFirewallRule -Name DCLabDirectoryLoss")
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
        tasks = json.loads(ps("DCWSUS", r"""
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
    print("PASS: actual AD and WinRM failure and recovery on the exact packaged console.", flush=True)
