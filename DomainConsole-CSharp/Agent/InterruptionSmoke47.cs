using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  // Isolated fault-injection worker: real agent process, mutex and persisted state;
  // the WUA operation is simulated. Never searches/downloads/installs real updates.
  static int InterruptionWorker47(string folder){
   Folder=Path.GetFullPath(folder);Job=Json.Deserialize<RemoteJob>(File.ReadAllText(FilePath("job.json")));
   using(var lane=new Mutex(false,@"Local\DomainConsole.InterruptionFixture47."+Job.Id)){
    lane.WaitOne();ReserveControl37();using(var self=Process.GetCurrentProcess()){
     State=new RemoteState{Id=Job.Id,Status="Running",HasUpdateSteps=true,UpdateOperationStarted=true,Started=DateTime.UtcNow.ToString("o"),ExecutorPid37=self.Id,ExecutorStarted37=self.StartTime.ToUniversalTime().ToString("o"),Progress=24};
     Stage(Job.Name);File.WriteAllText(FilePath("worker-ready47"),"ready");
     while(true){Thread.Sleep(500);Save();}
    }
   }
  }
  static int InterruptionSmoke47(string root){
   root=Path.GetFullPath(root);Directory.CreateDirectory(root);
   var outputs=new List<RemoteState>();var busy=InstallerBusy37;
   try{
    foreach(string stage in new[]{"Search","Download","Install","BeforeFinalResult"}){
     string scope=Path.Combine(root,stage);Directory.CreateDirectory(scope);
     var job=FixtureJob37("kill-fixture47");job.PlanId37=job.Id;job.Name=stage;job.Steps[0].Kind="Updates";
     FixtureFolder37(scope,job);string folder=Folder;string file=FilePath("status.json");
     using(var p=Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"interruption-worker47 "+Quote(folder)){UseShellExecute=false,CreateNoWindow=true})){
      try{
       var deadline=DateTime.UtcNow.AddSeconds(20);while(!File.Exists(Path.Combine(folder,"worker-ready47"))&&!p.HasExited&&DateTime.UtcNow<deadline)Thread.Sleep(100);
       if(!File.Exists(Path.Combine(folder,"worker-ready47"))||p.HasExited)throw new Exception("Fault worker failed to start: "+stage);
       InstallerBusy37=()=>false;
       if(ControlGate37(()=>JobFinished37(job.Id)))throw new Exception("Live worker incorrectly released");
       p.Kill();if(!p.WaitForExit(10000))throw new Exception("Killed worker remained alive");
       var killed=Json.Deserialize<RemoteState>(File.ReadAllText(file));
       if(killed.Status!="Running"||!string.IsNullOrEmpty(killed.Ended))throw new Exception("Kill unexpectedly saved terminal state");
       if(ControlGate37(()=>JobFinished37(job.Id)))throw new Exception("Two-minute confirmation grace bypassed");
       // Advance only the persisted heartbeat age, not boot time or executor identity.
       killed.Updated=DateTime.UtcNow.AddMinutes(-3).ToString("o");File.WriteAllText(file,Json.Serialize(killed));string before=File.ReadAllText(file);
       InstallerBusy37=()=>true;if(ControlGate37(()=>JobFinished37(job.Id)))throw new Exception("Busy WUA released after kill");
       if(File.ReadAllText(file)!=before)throw new Exception("Busy check mutated state");
       InstallerBusy37=()=>false;if(!ControlGate37(()=>JobFinished37(job.Id)))throw new Exception("Dead same-boot worker not reconciled");
       var recovered=Json.Deserialize<RemoteState>(File.ReadAllText(file));
       if(recovered.Status!="InterventionRequired"||!recovered.Error.StartsWith("DC_INTERRUPTED46:")||recovered.Ended.Length==0||File.ReadAllText(file+".interrupted46.bak")!=before)throw new Exception("Unknown outcome/backup lost");
       outputs.Add(recovered);
       if(!ControlGate37(()=>JobFinished37(job.Id)))throw new Exception("Repeated recovery not idempotent");
       var child=FixtureJob37("kill-fixture47",job.Id);child.AutomaticChild31=true;child.Steps[0].Kind="Updates";FixtureFolder37(scope,child);
       bool rejected=false;try{ReserveControl37();}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("Blind installation replay accepted");
       FixtureFolder37(scope,job);if(!ReleaseControl37()||!ReleaseControl37())throw new Exception("Confirmed interrupted plan cannot release");
       var scan=FixtureJob37("kill-fixture47");scan.PlanId37=scan.Id;scan.Steps[0].Kind="UpdateScan";FixtureFolder37(scope,scan);ReserveControl37();ReserveControl37();
       if(ReadControl37().JobIds.Count!=1)throw new Exception("Repeated reservation duplicated job");
       File.WriteAllText(FilePath("status.json"),Json.Serialize(new RemoteState{Id=scan.Id,Status="Completed",Ended=DateTime.UtcNow.ToString("o")}));ReleaseControl37();
       Console.WriteLine(stage+": real worker killed; no final result; grace/busy guards; backup; no blind replay; release twice; one fresh-search reservation; worker alive=0.");
      }finally{if(!p.HasExited){p.Kill();p.WaitForExit(10000);}}
     }
    }
    File.WriteAllText(Path.Combine(root,"recovered-states47.json"),Json.Serialize(outputs));return 0;
   }finally{InstallerBusy37=busy;}
  }
 }
}
