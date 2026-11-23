"""Run the native WTS probe with actual RDP sessions on the disposable Windows member."""
import contextlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time


def exercise(root, ps, host, password, evidence, save):
    source = (root / "UserObservation37.cs").read_text(encoding="utf-8")
    probe = re.search(r'UserProbeScript37="""\n(.*?)\n""";', source, re.S).group(1)
    probe += "\n@{Complete=$dcUsersComplete37;Users=@($dcUsers37);Error=$dcUserError37}|ConvertTo-Json -Depth 8 -Compress"
    client = shutil.which("xfreerdp3") or shutil.which("xfreerdp")
    if not client:
        raise RuntimeError("FreeRDP is not available")

    def read():
        snapshot = json.loads(ps("DCWSUS", probe))
        if not snapshot["Complete"]:
            raise RuntimeError("Native WTS query failed: " + snapshot["Error"])
        return {row["Account"].upper(): row for row in snapshot["Users"]}

    evidence["probe_commit"] = os.environ["DC_PROBE_COMMIT58"]
    evidence["checks"]["pre_rdp_wts"] = list(read())
    save()
    ps("DCAD", f"$p=ConvertTo-SecureString '{password}' -AsPlainText -Force;"
       "New-ADUser -Name 'RdpDomain58' -SamAccountName RdpDomain58 -GivenName Domain -Surname Fixture "
       "-AccountPassword $p -Enabled $true", True)
    ps("DCWSUS", f"$p=ConvertTo-SecureString '{password}' -AsPlainText -Force;"
       "New-LocalUser -Name RdpLocal58 -Password $p|Out-Null;"
       "$g=Get-LocalGroup -SID 'S-1-5-32-555';"
       "Add-LocalGroupMember -Group $g -Member 'DCWSUS\\RdpLocal58','DCLAB\\RdpDomain58';"
       "Set-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server' -Name fDenyTSConnections -Value 0;"
       "Enable-NetFirewallRule -Name RemoteDesktop-UserMode-In-TCP;"
       "Start-Service TermService")

    @contextlib.contextmanager
    def connect(domain, name):
        with (root / (name + "-rdp.log")).open("w") as log:
            job = subprocess.Popen(["xvfb-run", "-a", client, "/v:" + host, "/d:" + domain,
                                    "/u:" + name, "/p:" + password, "/cert:ignore", "/size:1024x768"],
                                   start_new_session=True, stdout=log, stderr=log)
            try:
                yield job
            finally:
                if job.poll() is None:
                    os.killpg(job.pid, 15)
                    job.wait(timeout=10)

    def await_accounts(expected, active_jobs, absent=()):
        until = time.monotonic() + 100
        while time.monotonic() < until:
            for job in active_jobs:
                if job.poll() is not None:
                    raise RuntimeError(f"RDP client exited with {job.returncode}")
            users = read()
            if all(key in users and users[key]["Local"] == is_local and users[key]["Active"]
                   for key, is_local in expected.items()) and all(key not in users for key in absent):
                return users
            time.sleep(3)
        raise TimeoutError("WTS did not report the expected RDP transitions")

    local = "DCWSUS\\RDPLOCAL58"
    domain = "DCLAB\\RDPDOMAIN58"
    with connect("DCWSUS", "RdpLocal58") as local_client:
        await_accounts({local: True}, [local_client])
        with connect("DCLAB", "RdpDomain58") as domain_client:
            snapshot = await_accounts({local: True, domain: False}, [local_client, domain_client])
            evidence["checks"]["concurrent_local_and_domain_rdp"] = {key: snapshot[key] for key in (local, domain)}
            save()
        ps("DCWSUS", "$line=quser.exe RdpDomain58|Select-String 'RdpDomain58';"
           "$match=[regex]::Match($line,'RdpDomain58\\s+(?:[^\\s]+\\s+)?(\\d+)\\s+');"
           "if(!$match.Success){throw 'Could not identify the disconnected RDP session'};"
           "logoff.exe ([int]$match.Groups[1].Value);if($LASTEXITCODE){throw 'Could not close RDP session'}")
        await_accounts({local: True}, [local_client], absent=(domain,))
    evidence["checks"]["rdp_logoff_observed_without_losing_other_session"] = True
    save()
