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
manual_pair=os.environ.get("DC_TEST_MANUAL_PAIR")=="1"
if manual_pair:
    nodes={"DCWSUS":10,"DCAD":11}
elif bootstrap_only:
    nodes={"DCWSUS":10}
private_addresses=["10.77.57."+str(n) for n in nodes.values()]
os.environ["NO_PROXY"]=os.environ["no_proxy"]=",".join(["localhost","127.0.0.1",*private_addresses,os.environ.get("NO_PROXY","")])
evidence = {"source": os.environ["GITHUB_SHA"], "booted": {}, "checks": {}}
evidence_file = root / "evidence.json"
processes = {}
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


def prepare(node,extra_files=()):
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
    call("guestfish", "--rw", "-a", disk, "-i", input="\n".join([*commands,*extra_files]) + "\n", text=True)
    pointer=root/"setup-location.reg"
    pointer.write_text('Windows Registry Editor Version 5.00\n\n[HKEY_LOCAL_MACHINE\\SYSTEM\\Setup]\n"UnattendFile"="C:\\\\DomainConsoleLab\\\\unattend.xml"\n')
    call("virt-win-reg","--merge",disk,pointer)
    answer.unlink()
    tap = "dclab" + str(nodes[node])
    call("ip", "tuntap", "add", tap, "mode", "tap")
    call("ip", "link", "set", tap, "master", "dclab0")
    call("ip", "link", "set", tap, "up")


def boot(node, gpt):
    memory = 3584 if manual_pair or node == "DCWSUS" else 1536
    tap = "dclab" + str(nodes[node])
    args = [qemu, "-accel", "kvm", "-cpu", "host", "-smp", "2", "-m", str(memory),
            "-machine", "q35" if gpt else "pc", "-uuid", str(uuid.uuid4()), "-name", node,
            "-drive", f"file={root / (node + '.qcow2')},format=qcow2,if=ide",
            "-cdrom", str(root / "drivers.iso"), "-boot", "c", "-display", "none",
            "-monitor", f"unix:{root / (node + '.sock')},server=on,wait=off", "-serial", "none",
            "-netdev", f"tap,id=net0,ifname={tap},script=no,downscript=no",
            "-device", f"virtio-net-pci,netdev=net0,id=nic0,mac=52:54:00:57:00:{nodes[node]:02x}"]
    if gpt:
        code = Path("/usr/share/OVMF/OVMF_CODE_4M.fd")
        nvram = root / (node + "-vars.fd")
        shutil.copy("/usr/share/OVMF/OVMF_VARS_4M.fd", nvram)
        args += ["-drive", f"if=pflash,format=raw,readonly=on,file={code}", "-drive", f"if=pflash,format=raw,file={nvram}"]
    with (root / (node + ".log")).open("w") as output:
        processes[node] = subprocess.Popen(args, stdout=output, stderr=output)


def session(node, domain=False):
    user = ("DCLAB" if domain else node) + "\\Administrator"
    return winrm.Session("http://" + ip(node) + ":5985/wsman", auth=(user, password),
                         transport="ntlm", read_timeout_sec=65, operation_timeout_sec=50)


def ps(node, script, domain=False):
    result = session(node, domain).run_ps("$ErrorActionPreference='Stop';" + script)
    if result.status_code:
        raise RuntimeError(node + ": " + result.std_err.decode(errors="replace").replace(password, "***")[-2400:])
    return result.std_out.decode("utf-8-sig", errors="replace").strip()


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

def wait(node, domain=False, minutes=24):
    until = time.monotonic() + 60 * minutes
    last_problem="No reply"
    while time.monotonic() < until:
        if processes[node].poll() is not None:
            raise RuntimeError(node + " stopped: " + (root / (node + ".log")).read_text()[-900:])
        try:
            with socket.create_connection((ip(node), 5985), timeout=2):
                pass
            actual = ps(node, "$env:COMPUTERNAME", domain)
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


