"""Exercise the released PowerShell bodies against WUA, AD and WSUS in the private lab."""
import concurrent.futures
import json
from pathlib import Path
import re
import subprocess
import time
import uuid

console = Path("DomainConsole-CSharp/Console")
reg = "HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate"
policy = "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate"


def body(file, label):
    match = re.search(r'public const string '+label+r'\s*=\s*"""\r?\n([\s\S]*?)\r?\n""";', (console / file).read_text())
    if not match:
        raise RuntimeError("Missing production body: "+label)
    return match.group(1)


def quote(value):
    return "'" + value.replace("'", "''") + "'"


def exercise(nodes, ps, system, evidence, save, root):
    clients = [n for n in nodes if n.startswith("CLONE")]
    probe = body("Identity58.cs", "IdentityProbe58")
    repair = body("IdentityRepair58.cs", "IdentityRepairBody58")
    verify = body("IdentityConfirmation58.cs", "IdentityServerScript58")
    wsus_api = "[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer('localhost',$false,8530);"
    ps("DCWSUS", wsus_api + "$null=$s.CreateComputerTargetGroup('DCLabClients')")
    settings = f"$p='{policy}';New-Item $p -Force|Out-Null;New-Item ($p+'\\AU') -Force|Out-Null;" + """
foreach($key in @('WUServer','WUStatusServer')){New-ItemProperty $p $key 'http://dcwsus.dc057.test:8530' -PropertyType String -Force|Out-Null}
New-ItemProperty $p TargetGroupEnabled 1 -PropertyType DWord -Force|Out-Null
New-ItemProperty $p TargetGroup 'DCLabClients' -PropertyType String -Force|Out-Null
New-ItemProperty ($p+'\\AU') UseWUServer 1 -PropertyType DWord -Force|Out-Null
New-ItemProperty ($p+'\\AU') AUOptions 2 -PropertyType DWord -Force|Out-Null
Restart-Service wuauserv -Force
$session=New-Object -ComObject Microsoft.Update.Session
$searcher=$session.CreateUpdateSearcher();$searcher.ServerSelection=1;$searcher.Online=$true
$scan=$searcher.Search('IsInstalled=0 and IsHidden=0');if([int]$scan.ResultCode -ne 2){throw "Initial managed scan returned $($scan.ResultCode)"}
& "$env:windir\\System32\\wuauclt.exe" /reportnow
"""
    def probe_client(node):
        result = json.loads(ps(node, probe + "\n$dcIdentity58|ConvertTo-Json -Compress"))
        if result.get("Error") or not result.get("Machine") or not result.get("SusClientId"):
            raise RuntimeError(node + ": incomplete real registry/CIM observation: " + str(result.get("Error")))
        uuid.UUID(result["SusClientId"])
        return result
    def configure(node):
        system(node, "configure-wua", settings, minutes=22)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        list(pool.map(configure, clients))
    originals = {node: probe_client(node) for node in clients}
    shared = originals[clients[0]]["SusClientId"]
    # Deliberately reproduce the reported cloning defect only inside these ephemeral VMs.
    for node in clients[1:]:
        ps(node, f"Stop-Service wuauserv -Force;Set-ItemProperty '{reg}' SusClientId {quote(shared)};Start-Service wuauserv")
    before = {node: probe_client(node) for node in clients}
    if len({r["Machine"].lower() for r in before.values()}) != len(clients) or any(r["SusClientId"].lower()!=shared.lower() for r in before.values()):
        raise RuntimeError("Did not reproduce a shared ID on distinct devices")
    evidence["checks"]["before"] = before
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
        def fix(node):
            old = before[node]
            prefix = "$dcExpectedId58="+quote(shared)+";$dcExpectedMachine58="+quote(old["Machine"])+";$dcExpectedName58="+quote(old["Name"])+";$dcExpectedServer58="+quote(old["Server"])+";"
            system(node, "identity-repair", prefix+repair, minutes=25)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            list(pool.map(fix, clients[:-1]))
    finally:
        subprocess.run(["ip","link","set",tap,"up"],check=True)
    if probe_client(held)["SusClientId"].lower()!=shared.lower():
        raise RuntimeError("Offline target was unexpectedly reset")
    fix(held)
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
    print("Real WSUS confirmed five independent reports and AD devices.",flush=True)
