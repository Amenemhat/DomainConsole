using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease45(){
  if(LogViewer.LineTone("Установка успешна · требуется перезагрузка")!="Warning"||LogViewer.LineTone("✓ Обновления · необходимых нет")!="Success"||LogViewer.LineTone("✕ Отчёт WSUS · ошибка")!="Error")throw new Exception("Journal disagrees with readiness criterion tones");
  var now=DateTimeOffset.UtcNow;string at=now.AddHours(-1).ToString("o"),boot=now.AddDays(-1).ToString("o");
  var old=new RebootSnapshot{Required=true,CollectedUtc=at,BootUtc=boot,Sources=new(){"Windows Update API","RebootRequired (Windows Update)","RebootPending (CBS)"}};
  var partial=new RebootSnapshot{CollectedUtc=now.ToString("o"),Error="uptime unavailable"};RebootSnapshotPolicy.Merge(old,partial);
  var view=DescribeReboot40(partial,"",now);if(view.Required!=true||view.Stamp!=at||view.Sources.Length!=3||view.Fresh)throw new Exception("Partial/missing boot lost positive reboot evidence or its source time");
  var partial2=new RebootSnapshot{CollectedUtc=now.AddSeconds(1).ToString("o"),Error="partial again"};RebootSnapshotPolicy.Merge(partial,partial2);if(DescribeReboot40(partial2,"",now).Required!=true||partial2.PreservedSources45.Count!=3)throw new Exception("Repeated partial discarded durable sources");
  var rebooted=new RebootSnapshot{BootUtc=now.AddMinutes(-10).ToString("o"),CollectedUtc=now.ToString("o")};RebootSnapshotPolicy.Merge(old,rebooted);if(rebooted.PreservedRequired.HasValue)throw new Exception("Old evidence survived proven new boot");
  var negative=new RebootSnapshot{Required=false,CollectedUtc=now.ToString("o")};RebootSnapshotPolicy.Merge(old,negative);if(negative.Required!=false||negative.PreservedRequired.HasValue)throw new Exception("Complete negative did not clear requirement");
  var brokenNegative=new RebootSnapshot{Required=false,CollectedUtc=now.ToString("o"),Error="CBS unavailable"};RebootSnapshotPolicy.Merge(old,brokenNegative);if(brokenNegative.Required!=null||brokenNegative.PreservedRequired!=true)throw new Exception("Partial negative cleared positive");
  string id=Guid.NewGuid().ToString();var report=new WsusReport{LastReportedUtc=at,LastSyncResult="Succeeded",FailedCount=1,NeededCount=1,RequiredComplete=true,FailedUpdates=new(){new(){Id=id,Revision=2}}};
  var state=new RemoteState{Status="Completed",Ended=now.ToString("o"),InstalledCount38=1,Results=new(){new(){Kind="Updates",ExitCode=0,Ended=now.ToString("o")}},InstalledUpdates45=new(){new(){Id=id,Revision=2,At=now.ToString("o")}}};
  if(!Criteria45.Resolved45(report,state))throw new Exception("Matching update success did not reconcile old error");state.InstalledUpdates45[0].Revision=3;if(Criteria45.Resolved45(report,state))throw new Exception("Different revision incorrectly cleared error");state.InstalledUpdates45[0].Revision=2;report.FailedUpdates.Add(new(){Id=Guid.NewGuid().ToString(),Revision=2});report.FailedCount=2;if(Criteria45.Resolved45(report,state))throw new Exception("Independent error hidden");
  var assessment=ClientReadiness41.Evaluate(report,false,true,view,state,false,true,0,now.ToString("o"),"",now,24);if(assessment.Kind!="Reboot"||!assessment.Title.Contains("Установка успешна"))throw new Exception("Old server error overrode successful installation and confirmed reboot");
  var groups=Criteria45.Build(assessment,report,view,state,false,now.ToString("o"),0,false,"",boot);if(groups.Count!=3||!groups[1].Text.Contains("ожидается")||!groups[2].Action)throw new Exception("Readiness groups/action do not explain old report/reboot");
  var running=Criteria45.Build(assessment,report,view,state,true,now.ToString("o"),0,false,"",boot);if(running.Any(g=>g.Action))throw new Exception("Active installation offers unrelated reboot check");
  if(Criteria45.Confirmed(new(){At=now.ToString("o"),Report=at,Scan=at,Reboot=at}).Any(g=>g.Tone!="Success"||g.Checks.Any(c=>c.Tone is "Warning" or "Error")))throw new Exception("Preserved full confirmation mixes partial observations into readiness criteria");
  var deferred45=OverviewRetryPolicy45(true,true,0,now,now.AddMinutes(15));if(deferred45.Attempts!=0||deferred45.Next!=now.AddMinutes(15))throw new Exception("Deferred probe consumed retry or bypassed cooldown");
  if(OverviewRetryPolicy45(true,false,2,now,now).Next.HasValue||OverviewRetryPolicy45(false,false,0,now,now).Next.HasValue||OverviewRetryPolicy45(true,true,0,now,DateTimeOffset.MaxValue).Next.HasValue)throw new Exception("Overview endlessly retries success, exhausted or blocked endpoint");
  if(OverviewRetryPolicy45(true,false,0,now,now).Next!=now.AddMinutes(2))throw new Exception("Retry lacks minimum pause");
  var row=Current();var previous=criteria45.GetValueOrDefault(row.Host);try{criteria45[row.Host]=groups;MainTabs.SelectedItem=WsusTab;RenderCriteria45(row,assessment,false);if(ReadinessTree45.Children.Count!=3)throw new Exception("Three readiness roots missing");var first=(Expander)ReadinessTree45.Children[0];first.IsExpanded=false;rendered45="";RenderCriteria45(row,assessment,false);if(((Expander)ReadinessTree45.Children[0]).IsExpanded)throw new Exception("Refresh reopened manually collapsed criterion");CaptureContent38("release45-criteria",1000,740,true);}finally{if(previous!=null)criteria45[row.Host]=previous;else criteria45.Remove(row.Host);rendered45="";ShowCurrent();}
 }
}
