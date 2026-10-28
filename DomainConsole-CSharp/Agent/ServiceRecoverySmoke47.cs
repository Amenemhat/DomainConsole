using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
     int exit=PowerShell(code,"step-1",2500);
     if(scenario=="NormalFinally"){
      if(exit==0||!File.Exists(FilePath("finally-ran"))||restored.Count!=0||!File.ReadAllText(FilePath("step-1.failure.txt")).Contains("FIXTURE_PRIMARY_FAILURE"))throw new Exception("Normal finally / primary failure changed");
     }else{
      if(exit!=124||File.Exists(FilePath("finally-ran"))||restored.Count!=1||restored[0]!="wuauserv")throw new Exception("Hard timeout failed to restore exactly the recorded service");
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
   Console.WriteLine("ServiceRecovery47 allowlist and idempotency passed; no real service changed.");
   return 0;
  }
 }
}
