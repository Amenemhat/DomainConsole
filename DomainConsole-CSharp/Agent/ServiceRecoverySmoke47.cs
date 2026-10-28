using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  static Action<string> serviceRestorerFixture47;
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
   Console.WriteLine("ServiceRecovery47 allowlist and idempotency passed; no real service changed.");
   return 0;
  }
 }
}
