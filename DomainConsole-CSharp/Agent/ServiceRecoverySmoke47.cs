using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  static Action<string> serviceRestorerFixture47;
  static int ServiceInterruptionWorker47(string folder){
   Folder=Path.GetFullPath(folder);Job=Json.Deserialize<RemoteJob>(File.ReadAllText(FilePath("job.json")));ReserveControl37();
   using(var self=Process.GetCurrentProcess())State=new RemoteState{Id=Job.Id,Status="Running",HasUpdateSteps=true,Started=DateTime.UtcNow.ToString("o"),ExecutorPid37=self.Id,ExecutorStarted37=self.StartTime.ToUniversalTime().ToString("o")};
   Stage("Cache interruption fixture");CurrentKind="CacheCleanup";
   return PowerShell("[IO.Directory]::CreateDirectory($dcServiceRecovery47)|Out-Null;[IO.File]::WriteAllText((Join-Path $dcServiceRecovery47 'bits.pending'),'fixture');[IO.File]::WriteAllText('"+FilePath("service-child-ready47").Replace("'","''")+"','ready');while($true){Start-Sleep -Milliseconds 100}","step-1",60000);
  }
  // Real Windows PowerShell process and real timeout/job-object termination.
  // Only service START is substituted: no Windows service is changed by this test.
  static int ServiceRecoverySmoke47(string root){
   foreach(var kind in new[]{"CacheCleanup","ReportRecovery"})foreach(var scenario in new[]{"Timeout","RestoreFailure","NormalFinally"}){
    Folder=Path.Combine(Path.GetFullPath(root),kind+scenario);Directory.CreateDirectory(Folder);
    State=new RemoteState{Status="Running"};Job=new RemoteJob();CurrentKind=kind;
    var restored=new List<string>();serviceRestorerFixture47=name=>{restored.Add(name);if(scenario=="RestoreFailure")throw new IOException("FIXTURE_RESTORE_FAILURE");};
    string code="[IO.Directory]::CreateDirectory($dcServiceRecovery47)|Out-Null;$p=Join-Path $dcServiceRecovery47 'wuauserv.pending';[IO.File]::WriteAllText($p,'intent');try{"+
     (scenario=="NormalFinally"?"throw 'FIXTURE_PRIMARY_FAILURE'":"while($true){Start-Sleep -Milliseconds 100}")+
     "}finally{[IO.File]::WriteAllText('"+FilePath("finally-ran").Replace("'","''")+"','yes');Remove-Item $p -Force}";
    try{
     int exit=PowerShell(code,"step-1",12000);
     if(scenario=="NormalFinally"){
      if(exit==0||!File.Exists(FilePath("finally-ran"))||restored.Count!=0||!File.ReadAllText(FilePath("step-1.failure.txt")).Contains("FIXTURE_PRIMARY_FAILURE"))throw new Exception("Normal finally / primary failure changed");
     }else{
      if(exit!=124||File.Exists(FilePath("finally-ran"))||restored.Count!=1||restored[0]!="wuauserv")throw new Exception("Hard timeout failed to restore exactly the recorded service: exit="+exit+"; finally="+File.Exists(FilePath("finally-ran"))+"; restored="+string.Join(",",restored)+"; failure="+(File.Exists(FilePath("step-1.failure.txt"))?File.ReadAllText(FilePath("step-1.failure.txt")):"none"));
      bool pending=File.Exists(FilePath("step-1.service-recovery/wuauserv.pending"));
      if(pending!=(scenario=="RestoreFailure"))throw new Exception("Pending recovery evidence lost or retained after success");
      var error=File.ReadAllText(FilePath("step-1.failure.txt"));
      if(scenario=="RestoreFailure"&&(!error.Contains("FIXTURE_RESTORE_FAILURE")||!error.Contains("124")))throw new Exception("Recovery failure replaced original timeout");
     }
     Console.WriteLine("ServiceRecovery47 "+kind+" "+scenario+" passed");
    }finally{serviceRestorerFixture47=null;serviceRecovery47=null;}
   }
   var path=Path.Combine(root,"allowlist");Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"WinRM.pending"),"foreign");
   int calls=0;var guard=new ServiceRecovery47(path,n=>calls++);
   if(guard.Recover().Count!=1||calls!=0||!File.Exists(Path.Combine(path,"WinRM.pending")))throw new Exception("Foreign service accepted");
   File.Delete(Path.Combine(path,"WinRM.pending"));File.WriteAllText(Path.Combine(path,"bits.pending"),"own");
   if(guard.Recover().Count!=0||guard.Recover().Count!=0||calls!=1)throw new Exception("Recovery not idempotent");
   var livePath=Path.Combine(root,"live-child");Directory.CreateDirectory(livePath);File.WriteAllText(Path.Combine(livePath,"bits.pending"),"own");
   using(var child=Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"stop-fixture ignore"){UseShellExecute=false,CreateNoWindow=true})){
    try{var liveGuard=new ServiceRecovery47(livePath,n=>calls++);liveGuard.Remember(child);int before=calls;
     if(liveGuard.Recover().Count!=1||calls!=before)throw new Exception("Live child raced service restoration");
     child.Kill();if(!child.WaitForExit(10000))throw new Exception("Fixture child did not exit");
     if(liveGuard.Recover().Count!=0||calls!=before+1)throw new Exception("Exited child did not permit restoration");
    }finally{if(!child.HasExited){child.Kill();child.WaitForExit(10000);}}
   }
   var failedJob=new RemoteJob{ContinueOnError=true,Steps=new List<CommandStep>{new CommandStep{Kind="CacheCleanup",Name="Expected cache failure",Code="throw 'CACHE_FAILURE_FIXTURE'"},new CommandStep{Kind="PowerShell",Name="Must not run",Code="[IO.File]::WriteAllText('"+Path.Combine(root,"unexpected-next-step").Replace("'","''")+"','bad')"}}};
   var failedFolder=Path.Combine(Path.GetFullPath(root),failedJob.Id);Directory.CreateDirectory(failedFolder);File.WriteAllText(Path.Combine(failedFolder,"job.json"),Json.Serialize(failedJob));
   using(var executor=Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"run "+Quote(failedFolder)){UseShellExecute=false,CreateNoWindow=true})){
    if(!executor.WaitForExit(60000)){executor.Kill();executor.WaitForExit(10000);throw new Exception("Protected failure fixture exceeded deadline");}
   }
   var final=Json.Deserialize<RemoteState>(File.ReadAllText(Path.Combine(failedFolder,"status.json")));
   if(final.Results.Count!=1||final.Status!="Failed"||File.Exists(Path.Combine(root,"unexpected-next-step")))throw new Exception("Failed reset continued despite safety boundary");
   var interrupted=FixtureJob37("service-interruption47");interrupted.PlanId37=interrupted.Id;interrupted.Steps[0].Kind="CacheCleanup";
   string scope=Path.Combine(Path.GetFullPath(root),"agent-killed");Directory.CreateDirectory(scope);FixtureFolder37(scope,interrupted);string interruptedFolder=Folder;
   var previousBusy=InstallerBusy37;int recoveredCalls=0;serviceRestorerFixture47=n=>{if(n!="bits")throw new Exception("Wrong recovery target");recoveredCalls++;};InstallerBusy37=()=>false;
   try{using(var agent=Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"service-interruption-worker47 "+Quote(interruptedFolder)){UseShellExecute=false,CreateNoWindow=true})){
    try{
     var until=DateTime.UtcNow.AddSeconds(30);while(!File.Exists(Path.Combine(interruptedFolder,"service-child-ready47"))&&!agent.HasExited&&DateTime.UtcNow<until)System.Threading.Thread.Sleep(100);
     if(agent.HasExited||!File.Exists(Path.Combine(interruptedFolder,"service-child-ready47")))throw new Exception("Service interruption worker not ready");
     if(ControlGate37(()=>JobFinished37(interrupted.Id))||recoveredCalls!=0)throw new Exception("Live agent recovery accepted");
     agent.Kill();if(!agent.WaitForExit(10000))throw new Exception("Killed agent survived");
     var statePath=Path.Combine(interruptedFolder,"status.json");var stale=Json.Deserialize<RemoteState>(File.ReadAllText(statePath));stale.Updated=DateTime.UtcNow.AddMinutes(-3).ToString("o");File.WriteAllText(statePath,Json.Serialize(stale));
     var identity=Path.Combine(interruptedFolder,"step-1.service-recovery","child.identity");var savedIdentity=File.ReadAllText(identity);File.Delete(identity);
     if(ControlGate37(()=>JobFinished37(interrupted.Id))||recoveredCalls!=0)throw new Exception("Missing child identity accepted");
     File.WriteAllText(identity,savedIdentity);InstallerBusy37=()=>true;
     if(ControlGate37(()=>JobFinished37(interrupted.Id))||recoveredCalls!=0)throw new Exception("Busy WUA allowed interrupted service recovery");
     InstallerBusy37=()=>false;
     if(!ControlGate37(()=>JobFinished37(interrupted.Id))||recoveredCalls!=1)throw new Exception("Dead agent service intent not restored exactly once");
     if(!ControlGate37(()=>JobFinished37(interrupted.Id))||recoveredCalls!=1)throw new Exception("Repeated interrupted service recovery");
     var outcome=Json.Deserialize<RemoteState>(File.ReadAllText(statePath));if(outcome.Status!="InterventionRequired"||!outcome.Error.StartsWith("DC_INTERRUPTED46:"))throw new Exception("Interrupted cache declared successful");
     outcome.ServiceRecoveryError47="Expected pending service";File.WriteAllText(statePath,Json.Serialize(outcome));File.WriteAllText(Path.Combine(interruptedFolder,"step-1.service-recovery","bits.pending"),"fixture");
     InstallerBusy37=()=>true;if(ControlGate37(()=>JobFinished37(interrupted.Id))||recoveredCalls!=1)throw new Exception("Pending terminal service failure bypassed busy guard");InstallerBusy37=()=>false;
     if(!ControlGate37(()=>JobFinished37(interrupted.Id))||recoveredCalls!=2)throw new Exception("Terminal service recovery did not unblock safely");
     outcome=Json.Deserialize<RemoteState>(File.ReadAllText(statePath));if(outcome.Status!="InterventionRequired"||outcome.ServiceRecoveryError47.Length!=0)throw new Exception("Service recovery rewrote operation outcome");
    }finally{if(!agent.HasExited){agent.Kill();agent.WaitForExit(10000);}}
   }}finally{InstallerBusy37=previousBusy;serviceRestorerFixture47=null;}
   Console.WriteLine("ServiceRecovery47 allowlist, idempotency and killed-agent persisted-intent recovery passed; no real service changed.");
   return 0;
  }
 }
}
