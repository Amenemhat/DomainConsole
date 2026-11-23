"""Check the packaged agent on two independent disposable Windows guests."""
import base64
import concurrent.futures
import hashlib
import json
import time
import uuid
import zipfile

def exercise(root, ps, session, evidence, save):
    nodes=("DCWSUS","DCAD")
    ids={n:ps(n,r"(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography').MachineGuid") for n in nodes}
    if not all(ids.values()) or len(set(ids.values()))!=2:
        raise RuntimeError("Expected two independent Windows installations")
    with zipfile.ZipFile(root/"package.zip") as package:
        agent=package.read("Agent/DomainConsole.Agent.exe")
    agent_hash=hashlib.sha256(agent).hexdigest()
    job=str(uuid.uuid4())
    root_path=r"C:\DomainConsoleLab\manual58"
    job_path=root_path+"\\"+job
    exe=root_path+"\\DomainConsole.Agent.exe"
    marker=job_path+"\\executions.txt"
    action="Add-Content -LiteralPath '"+marker+"' -Value 'executed'; Start-Sleep -Seconds 6"
    payload={"Id":job,"Manual56":True,"OutputChannel":"Commands","Steps":[{"Name":"Receipt loss fixture","Kind":"PowerShell","Code":action}]}
    definition=base64.b64encode(json.dumps(payload).encode("utf-8")).decode()
    def stage(node):
        ps(node,f"New-Item -ItemType Directory -Force '{job_path}'|Out-Null; [IO.File]::WriteAllBytes('{exe}',[byte[]]@()); [IO.File]::WriteAllBytes('{job_path}\\job.json',[Convert]::FromBase64String('{definition}'))")
        for offset in range(0,len(agent),3500):
            part=base64.b64encode(agent[offset:offset+3500]).decode()
            ps(node,f"$f=[IO.File]::Open('{exe}',[IO.FileMode]::Append);try{{$b=[Convert]::FromBase64String('{part}');$f.Write($b,0,$b.Length)}}finally{{$f.Dispose()}}")
        if ps(node,f"(Get-FileHash -Algorithm SHA256 '{exe}').Hash").lower()!=agent_hash:
            raise RuntimeError("Agent copy mismatch for "+node)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        list(pool.map(stage,nodes))
    scheduled="DCLab-Manual58-"+job
    second=scheduled+"-repeat"
    for node in nodes:
        ps(node,f"$action=New-ScheduledTaskAction -Execute '{exe}' -Argument 'run {job_path}'; Register-ScheduledTask -TaskName '{scheduled}' -Action $action -User SYSTEM -RunLevel Highest -Force|Out-Null; Register-ScheduledTask -TaskName '{second}' -Action $action -User SYSTEM -RunLevel Highest -Force|Out-Null")
    def launch(node, lose_reply=False, duplicate=False):
        task=second if duplicate else scheduled
        invocation=f"Start-ScheduledTask -TaskName '{task}'"
        if not lose_reply:
            ps(node,invocation)
            return
        reply=session(node).run_ps(invocation+";exit 72")
        if reply.status_code!=72:
            raise RuntimeError("Receipt-loss fixture failed")
    def await_value(node, script, seconds=55):
        until=time.monotonic()+seconds
        value=""
        while time.monotonic()<until:
            value=ps(node,script).strip()
            if value:
                return value
            time.sleep(0.4)
        raise TimeoutError(node+" did not present expected evidence")
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        missing=pool.submit(launch,nodes[0],True)
        peer=pool.submit(launch,nodes[1])
        missing.result()
        peer.result()
    await_value(nodes[0],f"if(Test-Path '{marker}'){{'started'}}")
    launch(nodes[0],duplicate=True)
    states={}
    for node in nodes:
        state=await_value(node,f"$p='{job_path}\\status.json';if(Test-Path $p){{try{{$s=Get-Content -Raw $p|ConvertFrom-Json;if($s.Ended){{$s|ConvertTo-Json -Compress}}}}catch{{}}}}")
        status=json.loads(state)
        if status.get("Status")!="Completed":
            raise RuntimeError(node+" did not complete native agent job")
        states[node]={"status":status["Status"],"executions":int(ps(node,f"@(Get-Content '{marker}'|Where-Object {{$_ -eq 'executed'}}).Count"))}
        if states[node]["executions"]!=1:
            raise RuntimeError("Command replayed on "+node)
    evidence["checks"]["manual_pair"]={"distinct_machine_ids":ids,"agent_sha256":agent_hash,"job":job,"simulated_missing_confirmation":True,"results":states}
    save()
    print("PASS: native agent executed once on both disposable Windows clients, including a repeated launch after an inconclusive receipt.",flush=True)
