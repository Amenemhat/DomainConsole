"""Prove transport loss after actual submission from the unchanged candidate."""
import base64
import json
from pathlib import Path
import time

from packaged_gui import PackagedConsole


def exercise(root, ps, password, evidence, save):
    fixture = Path(__file__).with_name("manual-ui-endpoint.ps1")
    encoded = base64.b64encode(fixture.read_bytes()).decode("ascii")
    evidence["package"] = json.loads((root / "package.json").read_text())
    checks = evidence["checks"]
    guests = ("DCWSUS", "CLONE1")
    for node in guests:
        ps(node, "Start-Service BITS;wevtutil.exe sl Microsoft-Windows-TaskScheduler/Operational /e:true;"
           "if($LASTEXITCODE){throw 'Task execution auditing could not be enabled'}")
    checks["winrm_fault_fixture"] = {"node": guests[0], "phase": "configure"}
    save()

    # Only this guest uses the fixture. The second guest retains its stock transport.
    ps(guests[0], rf"""
$p='C:\DomainConsoleLab\manual-ui-endpoint.ps1'
[IO.File]::WriteAllBytes($p,[Convert]::FromBase64String('{encoded}'))
Set-PSSessionConfiguration -Name Microsoft.PowerShell -StartupScript $p -NoServiceRestart -Force
$a=New-ScheduledTaskAction -Execute powershell.exe -Argument '-NoProfile -Command "Start-Sleep 5; Restart-Service WinRM -Force"'
Register-ScheduledTask DCLab-ReloadEndpoint -Action $a -User SYSTEM -RunLevel Highest -Force | Out-Null
Start-ScheduledTask DCLab-ReloadEndpoint
""")
    deadline = time.monotonic() + 110
    last_problem = "No reply from the test endpoint"
    while time.monotonic() < deadline:
        try:
            # pywinrm.run_ps uses WinRS. The application uses a PowerShell PSSession.
            result = ps("DCAD", rf"""
$secret=ConvertTo-SecureString '{password}' -AsPlainText -Force
$cred=[pscredential]::new('DCLAB\Administrator',$secret)
$opt=New-PSSessionOption -OpenTimeout 12000 -OperationTimeout 15000
Invoke-Command -ComputerName 'dcwsus.dc057.test' -ConfigurationName Microsoft.PowerShell -Credential $cred -SessionOption $opt -ScriptBlock {{
    $command=Get-Command Start-ScheduledTask
    if($global:DCLabReceiptFixtureReady -and $command.ScriptBlock.File -like '*manual-ui-endpoint.ps1'){{'DCLAB_READY'}}
    else{{'Endpoint script not loaded: '+$command.ModuleName+' | '+$command.ScriptBlock.File}}
}}
""", True)
            if result == "DCLAB_READY":
                break
            last_problem = result
        except RuntimeError as error:
            last_problem = str(error)
        time.sleep(4)
    else:
        checks["winrm_fault_fixture"]["reason"] = last_problem
        save()
        raise TimeoutError("One-shot PowerShell remoting fixture did not load: " + last_problem)
    ps(guests[0], "Unregister-ScheduledTask DCLab-ReloadEndpoint -Confirm:$false")
    checks["winrm_fault_fixture"]["phase"] = "ready"
    save()

    console = PackagedConsole(root, ps, password)
    try:
        console.start({}, script="manual-ui.ps1")
        console.wait("receipt-repeated.done", 480)
        ps(guests[0], "Remove-NetFirewallRule -Name DCLabReceiptPaused")
        console.wait("gui-evidence.json", 300)
        checks["packaged_ui"] = json.loads(console.remote(
            r"Get-Content -Encoding UTF8 -Raw C:\DomainConsoleLab\gui-evidence.json"))
        save()
        states = checks["guest_results"] = {}
        for node in guests:
            actual = ps(node, r"""
$folder=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$matched=@(Get-ChildItem $folder -Directory | Where-Object {
    $p=Join-Path $_.FullName 'job.json'
    (Test-Path $p) -and ((Get-Content -Raw $p) -match 'function Save-Dependents')
})
if($matched.Count -ne 1){throw "Expected one job directory, found $($matched.Count)"}
$id=$matched[0].Name
$job=$matched[0].FullName
$state=Get-Content (Join-Path $job 'status.json') -Raw | ConvertFrom-Json
$tasks=@(Get-ScheduledTask -TaskName "DomainConsoleCSharp-$id" -ErrorAction SilentlyContinue)
$trace=Get-Content -LiteralPath (Join-Path $job 'step-1.out') -Raw -Encoding UTF8
$started=@(Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-TaskScheduler/Operational';Id=100} -ErrorAction SilentlyContinue |
 Where-Object {(([xml]$_.ToXml()).Event.EventData.Data | Where-Object Name -eq TaskName).'#text' -eq "\DomainConsoleCSharp-$id"})
$closed='C:\DomainConsoleLab\receipt-disconnected.txt'
@{
  Job=$id; Status=$state.Status; Executions=$started.Count
  BitsStopped=$trace -match 'Остановлена: bits'
  BitsStarted=$trace -match 'bits: работает после восстановления' 
  BITS=(Get-Service BITS).Status.ToString(); Tasks=$tasks.Count
  ActiveTasks=@($tasks|Where-Object State -eq 'Running').Count
  TransportClosed=if(Test-Path $closed){Get-Content $closed -Raw}
} | ConvertTo-Json -Compress
""")
            states[node] = state = json.loads(actual)
            save()
            if state["Status"] != "Completed" or state["Executions"] != 1:
                raise RuntimeError(node + " did not complete exactly one real service restart")
            if state["Tasks"] > 1 or state["ActiveTasks"] or state["BITS"] != "Running" or not state["BitsStopped"] or not state["BitsStarted"]:
                raise RuntimeError(node + " left a running task, a duplicate task, or a stopped service")
            if state["Job"] != checks["packaged_ui"]["Jobs"][node.lower() + ".dc057.test"]:
                raise RuntimeError(node + " job does not match the consent and result in the console")
            receipt_job = (state.get("TransportClosed") or "").strip()
            if receipt_job != (state["Job"] if node == guests[0] else ""):
                raise RuntimeError(node + " transport interruption did not match the single submitted job")
    except Exception:
        try:
            checks["console_history_at_failure"] = json.loads(console.remote(r"""
$p='C:\DomainConsoleLab\package\Data\history.json'
$entries=@()
if(Test-Path $p){
 $entries=@(Get-Content -LiteralPath $p -Encoding UTF8 -Raw|ConvertFrom-Json |
   ForEach-Object {$job=$_;foreach($target in $job.Targets){
      $output=[string]$target.Output
      @{Id=$job.Id;Name=$job.Name;Host=$target.Host;Status=$target.Status;Output=$output.Substring(0,[Math]::Min(900,$output.Length))}
   }})
}
ConvertTo-Json -InputObject $entries -Depth 6 -Compress
"""))
        except Exception as error:
            checks["console_diagnostic_error"] = str(error)
        for node in guests:
            try:
                checks.setdefault("guest_state_at_failure", {})[node] = json.loads(ps(node, r"""
$audit='C:\DomainConsoleLab\receipt-disconnected.txt'
$jobs=@(Get-ScheduledTask -TaskName 'DomainConsoleCSharp-*' -ErrorAction SilentlyContinue)
@{Tasks=@($jobs|Select-Object TaskName,State); FaultFired=(Test-Path $audit); BITS=(Get-Service BITS).Status.ToString()} |
 ConvertTo-Json -Depth 5 -Compress
"""))
            except Exception as error:
                checks.setdefault("guest_diagnostic_errors", {})[node] = str(error)
        save()
        raise
    finally:
        if console.server:
            console.server.shutdown()
    print("PASS: actual same-click mass submission, partial receipt and one restart on each guest.", flush=True)
