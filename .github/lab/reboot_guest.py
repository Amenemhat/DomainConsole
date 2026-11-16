"""Run the packaged console's exact reboot script on the single disposable guest."""
import base64
import concurrent.futures
from datetime import datetime
import json
import hashlib
import time
import uuid


def exercise(root, ps, session, evidence, save):
    target="DCWSUS"
    if ps(target,"$env:COMPUTERNAME")!=target:
        raise RuntimeError("Unrecognized reboot target")
    template=(root/"payload"/"header-reboot.ps1").read_text(encoding="utf-8-sig")
    if "{{REBOOT_JOB}}" not in template or "DCLab isolated reboot validation" not in template:
        raise RuntimeError("Missing packaged reboot script")
    boot_script="(Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().ToString('o')"
    before=ps(target,boot_script)
    info={"before_boot":before,"package":json.loads((root/"payload"/"source.json").read_text(encoding="utf-8-sig"))}
    evidence["checks"]["real_reboot"]=info
    save()

    def watch(script, seconds=45):
        end=time.monotonic()+seconds
        last=""
        while time.monotonic()<end:
            try:
                last=ps(target,script)
                if last:
                    return last
            except Exception as exc:
                last=type(exc).__name__
            time.sleep(1)
        raise TimeoutError("Missing guest evidence: "+script[:80]+", last response "+last[:120])

    def stage(job):
        data=template.replace("{{REBOOT_JOB}}",job).encode("utf-8-sig")
        folder="C:\\ProgramData\\DomainConsoleCSharp\\"+job
        destination=folder+"\\body.ps1"
        ps(target,"New-Item -ItemType Directory -Path '"+folder+"' -Force|Out-Null;[IO.File]::WriteAllBytes('"+destination+"',[byte[]]@())")
        for offset in range(0,len(data),768):
            part=base64.b64encode(data[offset:offset+768]).decode()
            ps(target,"$part=[Convert]::FromBase64String('"+part+"');$stream=[IO.File]::OpenWrite('"+destination+"');try{$null=$stream.Seek(0,'End');$stream.Write($part,0,$part.Length)}finally{$stream.Dispose()}")
        if ps(target,"(Get-FileHash '"+destination+"').Hash").lower()!=hashlib.sha256(data).hexdigest():
            raise RuntimeError("Reboot body changed during private WinRM transfer")
        script=r"""
$folder='FOLDER';New-Item -ItemType Directory -Path $folder -Force|Out-Null
$wrapped="try { & '$folder\body.ps1' } catch { [IO.File]::WriteAllText('$folder\error.txt',\$_.Exception.Message);exit 1 }"
[IO.File]::WriteAllText("$folder\run.ps1",$wrapped,(New-Object Text.UTF8Encoding($true)))
$a=New-ScheduledTaskAction -Execute powershell.exe -Argument "-NoProfile -ExecutionPolicy Bypass -File $folder\run.ps1"
Register-ScheduledTask -TaskName 'DCLab-Priority-JOB' -Action $a -User SYSTEM -RunLevel Highest -Force|Out-Null
""".replace("\\$_","\x60$_").replace("FOLDER",folder).replace("JOB",job)
        ps(target,script)
        return job,folder

    def launch(staged):
        job,folder=staged
        ps(target,"Start-ScheduledTask -TaskName 'DCLab-Priority-"+job+"'")
        return folder

    def receipt(folder):
        return json.loads(watch("if(Test-Path '"+folder+"\\reboot-plan.json'){Get-Content -Raw '"+folder+"\\reboot-plan.json'}"))

    # Keep a real concurrent WinRM read alive while scheduling the first request.
    staged=[stage(str(uuid.uuid4())) for _ in range(3)]
    pool=concurrent.futures.ThreadPoolExecutor(max_workers=1)
    try:
        hold=pool.submit(session(target).run_ps,"'ready'|Set-Content C:\\DomainConsoleLab\\held-read.txt;Start-Sleep -Seconds 120")
        watch("if(Test-Path C:\\DomainConsoleLab\\held-read.txt){'busy'}")
        cancelled=launch(staged[0])
        first=receipt(cancelled)
        if hold.done() or first.get("DispatchUtc"):
            raise RuntimeError("The independent live read was not active during the delivery")
        ps(target,"Set-Content '"+cancelled+"\\reboot-cancel.flag' 'operator'")
        watch("if((Get-Content -Raw '"+cancelled+"\\reboot-plan.json'|ConvertFrom-Json).Cancelled){'cancelled'}")
        if ps(target,boot_script)!=before:
            raise RuntimeError("Cancelled countdown rebooted Windows")
        info["cancelled_while_reading"]=True
        save()
        active=launch(staged[1])
        acknowledged=receipt(active)
        if hold.done() or not acknowledged.get("BeforeBootUtc"):
            raise RuntimeError("Missing live reboot admission while WinRM read is active")
        duplicate=launch(staged[2])
        rejected=watch("if(Test-Path '"+duplicate+"\\error.txt'){Get-Content -Raw -Encoding UTF8 '"+duplicate+"\\error.txt'}")
        if ps(target,"Test-Path '"+duplicate+"\\reboot-plan.json'")!="False":
            raise RuntimeError("Duplicate request acquired its own reboot receipt")
        info.update(accepted_plan=acknowledged,duplicate_refusal=rejected)
        save()
        until=time.monotonic()+360
        while time.monotonic()<until:
            time.sleep(5)
            try:
                stamp=ps(target,boot_script)
                if datetime.fromisoformat(stamp.replace("Z","+00:00"))>datetime.fromisoformat(before.replace("Z","+00:00")):
                    info["after_boot"]=stamp
                    break
            except Exception:
                pass
        if "after_boot" not in info:
            raise TimeoutError("Windows never reported a new boot")
        events=ps(target,r"@(Get-WinEvent -FilterHashtable @{LogName='System';Id=1074} -MaxEvents 30|Where-Object {$_.Message -like '*DCLab isolated reboot validation*'}).Count")
        if events!="1":
            raise RuntimeError("Expected one Windows reboot event, observed "+events)
        info["windows_shutdown_events"]=int(events)
        print("Confirmed one actual Windows reboot, one cancelled countdown, and repeat refusal under live WinRM load",flush=True)
        save()
    finally:
        pool.shutdown(wait=False,cancel_futures=True)
