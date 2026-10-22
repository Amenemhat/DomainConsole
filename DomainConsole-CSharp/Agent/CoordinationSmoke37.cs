using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  static RemoteJob FixtureJob37(string owner,string plan=""){return new RemoteJob{ConsoleId37=owner,ConsoleLabel37="Console "+owner,PlanId37=plan,Steps=new List<CommandStep>{new CommandStep{Kind="CMD",Code="echo fixture"}}};}
  static void FixtureFolder37(string root,RemoteJob job){Job=job;Folder=Path.Combine(root,job.Id);Directory.CreateDirectory(Folder);File.WriteAllText(FilePath("job.json"),Json.Serialize(job));}
  static int CoordinationSmoke37(string root){
   Directory.CreateDirectory(root);var a=FixtureJob37("A",Guid.NewGuid().ToString());FixtureFolder37(root,a);ReserveControl37();
   var b=FixtureJob37("B");FixtureFolder37(root,b);bool blocked=false;try{ReserveControl37();}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Second console entered active plan");
   b.AllowUpdateConflict=true;b.IndependentControl=true;blocked=false;try{ReserveControl37();}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Foreign force bypassed ownership");
   b.Steps[0].Kind="Diagnostics";ReserveControl37();if(ReadControl37().OwnerId!="A")throw new Exception("Read-only diagnostic took ownership");
   File.WriteAllText(Path.Combine(root,a.Id,"status.json"),Json.Serialize(new RemoteState{Id=a.Id,Status="Completed",Ended=DateTime.UtcNow.ToString("o")}));
   b.Steps[0].Kind="CMD";blocked=false;try{ReserveControl37();}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Idle interval lost plan ownership");
   FixtureFolder37(root,a);ReleaseControl37();if(ReadControl37().OwnerId.Length!=0)throw new Exception("Completed plan cannot release");
   FixtureFolder37(root,b);ReserveControl37();FixtureFolder37(root,a);a.AutomaticChild31=true;blocked=false;try{ReserveControl37();}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Previous owner resumed after handoff");
   File.WriteAllText(Path.Combine(root,b.Id,"status.json"),Json.Serialize(new RemoteState{Id=b.Id,Status="Completed",Ended=DateTime.UtcNow.ToString("o")}));
   var busy=InstallerBusy37;try{InstallerBusy37=()=>true;blocked=false;try{ClaimControl37();}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Busy installer allowed ownership transfer");InstallerBusy37=()=>false;ClaimControl37();if(ReadControl37().OwnerId!="A"||ReadControl37().PlanId.Length>0)throw new Exception("Idle handoff failed");blocked=false;try{ReserveControl37();}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Old same-console plan resumed after explicit handoff");}finally{InstallerBusy37=busy;}
   var oldChild=FixtureJob37("B",b.PlanId37.Length>0?b.PlanId37:b.Id);oldChild.AutomaticChild31=true;FixtureFolder37(root,oldChild);blocked=false;try{ReserveControl37();}catch(InvalidOperationException){blocked=true;}if(!blocked)throw new Exception("Foreign old continuation reclaimed idle transferred client");
   // Independent processes compete for the same root, as different console endpoints do.
   var raceRoot=Path.Combine(root,"race");Directory.CreateDirectory(raceRoot);var x=FixtureJob37("X",Guid.NewGuid().ToString());FixtureFolder37(raceRoot,x);string xp=Folder;var y=FixtureJob37("Y",Guid.NewGuid().ToString());FixtureFolder37(raceRoot,y);string yp=Folder;
   var exe=typeof(Program).Assembly.Location;using(var px=Process.Start(new ProcessStartInfo(exe,"reserve37 \""+xp+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true}))using(var py=Process.Start(new ProcessStartInfo(exe,"reserve37 \""+yp+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true})){
    var ex=px.StandardError.ReadToEndAsync();var ey=py.StandardError.ReadToEndAsync();if(!px.WaitForExit(20000)||!py.WaitForExit(20000))throw new Exception("Admission race did not finish");if((px.ExitCode==0)==(py.ExitCode==0))throw new Exception("Concurrent consoles did not elect a single owner");
   }
   AdmissionSmoke38(Path.Combine(root,"admission38"));Console.WriteLine("Client-wide ownership, foreign force rejection, diagnostics, plan gaps, stale continuations and cross-process admission race verified.");return 0;
  }
  static void SearchTimeoutSmoke37(){
   Stage("Поиск · fixture");State.Recommendation="old installer advice";var fake=new FakeOperation();searchDeadline37=DateTime.UtcNow.AddMilliseconds(500);var task=Task.Run(()=>WaitUpdate(fake,"Поиск"));Thread.Sleep(2300);
   if(!fake.Aborted||task.IsCompleted||!State.ActivityMessage.Contains("не предоставляет процент")||State.Recommendation.Length>0||State.Progress.HasValue)throw new Exception("Search timeout falsely completed/reported progress");
   fake.completed=true;try{if(!task.Wait(5000))throw new Exception("Confirmed search completion was not observed");throw new Exception("Search deadline not reported");}catch(AggregateException ex){if(!(ex.GetBaseException() is TimeoutException))throw;}
   searchDeadline37=default(DateTime);if(State.ActivityMessage.Length>0||State.Recommendation.Length>0)throw new Exception("Finished search retained waiting advice");
  }
 }
}
