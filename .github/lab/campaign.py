"""One private domain checkpoint, fresh disposable overlays for each explicitly revised scenario."""
import concurrent.futures
import importlib.util
import json
import shutil
import subprocess
import time
import traceback


def run(root,nodes,domain_disks_use_uefi,processes,boot,wait,ps,system,evidence,save,checkpoint,password):
    def shutdown(node):
        ps(node,"shutdown.exe /s /t 2 /c 'Save disposable laboratory checkpoint'",node=="DCAD")
        processes[node].wait(timeout=150)
        processes.pop(node)
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(shutdown,nodes))
    for node in nodes:
        (root/(node+".qcow2")).rename(root/(node+"-domain.qcow2"))
        if domain_disks_use_uefi:
            shutil.copyfile(root/(node+"-vars.fd"),root/(node+"-baseline.fd"))
    initial_checks=dict(evidence["checks"])
    for attempt in range(1,4):
        driver=root/f"scenario-{attempt}"
        marker=driver.with_suffix(".tree")
        until=time.monotonic()+48*60
        while not marker.exists() and time.monotonic()<until:
            time.sleep(3)
        if not marker.is_file():
            raise TimeoutError("No reviewed domain scenario arrived")
        for node in nodes:
            disk=root/(node+".qcow2")
            disk.unlink(missing_ok=True)
            subprocess.run(["qemu-img","create","-q","-f","qcow2","-F","qcow2","-b",root/(node+"-domain.qcow2"),disk],check=True)
            if domain_disks_use_uefi:
                shutil.copyfile(root/(node+"-baseline.fd"),root/(node+"-vars.fd"))
        for node in ["DCAD","DCWSUS",*[n for n in nodes if n.startswith("CLONE")]]:
            boot(node,domain_disks_use_uefi)
            wait(node,node=="DCAD",minutes=12)
        evidence["checks"]=dict(initial_checks)
        evidence["scenario_tree"]=marker.read_text().strip()
        evidence["package"]=json.loads((root/"package.json").read_text())
        def module(name):
            spec=importlib.util.spec_from_file_location(name,driver/(name+".py"))
            item=importlib.util.module_from_spec(spec)
            spec.loader.exec_module(item)
            return item
        gui=module("packaged_gui").PackagedConsole(root,ps,password)
        try:
            module("identity_lab").exercise(nodes,ps,system,evidence,save,root,gui)
            outcome={"passed":True}
        except Exception as exc:
            outcome={"passed":False,"error":str(exc).replace(password,"***")[:2200],
                     "trace":traceback.format_exc().replace(password,"***")[-4000:]}
            print(outcome["trace"],flush=True)
        finally:
            if gui.server:
                gui.server.shutdown()
                gui.server.server_close()
        save()
        receipt=root/f"attempt-{attempt}.json"
        receipt.write_text(json.dumps({**outcome,"evidence":evidence},indent=2))
        checkpoint(f"attempt-{attempt}")
        if outcome["passed"]:
            return
        for proc in processes.values():
            proc.terminate()
        for proc in processes.values():
            try:
                proc.wait(timeout=20)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait()
        processes.clear()
    raise RuntimeError("All three reviewed fixtures failed")
