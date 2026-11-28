"""Disposable KVM guests on a host-only switch. Never use a production endpoint."""
import base64
import concurrent.futures
import contextlib
import datetime
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import select
import socket
import subprocess
import sys
import time
import threading
import uuid
from xml.sax.saxutils import escape

import winrm

root = Path(sys.argv[1]).resolve()
here = Path(__file__).resolve().parent
if os.getenv("GITHUB_ACTIONS") != "true" or not root.name == "dc-wsus-lab":
    raise SystemExit("Expected the disposable Actions lab")
password = "Dc!7" + secrets.token_hex(20)
print("::add-mask::" + password, flush=True)
nodes = {"DCWSUS": 10, "DCAD": 11, **{f"CLONE{i}": 20 + i for i in range(1, 6)}}
bootstrap_only=os.environ.get("DC_BOOTSTRAP_ONLY")=="1"
single_identity=os.environ.get("DC_SINGLE_IDENTITY")=="1"
single_natural=single_identity and os.environ.get("DC_SINGLE_NATURAL_ONLY")=="1"
clone_pair=os.environ.get("DC_IDENTITY_CLONE_PAIR")=="1"
if bootstrap_only:
    nodes={"DCWSUS":10}
elif single_natural:
    nodes={"DCWSUS":10,"CLONE1":21}
elif single_identity:
    nodes={"DCWSUS":10,"DCAD":11,"CLONE1":21}
elif clone_pair:
    nodes={"DCWSUS":10,"DCAD":11,"CLONE1":21,"CLONE2":22}
private_addresses=["10.77.57."+str(n) for n in nodes.values()]
os.environ["NO_PROXY"]=os.environ["no_proxy"]=",".join(["localhost","127.0.0.1",*private_addresses,os.environ.get("NO_PROXY","")])
evidence = {"source": os.environ["GITHUB_SHA"], "booted": {}, "checks": {}}
evidence_file = root / "evidence.json"
processes = {}
hardware_ids={node:str(uuid.uuid4()) for node in nodes}
qemu = shutil.which("qemu-system-x86_64")
base = root / "windows.vhd"


def log(message):
    print(message, flush=True)


evidence_lock = threading.Lock()
def save():
    with evidence_lock:
        tmp = evidence_file.with_suffix(".tmp")
        tmp.write_text(json.dumps(evidence, indent=2))
        tmp.replace(evidence_file)


def checkpoint(name):
    (root / (name+".ready")).touch()
    log(name+" ready")


def call(*args, **kwargs):
    return subprocess.run(list(map(str, args)), check=True, **kwargs)


def ip(node):
    return "10.77.57." + str(nodes[node])


def unattended(node):
    component = 'processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS"'
    pw = escape(password)
    return f'''<?xml version="1.0" encoding="utf-8"?>
<unattend xmlns="urn:schemas-microsoft-com:unattend" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State">
<settings pass="specialize"><component name="Microsoft-Windows-Deployment" {component}>
<RunSynchronous><RunSynchronousCommand wcm:action="add"><Order>1</Order><Description>Initialize private test adapter</Description><Path>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\\DomainConsoleLab\\guest-init.ps1</Path></RunSynchronousCommand></RunSynchronous></component>
<component name="Microsoft-Windows-Shell-Setup" {component}><ComputerName>{node}</ComputerName><TimeZone>UTC</TimeZone></component></settings>
<settings pass="oobeSystem"><component name="Microsoft-Windows-International-Core" {component}><InputLocale>en-US</InputLocale><SystemLocale>en-US</SystemLocale><UILanguage>en-US</UILanguage><UserLocale>en-US</UserLocale></component><component name="Microsoft-Windows-Shell-Setup" {component}>
<OOBE><HideEULAPage>true</HideEULAPage><HideLocalAccountScreen>true</HideLocalAccountScreen><ProtectYourPC>3</ProtectYourPC></OOBE>
<UserAccounts><AdministratorPassword><Value>{pw}</Value><PlainText>true</PlainText></AdministratorPassword></UserAccounts>
<AutoLogon><Password><Value>{pw}</Value><PlainText>true</PlainText></Password><Enabled>true</Enabled><Username>Administrator</Username><LogonCount>1</LogonCount></AutoLogon>
<FirstLogonCommands><SynchronousCommand wcm:action="add"><Order>1</Order><CommandLine>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\\DomainConsoleLab\\guest-init.ps1</CommandLine></SynchronousCommand></FirstLogonCommands>
</component></settings></unattend>'''