def setup_domain():
    ps("DCAD", "Install-WindowsFeature AD-Domain-Services -IncludeManagementTools | Out-Null")
    ps("DCAD", "$pw=ConvertTo-SecureString '" + password + "' -AsPlainText -Force;"
       "Install-ADDSForest -DomainName dc057.test -DomainNetbiosName DCLAB -InstallDns -Force -NoRebootOnCompletion -SafeModeAdministratorPassword $pw | Out-Null")
    ps("DCAD", "shutdown.exe /r /t 2")
    time.sleep(20)
    wait("DCAD", True, 18)
    for name in ["LabA", "LabB"]:
        ps("DCAD", f"New-ADOrganizationalUnit -Name {name} -Path 'DC=dc057,DC=test' -ProtectedFromAccidentalDeletion $false", True)


def join(node):
    ou = "LabA" if nodes[node] % 2 else "LabB"
    ps(node, "Get-NetAdapter|Where-Object Status -eq Up|Set-DnsClientServerAddress -ServerAddresses 10.77.57.11;"
       "$cred=[pscredential]::new('DCLAB\\Administrator',(ConvertTo-SecureString '" + password + "' -AsPlainText -Force));" +
       f"Add-Computer -DomainName dc057.test -OUPath 'OU={ou},DC=dc057,DC=test' -Credential $cred -Force;shutdown.exe /r /t 2")
    time.sleep(15)
    wait(node)


def main():
    (root / "SetupComplete.cmd").write_bytes(b"@echo off\r\npowershell -NoProfile -ExecutionPolicy Bypass -File C:\\DomainConsoleLab\\guest-init.ps1\r\n")
    part = call("guestfish", "--ro", "-a", base, "run", ":", "part-get-parttype", "/dev/sda", capture_output=True, text=True).stdout.strip()
    log("Guest partition table: " + part)
    extra_files=()
    if manual_pair:
        from manual_pair import exercise, prepare_fixture
        fixture,extra_files=prepare_fixture(root)
    for node in nodes:
        log("Creating distinct Windows guest " + node)
        prepare(node,extra_files)
    checkpoint("disks")
    # First drive one guest through specialization with the runner CPU to itself.
    boot("DCWSUS",part=="gpt")
    wait("DCWSUS",minutes=12)
    if manual_pair:
        boot("DCAD",part=="gpt")
        wait("DCAD",minutes=12)
        exercise(fixture,ps,session,evidence,save)
        checkpoint("winrm")
        return
    if bootstrap_only:
        if os.getenv("DC_TEST_ACTUAL_REBOOT") == "1":
            from reboot_guest import exercise as reboot_exercise
            reboot_exercise(root,ps,session,evidence,save)
        checkpoint("winrm")
        return
    remaining=[n for n in nodes if n!="DCWSUS"]
    for start in range(0,len(remaining),3):
        batch=remaining[start:start+3]
        for node in batch:
            boot(node, part=="gpt")
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
            list(pool.map(wait,batch))
    checkpoint("winrm")
    setup_domain()
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(join, [n for n in nodes if n != "DCAD"]))
    checkpoint("domain")
    system("DCWSUS", "install-wsus", (here / "wsus.ps1").read_text(), minutes=35)
    evidence["checks"]["WSUS"] = ps("DCWSUS", "[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');([Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer('localhost',$false,8530)).Version.ToString()")
    log("WSUS API available: " + evidence["checks"]["WSUS"])
    checkpoint("wsus")
    until=time.monotonic()+45*60
    while not (root/"package.json").is_file() and time.monotonic()<until:
        time.sleep(3)
    evidence["package"]=json.loads((root/"package.json").read_text())
    save()
    from identity_lab import exercise
    from packaged_gui import PackagedConsole
    gui=PackagedConsole(root,ps,password)
    try:
        exercise(nodes, ps, system, evidence, save, root, gui)
    finally:
        if gui.server:
            gui.server.shutdown()
    save()


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
