"""Exercise the released PowerShell bodies against WUA, AD and WSUS in the private lab."""
import concurrent.futures
import json
from pathlib import Path
import re
import subprocess
import time
import uuid

console = Path("_app/DomainConsole-CSharp/Console")
reg = "HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate"
policy = "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate"


def body(file, label):
    match = re.search(r'public const string '+label+r'\s*=\s*"""\r?\n([\s\S]*?)\r?\n""";', (console / file).read_text())
    if not match:
        raise RuntimeError("Missing production body: "+label)
    return match.group(1)


def quote(value):
    return "'" + value.replace("'", "''") + "'"


def exercise(nodes, ps, system, evidence, save, root, gui):
    clients = [n for n in nodes if n.startswith("CLONE")]
    probe = body("Identity58.cs", "IdentityProbe58")
    verify = body("IdentityConfirmation58.cs", "IdentityServerScript58")
    wsus_api = "[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer('localhost',$false,8530);"
    ps("DCWSUS", wsus_api + "$null=$s.CreateComputerTargetGroup('DCLabClients')")
    settings = f"$p='{policy}';New-Item $p -Force|Out-Null;New-Item ($p+'\\AU') -Force|Out-Null;" + """
foreach($key in @('WUServer','WUStatusServer')){New-ItemProperty -Path $p -Name $key -Value 'http://dcwsus.dc057.test:8530' -PropertyType String -Force|Out-Null}
New-ItemProperty -Path $p -Name TargetGroupEnabled -Value 1 -PropertyType DWord -Force|Out-Null
New-ItemProperty -Path $p -Name TargetGroup -Value 'DCLabClients' -PropertyType String -Force|Out-Null
New-ItemProperty -Path ($p+'\\AU') -Name UseWUServer -Value 1 -PropertyType DWord -Force|Out-Null
New-ItemProperty -Path ($p+'\\AU') -Name AUOptions -Value 2 -PropertyType DWord -Force|Out-Null
Restart-Service wuauserv -Force
$session=New-Object -ComObject Microsoft.Update.Session
$searcher=$session.CreateUpdateSearcher();$searcher.ServerSelection=1;$searcher.Online=$true
$scan=$searcher.Search('IsInstalled=0 and IsHidden=0');if([int]$scan.ResultCode -ne 2){throw "Initial managed scan returned $($scan.ResultCode)"}
& "$env:windir\\System32\\wuauclt.exe" /reportnow
"""
    def probe_client(node):
        result = json.loads(ps(node, probe + "\n$dcIdentity58|ConvertTo-Json -Compress"))
        if result.get("Error") or not result.get("Machine") or not result.get("SusClientId"):
            evidence["checks"].setdefault("incomplete",{})[node]=result
            save()
            raise RuntimeError(node + ": incomplete real registry/CIM observation: " + json.dumps(result,ensure_ascii=False))
        uuid.UUID(result["SusClientId"])
        return result
    def configure(node,shared=None):
        marker="Restart-Service wuauserv -Force"
        policy_only,scan=settings.split(marker,1)
        if node==clients[-2]:
            policy_only=policy_only.replace("http://dcwsus.dc057.test:8530","http://dcwsus:8530")
        if shared is None:
            # Reproduce an imaged machine on a pristine OS that may have no SusClientId yet.
            seed=f"$r='{reg}';if(!(Test-Path $r)){{New-Item $r -Force|Out-Null}};if(!(Get-ItemProperty $r -Name SusClientId -ErrorAction SilentlyContinue)){{New-ItemProperty $r -Name SusClientId -Value ([guid]::NewGuid().ToString()) -PropertyType String -Force|Out-Null}};"
            job=policy_only+"Stop-Service wuauserv -Force;"+seed+"Start-Service wuauserv;"+scan
        else:
            # These four clones have never reported separately to this WSUS.
            job=policy_only+"Stop-Service wuauserv -Force;"+f"if(!(Test-Path '{reg}')){{New-Item '{reg}' -Force|Out-Null}};New-ItemProperty -Path '{reg}' -Name SusClientId -Value {quote(shared)} -PropertyType String -Force|Out-Null;Start-Service wuauserv"
        system(node,"configure-wua",job,minutes=22)
    configure(clients[0])
    limit=time.monotonic()+180
    while True:
        try:
            shared=probe_client(clients[0])["SusClientId"]
            break
        except RuntimeError:
            if time.monotonic()>=limit:
                raise
            time.sleep(8)
    def seed_clone(node):
        configure(node,shared)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        list(pool.map(seed_clone,clients[1:]))
    def resources(label, active):
        sample = r"""
$p=@(Get-CimInstance Win32_Process|Where-Object Name -in 'DomainConsole.Agent.exe','wsmprovhost.exe','powershell.exe')
$jobs=@(Get-ScheduledTask|Where-Object TaskName -like 'DomainConsole*')
$store=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$size=if(Test-Path $store){(Get-ChildItem $store -Recurse -File|Measure-Object -Property Length -Sum).Sum}else{0}
@{Agent=@($p|Where-Object Name -eq 'DomainConsole.Agent.exe').Count;WinRM=@($p|Where-Object Name -eq 'wsmprovhost.exe').Count;PowerShell=@($p|Where-Object Name -eq 'powershell.exe').Count;OwnedTasks=$jobs.Count;WorkingSetBytes=($p|Measure-Object -Property WorkingSetSize -Sum).Sum;CPU100ns=[double]($p|Measure-Object -Property KernelModeTime -Sum).Sum+[double]($p|Measure-Object -Property UserModeTime -Sum).Sum;DataBytes=$size;RegistryBackups=@(Get-ChildItem (Join-Path $env:ProgramData 'DomainConsole-Backups/WindowsUpdate-*.reg') -ErrorAction SilentlyContinue).Count;Utc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Compress
"""
        def read(node):
            return node,json.loads(ps(node,sample))
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            evidence["checks"].setdefault("resource_samples",{})[label]=dict(pool.map(read,active))
        save()
    resources("before",clients)
    before = {node: probe_client(node) for node in clients}
    if len({r["Machine"].lower() for r in before.values()}) != len(clients) or any(r["SusClientId"].lower()!=shared.lower() for r in before.values()):
        raise RuntimeError("Did not reproduce a shared ID on distinct devices")
    evidence["checks"]["before"] = before
    query="$t=@($s.GetComputerTargets()|Where-Object FullDomainName -like 'clone*'|Select-Object Id,FullDomainName);ConvertTo-Json -InputObject $t -Compress"
    limit=time.monotonic()+300
    while True:
        initial=json.loads(ps("DCWSUS",wsus_api+query))
        if initial or time.monotonic()>=limit:
            break
        time.sleep(10)
    if len(initial)!=1 or initial[0]["Id"].lower()!=shared.lower():
        raise RuntimeError("Expected exactly one server record hiding five distinct clients: "+str(initial))
    evidence["checks"]["initial_server_records"]=initial
    ous = json.loads(ps("DCAD", "Get-ADComputer -Filter \"Name -like 'CLONE*'\"|Select-Object Name,DistinguishedName|ConvertTo-Json -Compress", True))
    if len(ous)!=len(clients) or len({r["DistinguishedName"].split(",")[1] for r in ous})<2:
        raise RuntimeError("Incorrect AD scope")
    evidence["checks"]["directory"] = ous
    save()
    held = clients[-1]
    tap = "dclab"+str(nodes[held])
    subprocess.run(["ip","link","set",tap,"down"],check=True)
    try:
        try:
            from socket import create_connection
            with create_connection(("10.77.57."+str(nodes[held]),5985),timeout=2):
                raise RuntimeError("Disconnected guest remained reachable")
        except OSError:
            evidence["checks"]["offline"] = held
        gui.start(before)
        gui.wait("read-only.done",900)
        resources("diagnosis",clients[:-1])
        for node,m in evidence["checks"]["resource_samples"]["diagnosis"].items():
            baseline=evidence["checks"]["resource_samples"]["before"][node]
            if m["RegistryBackups"]!=baseline["RegistryBackups"] or m["Agent"]>baseline["Agent"] or m["OwnedTasks"]>baseline["OwnedTasks"]:
                raise RuntimeError("Read-only diagnosis changed the client on "+node)
        gui.mark("allow-consent.done")
        gui.wait("first-consent.done",800)
        resources("during",clients[:-1])
        def changed(active):
            deadline=time.monotonic()+900
            while time.monotonic()<deadline:
                readings={}
                for node in active:
                    try:
                        current=probe_client(node)
                    except RuntimeError:
                        # Windows can briefly have no ID between the guarded removal and registration.
                        continue
                    if current["SusClientId"].lower()!=shared.lower():
                        readings[node]=current
                if len(readings)==len(active):
                    return readings
                time.sleep(15)
            raise TimeoutError("Packaged console did not change registrations on "+", ".join(active))
        evidence["checks"]["four-replacements"]=changed(clients[:-1])
        save()
    finally:
        subprocess.run(["ip","link","set",tap,"up"],check=True)
    if probe_client(held)["SusClientId"].lower()!=shared.lower():
        raise RuntimeError("Offline target was unexpectedly reset")
    gui.mark("fifth-online.done")
    gui.wait("expanded.done", 600)
    changed([held])
    after = {node: probe_client(node) for node in clients}
    ids={r["SusClientId"].lower() for r in after.values()}
    if shared.lower() in ids or len(ids)!=len(clients):
        raise RuntimeError("Repair failed to produce distinct new Windows IDs")
    evidence["checks"]["after"] = after
    save()
    pending = set(clients)
    deadline = time.monotonic()+620
    while pending and time.monotonic()<deadline:
        for node in list(pending):
            current=after[node]
            try:
                target=json.loads(ps("DCWSUS", "$port=8530;$ssl=$false;$identityId58="+quote(current["SusClientId"])+";"+verify))
            except RuntimeError:
                continue
            # A server record must match ID and real FQDN, report after the pre-repair probe and contain the client-side group.
            from datetime import datetime
            def stamp(value):
                return datetime.fromisoformat(value.replace("Z","+00:00"))
            if (target["Id"].lower()==current["SusClientId"].lower() and target["Name"].rstrip(".").lower()==current["Name"].rstrip(".").lower()
                    and stamp(target["ReportedUtc"])>=stamp(before[node]["At"]) and "DCLabClients" in target["Groups"]):
                pending.remove(node)
                evidence["checks"].setdefault("confirmed",{})[node]=target
                save()
        if pending:
            time.sleep(20)
    if pending:
        raise RuntimeError("WSUS has not confirmed fresh registration in 10 minutes: "+", ".join(sorted(pending)))
    evidence["checks"]["packaged-console"]=gui.finish()
    resources("after",clients)
    for node,m in evidence["checks"]["resource_samples"]["after"].items():
        if m["RegistryBackups"]!=evidence["checks"]["resource_samples"]["before"][node]["RegistryBackups"]+1:
            raise RuntimeError("Expected one reset per confirmed client: "+node)
        if m["Agent"]>1:
            raise RuntimeError("Multiple agents remained on "+node)
    save()
    print("Real WSUS confirmed five independent reports and AD devices.",flush=True)
