using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  // Only admission/readiness waiting is bounded here. This never kills an installer.
  static bool DeferWait52(TimeSpan elapsed,string reason){
   if(elapsed.TotalSeconds<Math.Max(1,Math.Min(300,Job.WaitBudgetSeconds52)))return false;
   State.Deferred52=true;State.Status="InterventionRequired";State.Stage="Ожидание отложено; исполнитель завершён";
   State.ActivityMessage=reason+" превысило предел. Контрольная точка сохранена; действующая установка не остановлена.";
   State.Error=State.ActivityMessage;State.Ended=DateTime.UtcNow.ToString("o");Save();Log(State.ActivityMessage);return true;
  }
  static int WaitingSmoke52(string root){
   Directory.CreateDirectory(root);var id=Guid.NewGuid().ToString();Folder=Path.Combine(root,id);Directory.CreateDirectory(Folder);
   Job=new RemoteJob{Id=id,WaitBudgetSeconds52=1,Steps=new System.Collections.Generic.List<CommandStep>{new CommandStep{Kind="CMD",Code="echo SHOULD_NOT_EXECUTE52",Name="Deferred fixture"}}};
   File.WriteAllText(FilePath("job.json"),Json.Serialize(Job));
   using(var lane=new Mutex(false,ExecutionLane())){
    if(!lane.WaitOne(0))throw new Exception("Fixture lane unexpectedly occupied");
    try{using(var p=Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"run "+Quote(Folder)){UseShellExecute=false,CreateNoWindow=true})){
     if(!p.WaitForExit(15000)){p.Kill();throw new Exception("Waiting agent did not terminate");}
     if(p.ExitCode!=0)throw new Exception("Waiting agent failed unexpectedly");
    }}finally{lane.ReleaseMutex();}
   }
   var result=Json.Deserialize<RemoteState>(File.ReadAllText(FilePath("status.json")));
   if(!result.Deferred52||result.Ended.Length==0||result.Results.Count!=0||result.Status!="InterventionRequired")throw new Exception("Waiting checkpoint missing or command executed");
   // A repeated scheduler invocation reads the terminal checkpoint and cannot replay it.
   using(var p=Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"run "+Quote(Folder)){UseShellExecute=false,CreateNoWindow=true})){if(!p.WaitForExit(10000)){p.Kill();throw new Exception("Deferred agent restarted work");}}
   if(File.Exists(FilePath("step-1.out")))throw new Exception("Deferred job replayed after lane freed");
   Console.WriteLine("Waiting52: owned agent exited at admission deadline; no command/installer started; persisted checkpoint prevents scheduler replay.");return 0;
  }
 }
}
