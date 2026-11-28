"""Drive real AD and WinRM interruptions on the isolated lab network."""
import json
import time

from packaged_gui import PackagedConsole


def exercise(root, ps, password, evidence, save):
    evidence["package"] = json.loads((root / "package.json").read_text())
    expected = "DCLAB\\RecoveryFixture59"
    ps("DCAD", f"$p=ConvertTo-SecureString '{password}' -AsPlainText -Force;"
       "New-ADUser -Server 'dcad.dc057.test' -Name RecoveryFixture59 -SamAccountName RecoveryFixture59 "
       "-GivenName 'Directory' -Surname 'Fixture' -AccountPassword $p -Enabled $true", True)
    # The lab supplies one real console login. The previous WTS matrix separately covers RDP.
    ps("CLONE1", rf"""
if($env:COMPUTERNAME -ne 'CLONE1' -or !(Test-Path 'C:\DomainConsoleLab\node.json')){{throw 'Disposable Windows member required'}}
$path='HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
$values=@{{AutoAdminLogon='1';DefaultDomainName='DCLAB';DefaultUserName='RecoveryFixture59';DefaultPassword='{password}'}}
foreach($entry in $values.GetEnumerator()){{
    New-ItemProperty -Path $path -Name $entry.Key -Value $entry.Value -PropertyType String -Force | Out-Null
}}
New-ItemProperty -Path $path -Name AutoLogonCount -Value 1 -PropertyType DWord -Force | Out-Null
wevtutil.exe sl Microsoft-Windows-TaskScheduler/Operational /e:true
if($LASTEXITCODE){{throw 'Task execution auditing could not be enabled'}}
shutdown.exe /r /t 3
""")
    time.sleep(15)
    deadline = time.monotonic() + 150
    current_user = ""
    while time.monotonic() < deadline:
        try:
            current_user = ps("CLONE1", "(Get-CimInstance Win32_ComputerSystem).UserName")
            if current_user.lower() == expected.lower():
                break
        except Exception:
            pass
        time.sleep(5)
    else:
        evidence["checks"]["console_login"] = {"expected": expected, "observed": current_user}
        save()
        raise TimeoutError("The disposable member did not report the actual domain console login")
    ps("CLONE1", r"""
$path='HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
Set-ItemProperty -Path $path -Name AutoAdminLogon -Value '0'
Remove-ItemProperty -Path $path -Name DefaultPassword,AutoLogonCount -ErrorAction SilentlyContinue
""")
    evidence["checks"]["console_login"] = {"observed": current_user, "verified": True}
    save()

    def set_winrm_loss(enabled):
        code = "New-NetFirewallRule -Name DCLabWinRMLoss -Direction Inbound -Action Block -Protocol TCP -LocalPort 5985 -RemoteAddress 10.77.57.10 | Out-Null"
        if not enabled:
            code = "Remove-NetFirewallRule -Name DCLabWinRMLoss"
        ps("CLONE1", code)

    console = PackagedConsole(root, ps, password, host="DCWSUS")
    try:
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
    print("PASS: actual AD and WinRM failure and recovery on the exact packaged console.", flush=True)
