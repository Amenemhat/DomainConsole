"""Reset one real Windows client through the candidate repair script and read WSUS independently."""
from datetime import datetime
import json
import time
from uuid import UUID

from identity_lab import body, policy, quote, reg


def exercise(ps, system, wait, evidence, save):
    client = "CLONE1"
    server = "DCWSUS"
    group = "DCLabSingleClient"
    url = "http://dcwsus.dc057.test:8530"
    checks = evidence["checks"]
    wsus_api = ("[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');"
                "$wsus=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer('localhost',$false,8530);")
    ps(server, wsus_api + f"$null=$wsus.CreateComputerTargetGroup({quote(group)})")
    probe = body("Identity58.cs", "IdentityProbe58")
    verify = body("IdentityConfirmation58.cs", "IdentityServerScript58")
    repair_body = body("IdentityRepair58.cs", "IdentityRepairBody58")

    def observe_client():
        snapshot = probe + r"""
$backups=Join-Path $env:ProgramData 'DomainConsole-Backups'
$dcIdentity58.BackupCount=@(Get-ChildItem $backups -Filter '*.reg' -ErrorAction SilentlyContinue).Count
$service=Get-CimInstance Win32_Service -Filter "Name='wuauserv'"
$dcIdentity58.Service=$service.State
$dcIdentity58.ServiceProcessId=$service.ProcessId
$dcIdentity58.BootUtc=(Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().ToString('o')
$dcIdentity58|ConvertTo-Json -Compress
"""
        return json.loads(ps(client, snapshot))

    def observe_server(identifier, name):
        prefix = f"$port=8530;$ssl=$false;$identityId58={quote(identifier)};$lookupName58={quote(name)};"
        return json.loads(ps(server, prefix + verify))

    def confirmed(record, identifier, name, since=None):
        if (record["Id"].lower() != identifier.lower()
                or record["Name"].lower().rstrip(".") != name.lower().rstrip(".")
                or group not in record["Groups"] or not record["ReportedUtc"]):
            return False
        reported = datetime.fromisoformat(record["ReportedUtc"])
        return reported.year > 2000 and (since is None or reported >= since)

    settings = f"$p={quote(policy)};$r={quote(reg)};$wsusUrl={quote(url)};$group={quote(group)};\n" + r"""
New-Item -Path $p -Force|Out-Null
New-Item -Path ($p+'\AU') -Force|Out-Null
foreach($key in 'WUServer','WUStatusServer'){
 New-ItemProperty -Path $p -Name $key -Value $wsusUrl -PropertyType String -Force|Out-Null
}
New-ItemProperty -Path $p -Name TargetGroupEnabled -Value 1 -PropertyType DWord -Force|Out-Null
New-ItemProperty -Path $p -Name TargetGroup -Value $group -PropertyType String -Force|Out-Null
New-ItemProperty -Path ($p+'\AU') -Name UseWUServer -Value 1 -PropertyType DWord -Force|Out-Null
New-ItemProperty -Path ($p+'\AU') -Name AUOptions -Value 2 -PropertyType DWord -Force|Out-Null
Stop-Service wuauserv
if(!(Test-Path $r)){New-Item $r -Force|Out-Null}
$seeded=![string](Get-ItemProperty $r).SusClientId
if($seeded){
 # Seed only the initial fixture on a fresh image. The replacement ID must come from WUA.
 New-ItemProperty $r -Name SusClientId -Value ([guid]::NewGuid().ToString()) -PropertyType String|Out-Null
}
$seeded|Set-Content C:\DomainConsoleLab\single-baseline-seeded.txt
Start-Service wuauserv
$session=New-Object -ComObject Microsoft.Update.Session
$searcher=$session.CreateUpdateSearcher()
$searcher.ServerSelection=1
$searcher.Online=$true
$result=[int]$searcher.Search('IsInstalled=0 and IsHidden=0').ResultCode
$result|Set-Content C:\DomainConsoleLab\single-baseline-search.txt
if($result -ne 2){throw ('Baseline managed search failed: '+$result)}
& "$env:windir\System32\wuauclt.exe" /reportnow
"""
    checks["stage"] = "baseline"
    save()
    system(client, "single-baseline", settings, minutes=5)
    checks["initial_id_seeded"] = ps(client, r"Get-Content C:\DomainConsoleLab\single-baseline-seeded.txt") == "True"
    deadline = time.monotonic() + 240
    while True:
        original = observe_client()
        old_id = original["SusClientId"]
        name = original["Name"]
        checks["client_before"] = original
        if old_id and name:
            UUID(old_id)
            baseline = observe_server(old_id, name)
            checks["server_before"] = baseline
            if confirmed(baseline, old_id, name):
                break
        save()
        if time.monotonic() >= deadline:
            raise RuntimeError("Baseline did not establish a real client ID and current WSUS record")
        time.sleep(12)
    print(f"Baseline confirmed: {name}, {old_id}", flush=True)

    expected = {"Id": old_id, "Machine": original["Machine"], "Installation": original["InstallationId"],
                "Name": name, "Server": original["Server"], "StatusServer": original["StatusServer"]}
    prefix = "".join(f"$dcExpected{key}58={quote(value)};" for key, value in expected.items())
    started = datetime.fromisoformat(ps(client, "[datetime]::UtcNow.ToString('o')"))
    checks["repair_started_utc"] = started.isoformat()
    checks["stage"] = "reset one client"
    save()
    script = "& {\n" + prefix + repair_body + "\n} | Out-File -Encoding utf8 -FilePath C:\\DomainConsoleLab\\single-repair.out"
    candidate_failure = None
    try:
        system(client, "single-repair", script, minutes=5)
    except RuntimeError as error:
        checks["repair_error"] = str(error)
    checks["client_after"] = observe_client()
    checks["repair_stdout"] = ps(client, "if(Test-Path C:\\DomainConsoleLab\\single-repair.out){Get-Content -Raw -Encoding UTF8 C:\\DomainConsoleLab\\single-repair.out}")
    checks["stage"] = "observe local ID and WSUS"
    save()

    try:
        deadline = time.monotonic() + 10 * 60
        while True:
            observed = observe_client()
            checks["client_after"] = observed
            identifier = observed["SusClientId"]
            if identifier and UUID(identifier) != UUID(old_id):
                record = observe_server(identifier, name)
                checks["server_after"] = record
                if confirmed(record, identifier, name, started):
                    checks["stage"] = "confirmed"
                    break
            if "repair_error" in checks and not identifier:
                raise RuntimeError("WUA did not create a replacement local ID during the one reset")
            save()
            if time.monotonic() >= deadline:
                raise RuntimeError("WSUS did not confirm the new ID, expected name, group and fresh report")
            time.sleep(15)
        if checks.get("repair_error"):
            raise RuntimeError("WSUS observed a new ID but the candidate command failed")
        for key in ("Machine", "InstallationId", "Server", "StatusServer", "UseWUServer"):
            if observed[key] != original[key]:
                raise RuntimeError(f"Unexpected change to {key}")
        if observed["BackupCount"] != original["BackupCount"] + 1:
            raise RuntimeError("The one-client reset did not produce exactly one registry backup")
        print(f"PASS: one client changed ID to {identifier}; WSUS recorded a fresh report.", flush=True)
    except RuntimeError as error:
        candidate_failure = error
    finally:
        checks["server_old_id_after"] = observe_server(old_id, name)
        try:
            checks["client_trace"] = ps(client, r"""
$log='C:\DomainConsoleLab\single-WindowsUpdate.log'
Get-WindowsUpdateLog -LogPath $log | Out-Null
Get-Content $log -Tail 2400 | Select-String -Pattern 'RegisterComputer|GetCookie|SyncUpdates|ReportingWebService|DomainConsole.Identity58' | Select-Object -Last 60 | ForEach-Object {$_.Line}
""")
        except Exception as error:
            checks["trace_error"] = str(error)[:800]
        save()

    if candidate_failure is None:
        return
    checks["candidate_failed"] = str(candidate_failure)
    pre_reboot = observe_client()
    checks["client_before_reboot"] = pre_reboot
    save()
    if pre_reboot["SusClientId"] or pre_reboot["BackupCount"] != original["BackupCount"] + 1:
        raise candidate_failure

    # Diagnostic only: reboot one disposable guest once without deleting any values again.
    control = checks["reboot_control"] = {"stage": "reboot requested"}
    save()
    try:
        ps(client, r"""
$computer=Get-CimInstance Win32_ComputerSystem
if($env:COMPUTERNAME -ne 'CLONE1' -or !$computer.PartOfDomain -or $computer.Domain -ne 'dc057.test'){
 throw 'Disposable CLONE1 required'
}
shutdown.exe /r /t 5 /c 'Single-client WUA process diagnostic'
if($LASTEXITCODE){throw "Lab reboot failed: $LASTEXITCODE"}
""")
        time.sleep(15)
        wait(client, minutes=7)
        after_boot = observe_client()
        control["after_reboot"] = after_boot
        if datetime.fromisoformat(after_boot["BootUtc"]) <= datetime.fromisoformat(pre_reboot["BootUtc"]):
            raise RuntimeError("A new Windows boot was not observed")
        control["stage"] = "one managed search"
        save()
        system(client, "single-reboot-search", r"""
$session=New-Object -ComObject Microsoft.Update.Session
$session.ClientApplicationID='DomainConsole.SingleRebootDiagnostic'
$searcher=$session.CreateUpdateSearcher()
$searcher.ServerSelection=1
$searcher.Online=$true
$result=[int]$searcher.Search('IsInstalled=0 and IsHidden=0').ResultCode
$result|Set-Content C:\DomainConsoleLab\single-reboot-search-code.txt
if($result -ne 2){throw ('Managed diagnostic search failed: '+$result)}
$path='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate'
$newId=[string](Get-ItemProperty $path).SusClientId
for($second=0;!$newId -and $second -lt 30;$second++){
 Start-Sleep -Seconds 1
 $newId=[string](Get-ItemProperty $path).SusClientId
}
if($newId){& "$env:windir\System32\wuauclt.exe" /reportnow}
""", minutes=5)
        control["search_code"] = ps(client, r"Get-Content C:\DomainConsoleLab\single-reboot-search-code.txt")
        deadline = time.monotonic() + 180
        while True:
            observed = observe_client()
            control["after_search"] = observed
            identifier = observed["SusClientId"]
            if identifier and UUID(identifier) != UUID(old_id):
                record = observe_server(identifier, name)
                control["server"] = record
                if confirmed(record, identifier, name, datetime.fromisoformat(after_boot["BootUtc"])):
                    if observed["BackupCount"] != pre_reboot["BackupCount"]:
                        raise RuntimeError("The diagnostic unexpectedly performed a second reset")
                    control["stage"] = "confirmed after reboot"
                    print(f"DIAGNOSTIC PASS: {name} registered {identifier} after one reboot and no second reset.", flush=True)
                    break
            save()
            if time.monotonic() >= deadline:
                raise RuntimeError("WSUS did not confirm the replacement ID after the controlled reboot")
            time.sleep(15)
    except Exception as error:
        control["error"] = str(error)[:1000]
    finally:
        save()
    raise candidate_failure
