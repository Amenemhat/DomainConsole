using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease44(){
  var now=DateTimeOffset.UtcNow;var positive=new RebootSnapshot{Required=true,BootUtc=now.AddDays(-2).ToString("o"),CollectedUtc=now.AddMinutes(-20).ToString("o"),Sources=new(){"RebootRequired"}};
  var view=DescribeReboot40(positive,positive.BootUtc,now);var report=new WsusReport{NeededCount=0,RequiredComplete=true,LastReportedUtc=now.ToString("o"),LastSyncResult="Succeeded"};
  var a=ClientReadiness41.Evaluate(report,false,true,view,new(){Status="Completed"},false,true,0,now.ToString("o"),"",now,24,positive.BootUtc);
  if(view.Fresh||view.Required!=true||a.Kind!="Reboot"||!a.Title.Contains("последней проверке"))throw new Exception("Aged positive reboot vanished");
  var after=DescribeReboot40(positive,now.AddMinutes(-10).ToString("o"),now);if(after.Required!=null||!after.Limitation.Contains("повторная"))throw new Exception("New boot falsely cleared flags without check");
  var plan=new MaintenancePlan{StartedUtc=now.AddDays(-3).ToString("o"),DeadlineUtc44=now.AddDays(5).ToString("o")};if(!PlanExpired44(plan,now))throw new Exception("Saved deadline extended plan lifetime");
  var job=new RemoteJob{Created=now.AddHours(-2).ToString("o"),Targets=new(){new(){Host="plan44.fixture.invalid"}}};BeginMaintenance(job);var original=maintenancePlans[job.Targets[0].Host];original.Done=true;BeginMaintenance(job);BeginAcceptedPlan38(job,job.Targets[0]);if(!ReferenceEquals(original,maintenancePlans[job.Targets[0].Host])||!original.Done)throw new Exception("Completed plan revived");maintenancePlans.Remove(job.Targets[0].Host);SaveMaintenance();
  var result=new PollResult{State=new(){Status="Completed"},Files=new(){new(){Name="step-1.out",Body="FIRST44\n"+string.Join("\n",Enumerable.Repeat("middle",150))+"\nLAST44"},new(){Name="step-2.out",Body="SECOND44"}}};var shown=PresentResult("fixture.invalid",result);if(!shown.Contains("FIRST44")||!shown.Contains("LAST44")||!shown.Contains("SECOND44"))throw new Exception("Command blocks overwritten");
  if(PausePolling41 is not System.Windows.Controls.CheckBox||PausePolling41.Parent!=AutoServiceToggle.Parent)throw new Exception("Polling and auto switches not grouped");
  CaptureContent38("release44-layout",1000,640,true);
 }
}
