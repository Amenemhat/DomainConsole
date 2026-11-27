"""Exercise the released PowerShell bodies against WUA, AD and WSUS in the private lab."""
import concurrent.futures
from datetime import datetime
import json
import os
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


def verify_windows_firmware(nodes, ps, root, evidence, save):
    # These guests run under SeaBIOS. Windows Server 2022 cannot enumerate a type-1 table
    # advertised solely through the 64-bit entry point on affected QEMU versions (QEMU #2008).
    import os
    from __main__ import processes, wait
    if os.environ.get("GITHUB_ACTIONS")!="true" or root.name!="dc-wsus-lab":
        raise RuntimeError("Disposable lab firmware only")
    def verify(node):
        proc=processes[node]
        args=list(proc.args)
        if not Path(args[0]).name.startswith("qemu-system-") or args[args.index("-name")+1]!=node:
            raise RuntimeError("Unexpected lab VM process: "+node)
        expected=uuid.UUID(args[args.index("-uuid")+1])
        read="([string](Get-CimInstance Win32_ComputerSystemProduct).UUID).Trim()"
        observed=ps(node,read)
        if not observed:
            ps(node,"shutdown.exe /s /t 2 /c 'Use Windows-compatible SMBIOS firmware entry point'")
            proc.wait(timeout=150)
            machine=args.index("-machine")+1
            args[machine]=args[machine]+",smbios-entry-point-type=32"
            with (root/(node+".log")).open("a") as log:
                processes[node]=subprocess.Popen(args,stdout=log,stderr=log)
            wait(node,minutes=12)
            observed=ps(node,read)
        if not observed or uuid.UUID(observed)!=expected:
            raise RuntimeError(f"{node}: configured UUID {expected}, Windows CIM UUID {observed!r}")
        return node,observed
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        readings=dict(pool.map(verify,[n for n in nodes if n.startswith("CLONE")]))
    if len(set(v.lower() for v in readings.values()))!=5:
        raise RuntimeError("Expected five different firmware-backed Windows CIM identifiers")
    evidence["checks"]["firmware_uuids"]=readings
    save()


