"""Drive real AD and WinRM interruptions on the isolated lab network."""
from contextlib import contextmanager
import json
import signal
import subprocess

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
    def stage(name, action):
        check = {"name": name, "completed": False}
        evidence["checks"].setdefault("packaged_steps", []).append(check)
        save()
        print("Packaged user recovery: " + name, flush=True)
        result = action()
        check["completed"] = True
        save()
        return result

    def bounded_ps(node, script, domain=False):
        def timed_out(*_):
            raise TimeoutError("Lab control WinRM did not reply on " + node)

        previous = signal.signal(signal.SIGALRM, timed_out)
        signal.setitimer(signal.ITIMER_REAL, 105)
        try:
            return ps(node, script, domain)
        finally:
            signal.setitimer(signal.ITIMER_REAL, 0)
            signal.signal(signal.SIGALRM, previous)

    def bridge_rules(directory, winrm):
        # Only stop guest-to-guest traffic entering through the console guest's tap.
        # The runner's control sessions originate on the bridge itself.
        rules = ["flush chain bridge dc_user_recovery outage"]
        if directory:
            rules += [
                'add rule bridge dc_user_recovery outage iifname "dclab21" ip daddr 10.77.57.11 tcp dport { 389, 636, 3268, 3269 } counter drop',
                'add rule bridge dc_user_recovery outage iifname "dclab21" ip daddr 10.77.57.11 udp dport 389 counter drop',
            ]
        if winrm:
            rules.append(
                'add rule bridge dc_user_recovery outage iifname "dclab21" ip daddr 10.77.57.10 tcp dport 5985 counter drop')
        subprocess.run(["nft", "-f", "-"], input="\n".join(rules) + "\n", text=True, timeout=12, check=True)

    @contextmanager
    def private_outage_chain():
        subprocess.run(["nft", "-f", "-"], input=(
            "add table bridge dc_user_recovery\n"
            "add chain bridge dc_user_recovery outage { type filter hook forward priority -20; policy accept; }\n"
        ), text=True, timeout=12, check=True)
        try:
            yield
        finally:
            subprocess.run(["nft", "delete", "table", "bridge", "dc_user_recovery"], timeout=12, check=True)

    console = PackagedConsole(root, bounded_ps, password, host="CLONE1")
    try:
        stage("install and launch the packaged UI",
              lambda: console.start({"Host": "dcwsus.dc057.test", "Account": expected}, script="user-recovery.ps1"))
        stage("observe the packaged window", lambda: console.wait("window-ready.done", 145))
        with private_outage_chain():
            stage("block directory and client access", lambda: bridge_rules(directory=True, winrm=True))
            console.mark("start-outages.done")
            stage("WinRM unavailable before a name is saved", lambda: console.wait("winrm-without-history.done", 150))
            bridge_rules(directory=True, winrm=False)
            console.mark("test-ad-loss.done")
            stage("WTS works while LDAP is blocked", lambda: console.wait("ad-unavailable.done", 155))
            bridge_rules(directory=False, winrm=False)
            console.mark("restore-directory.done")
            stage("resolve the name after directory recovery", lambda: console.wait("directory-recovered.done", 250))
            bridge_rules(directory=False, winrm=True)
            console.mark("test-winrm-with-history.done")
            stage("keep the confirmed name and time during WinRM loss", lambda: console.wait("winrm-name-kept.done", 145))
            stage("restore the confirmed name and time after console restart", lambda: console.wait("winrm-with-history.done", 115))
            bridge_rules(directory=False, winrm=False)
            console.mark("restore-winrm.done")
            stage("read a new observation after WinRM recovery", lambda: console.wait("gui-evidence.json", 150))
        evidence["checks"]["packaged_ui"] = json.loads(console.remote(
            r"Get-Content -LiteralPath C:\DomainConsoleLab\gui-evidence.json -Encoding UTF8 -Raw"))
        save()
        tasks = json.loads(bounded_ps("DCWSUS", r"""
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