def prepare(node):
    disk = root / (node + ".qcow2")
    call("qemu-img", "create", "-q", "-f", "qcow2", "-F", "vpc", "-b", base, disk)
    answer = root / (node + ".xml")
    answer.write_text(unattended(node))
    config = root / (node + ".json")
    config.write_text(json.dumps({"ip": ip(node), "name": node, "temporaryAdmin":password}))
    commands = ["mkdir-p /DomainConsoleLab", "mkdir-p /Windows/Panther",
                "mkdir-p /Windows/Setup/Scripts", f"upload {answer} /DomainConsoleLab/unattend.xml",
                "mkdir-p /Windows/Panther/Unattend",f"upload {answer} /Windows/Panther/Unattend/Unattend.xml",
                f"upload {answer} /Windows/Panther/unattend.xml",
                f"upload {here / 'guest-init.ps1'} /DomainConsoleLab/guest-init.ps1",
                f"upload {config} /DomainConsoleLab/node.json",
                f"upload {root / 'SetupComplete.cmd'} /Windows/Setup/Scripts/SetupComplete.cmd"]
    call("guestfish", "--rw", "-a", disk, "-i", input="\n".join(commands) + "\n", text=True)
    pointer=root/"setup-location.reg"
    pointer.write_text('Windows Registry Editor Version 5.00\n\n[HKEY_LOCAL_MACHINE\\SYSTEM\\Setup]\n"UnattendFile"="C:\\\\DomainConsoleLab\\\\unattend.xml"\n')
    call("virt-win-reg","--merge",disk,pointer)
    answer.unlink()
    tap = "dclab" + str(nodes[node])
    call("ip", "tuntap", "add", tap, "mode", "tap")
    call("ip", "link", "set", tap, "master", "dclab0")
    call("ip", "link", "set", tap, "up")


def boot(node, gpt, specialize=False):
    memory = 3584 if specialize or node == "DCWSUS" else 2048 if node=="DCAD" else 1536
    tap = "dclab" + str(nodes[node])
    firmware_id=hardware_ids["CLONE1"] if clone_pair and node.startswith("CLONE") else hardware_ids[node]
    args = [qemu, "-accel", "kvm", "-cpu", "host", "-smp", "2", "-m", str(memory),
            "-machine", ("q35" if gpt else "pc")+",smbios-entry-point-type=32", "-uuid", firmware_id, "-smbios", f"type=1,manufacturer=DCLab,product=WindowsDomainGuest,serial={node},uuid={firmware_id}", "-name", node,
            "-drive", f"file={root / (node + '.qcow2')},format=qcow2,if=ide",
            "-cdrom", str(root / "drivers.iso"), "-boot", "c", "-display", "none",
            "-monitor", f"unix:{root / (node + '.sock')},server=on,wait=off", "-serial", "none",
            "-netdev", f"tap,id=net0,ifname={tap},script=no,downscript=no",
            "-device", f"virtio-net-pci,netdev=net0,id=nic0,mac=52:54:00:57:00:{nodes[node]:02x}"]
    if gpt:
        code = Path("/usr/share/OVMF/OVMF_CODE_4M.fd")
        nvram = root / (node + "-vars.fd")
        if not nvram.exists():
            shutil.copy("/usr/share/OVMF/OVMF_VARS_4M.fd", nvram)
        args += ["-drive", f"if=pflash,format=raw,readonly=on,file={code}", "-drive", f"if=pflash,format=raw,file={nvram}"]
    with (root / (node + ".log")).open("w") as output:
        processes[node] = subprocess.Popen(args, stdout=output, stderr=output)