def exercise(nodes, ps, system, evidence, save, root, gui):
    clients = [n for n in nodes if n.startswith("CLONE")]
    pair = len(clients) == 2
    own_service = pair or os.environ.get("DC_IDENTITY_OWN_WUA_PROCESS") == "1"
    if not pair:
        verify_windows_firmware(nodes,ps,root,evidence,save)
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
        boot = "\n$dcIdentity58.BootUtc=(Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().ToString('o')" if own_service else ""
        result = json.loads(ps(node, probe + boot + "\n$dcIdentity58|ConvertTo-Json -Compress"))
        if result.get("Error") or not result.get("Machine") or not result.get("SusClientId"):
            incomplete=evidence["checks"].setdefault("incomplete",{})
            if node not in incomplete:
                # Diagnostic reads only. Compare the exact product observation with both registry views.
                audit=r"""
$sys=Get-CimInstance Win32_ComputerSystemProduct
$values=@{}
foreach($v in 'Registry64','Registry32'){
 $h=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]$v)
 try{$k=$h.OpenSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate');if($k){try{$values[$v]=[string]$k.GetValue('SusClientId')}finally{$k.Dispose()}}}finally{$h.Dispose()}
}
@{Uuid=[string]$sys.UUID;Product=[string]$sys.Name;Serial=[string]$sys.IdentifyingNumber;Views=$values;Name=$env:COMPUTERNAME;Service=(Get-Service wuauserv).Status.ToString();Is64Bit=[Environment]::Is64BitProcess}|ConvertTo-Json -Depth 3 -Compress
"""
                incomplete[node]={"product":result,"windows":json.loads(ps(node,audit))}
                save()
            raise RuntimeError(node + ": incomplete real registry/CIM observation: " + json.dumps(result,ensure_ascii=False))
        uuid.UUID(result["SusClientId"])
        return result
    def configure(node,shared=None):
        marker="Restart-Service wuauserv -Force"
        policy_only,scan=settings.split(marker,1)
        if not pair and node==clients[-2]:
            policy_only=policy_only.replace("http://dcwsus.dc057.test:8530","http://dcwsus:8530")
        if own_service:
            policy_only+=r"""Stop-Service wuauserv
(Get-Service wuauserv).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))
& sc.exe config wuauserv type= own | Out-Null
if($LASTEXITCODE){throw 'Could not isolate the disposable client service'}
"""
        if shared is None:
            # Reproduce an imaged machine on a pristine OS that may have no SusClientId yet.
            seed=f"$r='{reg}';if(!(Test-Path $r)){{New-Item $r -Force|Out-Null}};if(!(Get-ItemProperty $r -Name SusClientId -ErrorAction SilentlyContinue)){{New-ItemProperty $r -Name SusClientId -Value ([guid]::NewGuid().ToString()) -PropertyType String -Force|Out-Null}};"
            job=policy_only+"Stop-Service wuauserv -Force;"+seed+"Start-Service wuauserv;"+scan
        else:
            # Initialize each native WUA/validation record before reproducing an old ID.
            # Suppress unsolicited reports while preparing the fixture; the test operator later removes this isolated rule.
            block="New-NetFirewallRule -Name DCLabPrepareClone -DisplayName 'Isolated clone preparation' -Direction Outbound -RemoteAddress 10.77.57.10 -Protocol TCP -RemotePort 8530 -Action Block|Out-Null;"
            offline="try{$w=New-Object -ComObject Microsoft.Update.Session;$r=$w.CreateUpdateSearcher();$r.Online=$false;$null=$r.Search('IsInstalled=0')}catch{$_.Exception.Message|Set-Content C:\\DomainConsoleLab\\seed-offline-search.txt};"
            job=block+policy_only+"Restart-Service wuauserv -Force;"+offline

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
    if pair:
        installation = probe_client(clients[0])["InstallationId"]
        uuid.UUID(installation)
        ps(clients[1], "if($env:COMPUTERNAME -ne 'CLONE2' -or (Get-CimInstance Win32_ComputerSystem).Domain -ne 'dc057.test'){throw 'Disposable domain clone required'};"
           + "Set-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Cryptography' -Name MachineGuid -Value " + quote(installation))
        nics = {node: ps(node, r"""
$active=@(Get-NetAdapter -Physical -ErrorAction Stop|Where-Object Status -eq 'Up')
if($active.Count -ne 1){throw 'Exactly one isolated adapter is required'}
$active[0].MacAddress
""") for node in clients}
        if len(set(nics.values())) != len(clients) or not all(nics.values()):
            raise RuntimeError("The clone pair must have separate physical adapter addresses")
        evidence["checks"]["pair_adapters"] = nics
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
    shared=probe_client(clients[0])["SusClientId"]
    def write_cloned_id(node):
        # Only fixture preparation, before the console and before consent. Preserve each device's own validation bytes.
        ps(node,f"$p='{reg}';if(!(Test-Path $p)){{New-Item $p -Force|Out-Null}};New-ItemProperty $p -Name SusClientId -PropertyType String -Value {quote(shared)} -Force|Out-Null")
    def read_all():
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            return dict(zip(clients,pool.map(probe_client,clients)))
    for attempt in range(2):
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            list(pool.map(write_cloned_id,clients[1:]))
        before=read_all()
        time.sleep(15)
        stable=read_all()
        evidence["checks"]["before"]=stable
        save()
        hardware_count = len({r["Machine"].lower() for r in stable.values()})
        valid_hardware = hardware_count == len(clients) if not pair else hardware_count == 1 and len({r["InstallationId"] for r in stable.values()}) == 1
        if valid_hardware and all(uuid.UUID(r["SusClientId"])==uuid.UUID(shared) for r in (*before.values(),*stable.values())):
            before=stable
            break
        if attempt:
            raise RuntimeError("Shared fixture IDs were not stable: "+str({n:r["SusClientId"] for n,r in stable.items()}))
    resources("before",clients)
    query="$t=@($s.GetComputerTargets()|Where-Object FullDomainName -like 'clone*'|ForEach-Object {[pscustomobject]@{Id=[string]$_.Id;FullDomainName=[string]$_.FullDomainName}});ConvertTo-Json -InputObject $t -Compress"
    limit=time.monotonic()+300
    while True:
        initial=json.loads(ps("DCWSUS",wsus_api+query))
        if initial or time.monotonic()>=limit:
            break
        time.sleep(10)
    evidence["checks"]["initial_server_records"]=initial
    save()
    if len(initial)!=1 or uuid.UUID(initial[0]["Id"])!=uuid.UUID(shared):
        raise RuntimeError("Expected exactly one WSUS record for the shared client ID: "+str(initial))
    for node in clients[1:]:
        ps(node,"Remove-NetFirewallRule -Name DCLabPrepareClone;if(Get-NetFirewallRule -Name DCLabPrepareClone -ErrorAction SilentlyContinue){throw 'Establishment firewall rule still present'}")
    evidence["checks"]["fixture_network_restrictions_removed"]=True
    save()
    ous = json.loads(ps("DCAD", "Get-ADComputer -Filter \"Name -like 'CLONE*'\"|Select-Object Name,DistinguishedName|ConvertTo-Json -Compress", True))
    if len(ous)!=len(clients) or len({r["DistinguishedName"].split(",")[1] for r in ous})<2:
        raise RuntimeError("Incorrect AD scope")
    evidence["checks"]["directory"] = ous
    save()
    def capture_failed_repairs(active):
        # Preserve the actual executor state and WUA errors before the disposable guests are stopped.
        summary=json.loads(gui.diagnostics())
        evidence["checks"]["repair-progress"]=summary
        attempts={item["Host"].split(".",1)[0].upper():item.get("JobId","") for item in summary["Repair"]}
        diagnostics=evidence["checks"].setdefault("client-repair-diagnostics",{})
        for node in active:
            job_id=attempts.get(node,"")
            try:
                if job_id:
                    job_id=str(uuid.UUID(job_id))
                script=r"""
$root=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$files=@{}
if($job){
 $dir=Join-Path $root $job
 foreach($name in 'status.json','step-1.out','step-1.err'){
  $file=Join-Path $dir $name
  if(Test-Path -LiteralPath $file){
   try{$text=Get-Content -Raw -Encoding UTF8 -LiteralPath $file -ErrorAction Stop;$files[$name]=if($text){$text.Substring([Math]::Max(0,$text.Length-6000))}else{''}}
   catch{$files[$name]=$_.Exception.Message}
  }
 }
}
$events=@(Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-WindowsUpdateClient/Operational'; StartTime=(Get-Date).AddMinutes(-25)} -MaxEvents 20 -ErrorAction SilentlyContinue|Select-Object Id,TimeCreated,Message)
$reg='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate'
@{At=[DateTime]::UtcNow.ToString('o');Files=$files;Events=$events;SusClientId=[string](Get-ItemProperty $reg -ErrorAction SilentlyContinue).SusClientId;Service=(Get-Service wuauserv).Status.ToString()}|ConvertTo-Json -Depth 5 -Compress
"""
                diagnostics[node]=json.loads(ps(node,"$job="+quote(job_id)+";"+script))
            except Exception as exc:
                diagnostics[node]={"error":str(exc)[:1600]}
            save()
        try:
            iis=r"""
$root=Join-Path $env:SystemDrive 'inetpub\logs\LogFiles'
$entries=@(
 Get-ChildItem $root -Filter *.log -Recurse -ErrorAction SilentlyContinue|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 5|ForEach-Object {
  Get-Content -LiteralPath $_.FullName -Tail 1500|Where-Object {$_ -like '#Fields:*' -or $_ -match '/(?:ClientWebService|SimpleAuthWebService|ReportingWebService)/'}|Select-Object -Last 50
 }
)
ConvertTo-Json -InputObject $entries -Compress
"""
            evidence["checks"]["wsus-registration-http"]=json.loads(ps("DCWSUS",iis) or "[]")
        except Exception as exc:
            evidence["checks"]["wsus-registration-http"]={"error":str(exc)[:1600]}
        save()

    def compare_wua_restart(active):
        """Compare a service restart with an OS restart on a failed disposable replica."""
        diagnostics=evidence["checks"].get("client-repair-diagnostics",{})
        for node in active:
            status=diagnostics.get(node,{}).get("Files",{}).get("status.json","")
            try:
                failure=json.loads(status).get("Error","") if status else ""
            except ValueError:
                continue
            if "WUA не подтвердил новый SusClientId" in failure:
                break
        else:
            return

        from __main__ import wait
        before=diagnostics[node].get("SusClientId","")
        expected=evidence["checks"]["before"][node]
        if before and before.lower()!=shared.lower():
            return
        reset=f"$r={quote(reg)};$old={quote(shared)};$p={quote(policy)};"
        reset+=(
            "$id=[string](Get-ItemProperty $r).SusClientId;"
            "if($env:COMPUTERNAME -ne "+quote(node)+
            " -or ($id -and $id -ne $old)"
            " -or (Get-ItemProperty $p).WUServer -ne "+quote(expected["Server"])+
            "){throw 'Unexpected diagnostic target or identity'};"
            "Stop-Service wuauserv -ErrorAction Stop;"
            "(Get-Service wuauserv).WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped,[TimeSpan]::FromSeconds(30));"
            "foreach($name in 'SusClientId','SusClientIdValidation'){if((Get-ItemProperty $r).PSObject.Properties.Name -contains $name){Remove-ItemProperty $r -Name $name -ErrorAction Stop}};"
        )
        start="Start-Service wuauserv -ErrorAction Stop;(Get-Service wuauserv).WaitForStatus([ServiceProcess.ServiceControllerStatus]::Running,[TimeSpan]::FromSeconds(30));"
        scan=r"""
$session=New-Object -ComObject Microsoft.Update.Session
$session.ClientApplicationID='DCLab.IdentityDiagnostic'
$search=$session.CreateUpdateSearcher();$search.ServerSelection=1;$search.Online=$true
$rc=[int]$search.Search('IsInstalled=0 and IsHidden=0').ResultCode
& "$env:windir\System32\wuauclt.exe" /reportnow
"""

        for mode in ("warm","cold"):
            key=mode+"-wua-diagnostic"
            result={"node":node,"accepted_application_check":False,"before":before}
            evidence["checks"][key]=result
            save()
            try:
                if mode=="cold":
                    ps(node,reset+"shutdown.exe /r /t 3;exit $LASTEXITCODE")
                    time.sleep(18)
                    wait(node,minutes=7)
                scan_file=rf"C:\DomainConsoleLab\{mode}-scan-result.txt"
                # Both paths run the same managed scan with no preceding asynchronous detect request.
                preparation=(reset if mode=="warm" else "")+start
                system(node,mode+"-managed-scan",preparation+scan+"\n$rc|Set-Content "+quote(scan_file),minutes=4)
                result["search_result"]=ps(node,"Get-Content "+quote(scan_file))
                until=time.monotonic()+30
                while True:
                    result["after"]=json.loads(ps(node,probe+"\n$dcIdentity58|ConvertTo-Json -Compress"))
                    new_id=result["after"].get("SusClientId","")
                    if new_id and new_id.lower()!=shared.lower():
                        break
                    if time.monotonic()>=until:
                        break
                    time.sleep(5)
                save()
                if not new_id or new_id.lower()==shared.lower():
                    before=new_id
                    continue

                name=result["after"]["Name"]
                lookup="$port=8530;$ssl=$false;$identityId58="+quote(new_id)+";$lookupName58="+quote(name)+";"
                until=time.monotonic()+180
                while True:
                    result["server"]=json.loads(ps("DCWSUS",lookup+verify))
                    confirmed=result["server"]["Id"].lower()==new_id.lower() and result["server"]["Name"].rstrip(".").lower()==name.rstrip(".").lower()
                    result["server_target_found"]=confirmed
                    if confirmed or time.monotonic()>=until:
                        break
                    time.sleep(10)
                return
            except Exception as exc:
                result["error"]=str(exc)[:1600]
                return
            finally:
                save()

    def first_consent(active):
        gui.start(before)
        try:
            gui.wait("read-only.done",900)
        except (TimeoutError, RuntimeError):
            evidence["checks"]["read-only-survey-failure"]=json.loads(gui.diagnostics())
            save()
            raise
        evidence["checks"]["read-only-survey"]=json.loads(gui.diagnostics())
        save()
        resources("diagnosis",active)
        for node,m in evidence["checks"]["resource_samples"]["diagnosis"].items():
            baseline=evidence["checks"]["resource_samples"]["before"][node]
            if m["RegistryBackups"]!=baseline["RegistryBackups"] or m["Agent"]>baseline["Agent"] or m["OwnedTasks"]>baseline["OwnedTasks"]:
                raise RuntimeError("Read-only diagnosis changed the client on "+node)
        gui.mark("allow-consent.done")
        gui.wait("first-consent.done",800)
        evidence["checks"]["initial_consent"]=gui.remote("Get-Content C:\\DomainConsoleLab\\first-consent.done")
        save()
        resources("during",active)

    def changed(active):
        deadline=time.monotonic()+900
        last_diag=0
        while time.monotonic()<deadline:
            readings={}
            for node in active:
                try:
                    current=probe_client(node)
                except RuntimeError:
                    # Windows can briefly have no ID between removal and registration.
                    continue
                if current["SusClientId"].lower()!=shared.lower():
                    readings[node]=current
            if len(readings)==len(active):
                return readings
            if time.monotonic()-last_diag>60:
                evidence["checks"]["repair-progress"]=json.loads(gui.diagnostics())
                save()
                last_diag=time.monotonic()
            time.sleep(15)
        capture_failed_repairs(active)
        if not own_service:
            compare_wua_restart(active)
        raise TimeoutError("Packaged console did not change registrations on "+", ".join(active)+" · "+gui.diagnostics()[:3300])

    if pair:
        first_consent(clients)
        evidence["checks"]["pair_replacements"]=changed(clients)
        save()
    else:
        held = clients[-1]
        tap = "dclab" + str(nodes[held])
        subprocess.run(["ip", "link", "set", tap, "down"], check=True)
        try:
            from socket import create_connection
            try:
                with create_connection(("10.77.57." + str(nodes[held]), 5985), timeout=2):
                    raise RuntimeError("Disconnected guest remained reachable")
            except OSError:
                evidence["checks"]["offline"] = held
            first_consent(clients[:-1])
            evidence["checks"]["four-replacements"] = changed(clients[:-1])
            save()
        finally:
            subprocess.run(["ip", "link", "set", tap, "up"], check=True)
        if probe_client(held)["SusClientId"].lower() != shared.lower():
            raise RuntimeError("Offline target was unexpectedly reset")
        gui.mark("fifth-online.done")
        gui.wait("expanded.done", 600)
        evidence["checks"]["consent-five"] = gui.remote("Get-Content C:\\DomainConsoleLab\\expanded.done")
        save()
        changed([held])
    after = {node: probe_client(node) for node in clients}
    ids={r["SusClientId"].lower() for r in after.values()}
    if shared.lower() in ids or len(ids)!=len(clients):
        raise RuntimeError("Repair failed to produce distinct new Windows IDs")
    if own_service and any(after[node]["BootUtc"] != before[node]["BootUtc"] for node in clients):
        raise RuntimeError("Unexpected Windows reboot during the client identity check")
    evidence["checks"]["after"] = after
    attempts=json.loads(gui.diagnostics())["Repair"]
    started={item["Host"].split(".",1)[0].upper():item["StartedUtc"] for item in attempts if item["JobId"]}
    evidence["checks"]["repair_started_utc"]=started
    save()
    if set(started)!=set(clients) or not all(started.values()):
        raise RuntimeError("Missing product repair start timestamp for one or more expected clients")
    repair_start={node:datetime.fromisoformat(value) for node,value in started.items()}
    pending = set(clients)
    deadline = time.monotonic()+620
    while pending and time.monotonic()<deadline:
        for node in list(pending):
            current=after[node]
            try:
                target=json.loads(ps("DCWSUS", "$port=8530;$ssl=$false;$identityId58="+quote(current["SusClientId"])+";"+verify))
            except RuntimeError:
                continue
            # An update report must be newer than the repair of this exact device.
            if (target["Id"].lower()==current["SusClientId"].lower() and target["Name"].rstrip(".").lower()==current["Name"].rstrip(".").lower()
                    and target["ReportedUtc"] and datetime.fromisoformat(target["ReportedUtc"])>=repair_start[node]
                    and "DCLabClients" in target["Groups"]):
                pending.remove(node)
                evidence["checks"].setdefault("confirmed",{})[node]=target
                save()
        if pending:
            time.sleep(20)
    if pending:
        capture_failed_repairs(sorted(pending))
        raise RuntimeError("WSUS has not confirmed fresh registration in 10 minutes: "+", ".join(sorted(pending)))
    evidence["checks"]["packaged-console"]=gui.finish()
    resources("after",clients)
    for node,m in evidence["checks"]["resource_samples"]["after"].items():
        if m["RegistryBackups"]!=evidence["checks"]["resource_samples"]["before"][node]["RegistryBackups"]+1:
            raise RuntimeError("Expected one reset per confirmed client: "+node)
        if m["Agent"]>1:
            raise RuntimeError("Multiple agents remained on "+node)
    save()
    print(f"Real WSUS confirmed {len(clients)} independent reports and AD devices.",flush=True)
