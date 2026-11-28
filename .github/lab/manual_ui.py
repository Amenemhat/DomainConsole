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
        ps(node, "Start-Service BITS")
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
    deadline = time.monotonic() + 90
    while time.monotonic() < deadline:
        try:
            if ps(guests[0], "(Get-Command Start-ScheduledTask).CommandType.ToString()") == "Function":
                break
        except Exception:
            pass
        time.sleep(4)
    else:
        raise TimeoutError("One-shot transport fault endpoint did not load")
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
    (Test-Path $p) -and ((Get-Content -Raw $p) -match 'DCLabManualReceipt059')
})
if($matched.Count -ne 1){throw "Expected one job directory, found $($matched.Count)"}
$id=$matched[0].Name
$job=$matched[0].FullName
$state=Get-Content (Join-Path $job 'status.json') -Raw | ConvertFrom-Json
$tasks=@(Get-ScheduledTask -TaskName "DomainConsoleCSharp-$id" -ErrorAction SilentlyContinue)
$marker='C:\DomainConsoleLab\DCLabManualReceipt059.txt'
$closed='C:\DomainConsoleLab\receipt-disconnected.txt'
@{
  Job=$id; Status=$state.Status; Executions=@(Get-Content $marker).Count
  BITS=(Get-Service BITS).Status.ToString(); Tasks=$tasks.Count
  ActiveTasks=@($tasks|Where-Object State -eq 'Running').Count
  TransportClosed=if(Test-Path $closed){Get-Content $closed -Raw}
} | ConvertTo-Json -Compress
""")
            states[node] = state = json.loads(actual)
            save()
            if state["Status"] != "Completed" or state["Executions"] != 1:
                raise RuntimeError(node + " did not complete exactly one real service restart")
            if state["Tasks"] > 1 or state["ActiveTasks"] or state["BITS"] != "Running":
                raise RuntimeError(node + " left a running task, a duplicate task, or a stopped service")
            if state["Job"] != checks["packaged_ui"]["Jobs"][node.lower() + ".dc057.test"]:
                raise RuntimeError(node + " job does not match the consent and result in the console")
            receipt_job = (state.get("TransportClosed") or "").strip()
            if receipt_job != (state["Job"] if node == guests[0] else ""):
                raise RuntimeError(node + " transport interruption did not match the single submitted job")
    finally:
        if console.server:
            console.server.shutdown()
    print("PASS: actual same-click mass submission, partial receipt and one restart on each guest.", flush=True)