def session(node, domain=False):
    user = ("DCLAB" if domain else node) + "\\Administrator"
    return winrm.Session("http://" + ip(node) + ":5985/wsman", auth=(user, password),
                         transport="ntlm", read_timeout_sec=65, operation_timeout_sec=50)


def ps(node, script, domain=False):
    shell=session(node,domain)
    command="$ErrorActionPreference='Stop';"+script
    protocol=shell.protocol
    # Only retry creation of the transport shell. At this point no caller command has been submitted.
    deadline=time.monotonic()+150
    while True:
        try:
            shell_id=protocol.open_shell()
            break
        except Exception as exc:
            if time.monotonic()>=deadline or not any(v in str(exc).lower() for v in ("marked for deletion","2147943418","connection reset","connection refused","read timed out","remote end closed connection")):
                raise
            time.sleep(5)
    try:
        command="[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);"+command
        stream=base64.b64encode(command.encode("utf-8"))
        send_input=len(command.encode("utf-16-le"))*4//3>=7000
        wrapper="$data=[Console]::In.ReadToEnd();&([ScriptBlock]::Create([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($data))))" if send_input else command
        encoded=base64.b64encode(wrapper.encode("utf-16-le")).decode()
        command_id=protocol.run_command(shell_id,"powershell.exe",["-NoProfile","-NonInteractive","-EncodedCommand",encoded])
        try:
            if send_input:
                for start in range(0,len(stream),12000):
                    protocol.send_command_input(shell_id,command_id,stream[start:start+12000],end=start+12000>=len(stream))
            stdout,stderr,status=protocol.get_command_output(shell_id,command_id)
        finally:
            protocol.cleanup_command(shell_id,command_id)
    finally:
        protocol.close_shell(shell_id)
    if status:
        raise RuntimeError(node+": "+stderr.decode(errors="replace").replace(password,"***")[-2400:])
    return stdout.decode("utf-8-sig",errors="replace").strip()


def capture_console(node):
    path=root/(node+"-boot.ppm")
    try:
        with socket.socket(socket.AF_UNIX,socket.SOCK_STREAM) as handle:
            handle.settimeout(4);handle.connect(str(root/(node+".sock")))
            if select.select([handle],[],[],0.4)[0]:
                handle.recv(4096)
            handle.sendall(("screendump "+str(path)+"\n").encode())
            limit=time.monotonic()+6
            while not path.exists() and time.monotonic()<limit:
                if select.select([handle],[],[],0.2)[0]:
                    handle.recv(16384)
        from PIL import Image
        with Image.open(path) as shot:
            shot.save(root/(node+"-boot.png"))
        path.unlink()
    except (OSError,ValueError) as exc:
        log("Boot screen unavailable: "+str(exc))

