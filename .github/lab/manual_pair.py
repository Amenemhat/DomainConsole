"""Check the packaged agent on two independent disposable Windows guests."""
import concurrent.futures
import hashlib
import json
import time
import uuid
import zipfile

def prepare_fixture(root):
    """Stage the exact package before boot, leaving WinRM for the behavior under test."""
    with zipfile.ZipFile(root/"package.zip") as package:
        agent=package.read("Agent/DomainConsole.Agent.exe")
    job=str(uuid.uuid4())
    root_path=r"C:\DomainConsoleLab\manual58"
    job_path=root_path+"\\"+job
    exe=root_path+"\\DomainConsole.Agent.exe"
    marker=job_path+"\\executions.txt"
    action="Add-Content -LiteralPath '"+marker+"' -Value 'executed'; Start-Sleep -Seconds 6"
    payload={"Id":job,"Manual56":True,"OutputChannel":"Commands","Steps":[{"Name":"Receipt loss fixture","Kind":"PowerShell","Code":action}]}
    (root/"manual-agent.exe").write_bytes(agent)
    (root/"manual-job.json").write_text(json.dumps(payload),encoding="utf-8")
    guest="/DomainConsoleLab/manual58"
    return (job,job_path,exe,marker,hashlib.sha256(agent).hexdigest()), [
        f"mkdir-p {guest}/{job}",
        f"upload {root / 'manual-agent.exe'} {guest}/DomainConsole.Agent.exe",
        f"upload {root / 'manual-job.json'} {guest}/{job}/job.json",
    ]


def exercise(fixture, ps, session, evidence, save):
    nodes=("DCWSUS","DCAD")
    ids={n:ps(n,r"(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography').MachineGuid") for n in nodes}
    if not all(ids.values()) or len(set(ids.values()))!=2:
        raise RuntimeError("Expected two independent Windows installations")
    job,job_path,exe,marker,agent_hash=fixture
    for node in nodes:
        if ps(node,f"(Get-FileHash -Algorithm SHA256 '{exe}').Hash").lower()!=agent_hash:
            raise RuntimeError("Agent copy mismatch for "+node)
        if ps(node,f"(Get-Content -Raw '{job_path}\\job.json'|ConvertFrom-Json).Id")!=job:
            raise RuntimeError("Job definition mismatch for "+node)
    evidence["checks"]["staged_agent"]={"nodes":list(nodes),"agent_sha256":agent_hash,"job":job}
    save()
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