def wait(node, domain=False, minutes=24, specialized=False):
    until = time.monotonic() + 60 * minutes
    last_problem="No reply"
    while time.monotonic() < until:
        if processes[node].poll() is not None:
            raise RuntimeError(node + " stopped: " + (root / (node + ".log")).read_text()[-900:])
        try:
            with socket.create_connection((ip(node), 5985), timeout=2):
                pass
            probe="$env:COMPUTERNAME"
            if specialized:
                probe=r"if((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\State').ImageState -eq 'IMAGE_STATE_COMPLETE' -and (Test-Path C:\DomainConsoleLab\network-ready.txt)){$env:COMPUTERNAME}else{'Specializing'}"
            actual = ps(node,probe,domain)
            last_problem="Unexpected guest name "+actual
            if actual == node:
                evidence["booted"][node] = actual
                save()
                log("WinRM ready: " + node)
                return
        except Exception as exc:
            last_problem=str(exc).replace(password,"***")[:350]
        if not int((until-time.monotonic())//9)%8:
            log(node+" waiting for WinRM: "+last_problem[:240])
        time.sleep(8)
    evidence.setdefault("winrm_failure",{})[node]=last_problem
    save()
    capture_console(node)
    raise TimeoutError("WinRM did not become ready on " + node)


def system(node, name, script, domain=False, minutes=20):
    """Create one bounded SYSTEM task. Report completion independently of console connectivity."""
    path = "C:\\DomainConsoleLab\\" + name
    wrapped = "$ErrorActionPreference='Stop';try { & {\n" + script + "\n};if(!$?){throw 'PowerShell execution failed'};'ok'|Set-Content '" + path + ".done'}catch{($_|Out-String)|Set-Content '" + path + ".error';exit 1}"
    data = base64.b64encode(wrapped.encode("utf-8-sig")).decode()
    ps(node, f"$p='{path}';[IO.File]::WriteAllBytes($p+'.ps1',[Convert]::FromBase64String('{data}'));" +
       f"$a=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File {path}.ps1';" +
       f"Register-ScheduledTask -TaskName 'DCLab-{name}' -Action $a -User SYSTEM -RunLevel Highest -Force|Out-Null;Start-ScheduledTask 'DCLab-{name}'", domain)
    until = time.monotonic() + 60 * minutes
    while time.monotonic() < until:
        result = ps(node, f"$p='{path}';if(Test-Path ($p+'.error')){{'FAIL '+(Get-Content ($p+'.error') -Raw)}}elseif(Test-Path ($p+'.done')){{'OK'}}", domain)
        if result.startswith("FAIL"):
            raise RuntimeError(node + " " + name + " " + result[:1200])
        if result == "OK":
            ps(node, f"Unregister-ScheduledTask 'DCLab-{name}' -Confirm:$false", domain)
            return
        time.sleep(5)
    raise TimeoutError(node + " " + name)


def start_role_task(node, label, body):
    # Promotion/domain join can invalidate the initiating WinRM logon. The local task owns this operation through restart.
    path="C:\\DomainConsoleLab\\"+label
    wrapped="$ErrorActionPreference='Stop';try{"+body+";'ok'|Set-Content '"+path+".done';shutdown.exe /r /t 4}catch{($_|Out-String)|Set-Content '"+path+".error';exit 1}"
    encoded=base64.b64encode(wrapped.encode("utf-8-sig")).decode()
    ps(node,f"$p='{path}';[IO.File]::WriteAllBytes($p+'.ps1',[Convert]::FromBase64String('{encoded}'));"+
       f"$a=New-ScheduledTaskAction -Execute powershell.exe -Argument '-NoProfile -ExecutionPolicy Bypass -File {path}.ps1';"+
       f"Register-ScheduledTask -TaskName 'DCLab-{label}' -Action $a -User SYSTEM -RunLevel Highest -Force|Out-Null;Start-ScheduledTask 'DCLab-{label}'")


def wait_role(node,label,domain,minutes):
    limit=time.monotonic()+minutes*60
    query=("$p='C:\\DomainConsoleLab\\"+label+"';if(Test-Path ($p+'.error')){'FAIL '+(Get-Content ($p+'.error') -Raw)}"+
           "elseif((Test-Path ($p+'.done')) -and (Get-CimInstance Win32_ComputerSystem).Domain -eq 'dc057.test' -and "+
           "(Get-CimInstance Win32_OperatingSystem).LastBootUpTime -gt (Get-Item ($p+'.done')).LastWriteTime){'READY'}")
    last=""
    while time.monotonic()<limit:
        try:
            result=ps(node,query,domain)
        except Exception as exc:
            last=str(exc).replace(password,"***")[:250]
            if domain and "credentials" in last.lower():
                try:
                    local_error=ps(node,"$p='C:\\DomainConsoleLab\\"+label+".error';if(Test-Path $p){Get-Content $p -Raw}")
                except Exception:
                    local_error=""
                if local_error:
                    raise RuntimeError(node+" "+local_error.replace(password,"***")[:1000])
            time.sleep(6)
            continue
        if result.startswith("FAIL"):
            raise RuntimeError(node+" "+label+" "+result[:1000].replace(password,"***"))
        if result=="READY":
            return
        time.sleep(7)
    raise TimeoutError(node+" "+label+" did not return after restart: "+last)


def setup_domain():
    start_role_task("DCAD","promote",r"$feature=Install-WindowsFeature AD-Domain-Services -IncludeManagementTools;if(!$feature.Success){throw 'AD DS role installation failed'};"
                    +"$pw=ConvertTo-SecureString '"+password+"' -AsPlainText -Force;"
                    +"Install-ADDSForest -DomainName dc057.test -DomainNetbiosName DCLAB -InstallDns -Force -NoRebootOnCompletion -SafeModeAdministratorPassword $pw | Out-Null")
    time.sleep(24)
    wait_role("DCAD","promote",True,22)
    until=time.monotonic()+300
    while True:
        try:
            found=ps("DCAD","if((Get-ADDomain -Server localhost).DNSRoot -eq 'dc057.test'){'READY'}",True)
            if found=="READY":
                break
        except Exception:
            if time.monotonic()>=until:
                raise
        if time.monotonic()>=until:
            raise TimeoutError("AD Web Services not ready")
        time.sleep(5)
    for name in ["LabA","LabB"]:
        ps("DCAD",f"New-ADOrganizationalUnit -Server localhost -Name {name} -Path 'DC=dc057,DC=test' -ProtectedFromAccidentalDeletion $false",True)


def join(node):
    ou="LabA" if nodes[node]%2 else "LabB"
    if clone_pair or os.environ.get("DC_IDENTITY_OWN_WUA_PROCESS")=="1":
        ps(node,r"""
$end=[DateTime]::UtcNow.AddMinutes(2)
do {
 $srv=Resolve-DnsName '_ldap._tcp.dc._msdcs.dc057.test' -Type SRV -Server 10.77.57.11 -DnsOnly -QuickTimeout -ErrorAction SilentlyContinue
 if($srv.NameTarget){return}
 Start-Sleep -Seconds 5
}while([DateTime]::UtcNow -lt $end)
throw 'The isolated domain SRV record was not advertised'
""")
    start_role_task(node,"join-domain","Get-NetAdapter|Where-Object Status -eq Up|Set-DnsClientServerAddress -ServerAddresses 10.77.57.11;"
                    +"$cred=[pscredential]::new('DCLAB\\Administrator',(ConvertTo-SecureString '"+password+"' -AsPlainText -Force));"
                    +f"Add-Computer -DomainName dc057.test -OUPath 'OU={ou},DC=dc057,DC=test' -Credential $cred -Force")
    time.sleep(15)
    wait_role(node,"join-domain",False,14)


def main():
    (root / "SetupComplete.cmd").write_bytes(b"@echo off\r\npowershell -NoProfile -ExecutionPolicy Bypass -File C:\\DomainConsoleLab\\guest-init.ps1\r\n")
    part = call("guestfish", "--ro", "-a", base, "run", ":", "part-get-parttype", "/dev/sda", capture_output=True, text=True).stdout.strip()
    log("Guest partition table: " + part)
    for node in nodes:
        log("Creating distinct Windows guest " + node)
        prepare(node)
    checkpoint("disks")
    gpt=part=="gpt"
    if bootstrap_only:
        boot("DCWSUS",gpt,True)
        wait("DCWSUS",minutes=12,specialized=True)
        checkpoint("winrm")
        return
    # Specialization and OOBE require substantially more RAM than an idle member.
    # Two fresh guests use 7 GiB combined. Preserve per-guest readiness and S5.
    def specialize(node):
        boot(node,gpt,True)
        wait(node,minutes=16,specialized=True)
        if node=="DCWSUS":
            if ps(node,"#"+("x"*6000)+"\n'private WinRM stdin ready'")!="private WinRM stdin ready":
                raise RuntimeError("Long WinRM stdin transfer failed")
            evidence["checks"]["bounded_winrm_stdin"]=True
        ps(node,"shutdown.exe /s /t 2 /c 'Complete private lab image initialization'")
        processes[node].wait(timeout=120)
        processes.pop(node)
        evidence.setdefault("specialized",{})[node]=evidence["booted"].pop(node)
        save()
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        list(pool.map(specialize,nodes))
    checkpoint("specialized")
    if not single_natural:
        # Complete promotion while the isolated controller has the runner to itself.
        boot("DCAD",gpt)
        wait("DCAD",minutes=12)
        setup_domain()
        evidence["checks"]["controller_promoted_before_members"] = True
        save()
    for node in nodes:
        if node=="DCAD":
            continue
        boot(node,gpt)
        wait(node,minutes=12)
    checkpoint("winrm")
    if not single_natural:
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            list(pool.map(join, [n for n in nodes if n != "DCAD"]))
        checkpoint("domain")
    else:
        evidence["workgroup_control"]=True
    system("DCWSUS", "install-wsus", (here / "wsus.ps1").read_text(), minutes=35)
    evidence["checks"]["WSUS"] = ps("DCWSUS", "[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');([Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer('localhost',$false,8530)).Version.ToString()")
    log("WSUS API available: " + evidence["checks"]["WSUS"])
    checkpoint("wsus")
    if single_identity:
        sys.path.insert(0,str(here.parent / "tests" / "domain-scenario"))
        from single_identity import exercise
        from single_wsus_wire import capture_exchange
        def capture():
            return capture_exchange(root, "dclab21", ip("CLONE1"), ip("DCWSUS"))

        url=f"http://{ip('DCWSUS')}:8530" if single_natural else "http://dcwsus.dc057.test:8530"
        own_process=single_natural and os.environ.get("DC_SINGLE_OWN_WUA_PROCESS") == "1"
        exercise(ps,system,wait,evidence,save,capture,url,natural_only=single_natural,
                 own_process=own_process,
                 fresh_auth=own_process and os.environ.get("DC_SINGLE_FRESH_AUTH") == "1")
        checkpoint("single-identity")
        return
    until=time.monotonic()+45*60
    while not (root/"package.json").is_file() and time.monotonic()<until:
        time.sleep(3)
    evidence["package"]=json.loads((root/"package.json").read_text())
    save()
    import campaign
    campaign.run(root,nodes,gpt,processes,boot,wait,ps,system,evidence,save,checkpoint,password)


try:
    main()
except Exception as exc:
    evidence["failure"]=str(exc).replace(password,"***")[:1800]
    raise
finally:
    save()
    for proc in processes.values():
        proc.terminate()
    for node, proc in processes.items():
        try:
            proc.wait(timeout=12)
        except subprocess.TimeoutExpired:
            proc.kill()
        errorlog = (root / (node + ".log")).read_text(errors="replace")
        if errorlog:
            log(node + " QEMU: " + errorlog[-450:])
    for node in set(processes) - set(evidence["booted"]):
        disk = root / (node + ".qcow2")
        for logpath in ["/DomainConsoleLab/network.log", "/DomainConsoleLab/network-ready.txt", "/DomainConsoleLab/setup.log", "/Windows/Panther/setuperr.log", "/Windows/Panther/setupact.log", "/Windows/Panther/UnattendGC/setupact.log"]:
            result = subprocess.run(["guestfish", "--ro", "-a", str(disk), "-i", "cat", logpath], capture_output=True, text=True, errors="replace", timeout=90)
            log(node + " " + logpath + ": " + result.stdout.replace(password, "***")[-2400:])
