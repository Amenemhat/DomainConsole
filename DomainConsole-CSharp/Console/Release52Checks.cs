using DomainConsole.Shared;
using System.Windows;
using System.Windows.Controls;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyRelease52(){
  var plans=maintenancePlans.ToList();var jobs=History.ToList();var pending=queue.ToList();var evidence=queueEvidence52.ToList();var recoveries=reportRecoveries.ToList();
  bool auto=AutoServiceEnabled,paused=TransportBudget41.Paused,tick=timer.IsEnabled;timer.Stop();
  var row=new ComputerRow(new(){Host="continuation52.fixture.invalid",Name="Continuation fixture"});Computers.Add(row);
  try{
   PollingControl43.Resume();AutoServiceEnabled=true;maintenancePlans.Clear();History.Clear();queue.Clear();reportRecoveries.Clear();
   var now=DateTimeOffset.UtcNow;var old=new WsusReport{LastReportedUtc=now.AddDays(-4).ToString("o")};
   var probe=new ClientReportProbe{CollectedUtc=now.ToString("o"),UseWUServer=1,WUServer="http://wsus.fixture.invalid",WUStatusServer="http://wsus.fixture.invalid",Queues=new(){new(){Name="EventCache",Files=3,Bytes=10,OldestUtc=now.AddDays(-3).ToString("o")},new(){Name="EventCache.v2",Files=2,Bytes=20}}};
   RememberQueue52(ConnectionStore.Capture(row.Target),old,probe);
   var original=new Criterion45("report","Отчёт WSUS","Warning","Warning",new());
   var view=QueueCriterion52(row,original,old);if(!view.Repair52||!view.Hint52.Contains("5 файлов · 30 байт"))throw new Exception("Report queue is not actionable/visible");
   VerifyQueueLayout52(row,view);
   probe.QueueError="Access denied";if(!QueueHint52(probe).Contains("не определён")||QueueCriterion52(row,original,old).Repair52)throw new Exception("Incomplete queue is treated as empty/actionable");probe.QueueError="";
   if(QueueCriterion52(row,original,new(){LastReportedUtc=now.ToString("o")}).Repair52)throw new Exception("Fresh report retained obsolete repair");
   var target=ConnectionStore.Capture(row.Target);target.Status="Completed";target.State=new(){Status="Completed",Started=now.AddMinutes(-30).ToString("o"),Ended=now.AddMinutes(-20).ToString("o"),UpdatesRemaining=1};
   var scan=new RemoteJob{OutputChannel="WSUS",Steps=new(){new(){Kind="UpdateScan"}},Targets=new(){target}};History.Add(scan);BeginAcceptedPlan38(scan,target);
   var plan=maintenancePlans[row.Host];if(plan.Cycles!=0)throw new Exception("Search consumed installation cycle");
   Task<RebootSnapshot?> Snapshot(ComputerRow _)=>Task.FromResult<RebootSnapshot?>(new(){Required=false,BootUtc=now.AddDays(-1).ToString("o"),CollectedUtc=DateTimeOffset.UtcNow.ToString("o")});
   ResumeConnection52(row.Host,now.AddMinutes(-2),now);if(plan.RecheckAfterConnection52)throw new Exception("Short disconnection forced a new scan");
   ResumeConnection52(row.Host,now.AddMinutes(-30),now);if(!plan.RecheckAfterConnection52)throw new Exception("Long disconnection reused old applicability");
   await AdvanceMaintenance(Snapshot,_=>Task.FromResult(new LiveUpdateActivity()),false);
   if(queue.Count!=1||queue.Peek().Job.Steps.Single().Kind!="UpdateScan"||plan.Cycles!=0)throw new Exception("Return duplicated installation instead of rechecking");
   var fresh=queue.Dequeue().Job;fresh.Targets[0].Status="Completed";fresh.Targets[0].State=new(){Status="Completed",Ended=DateTimeOffset.UtcNow.ToString("o"),UpdatesRemaining=1};plan.Next=default;
   await AdvanceMaintenance(Snapshot,_=>Task.FromResult(new LiveUpdateActivity()),false);
   if(queue.Count!=1||queue.Peek().Job.Steps.Single().Kind!="Updates"||plan.Cycles!=1)throw new Exception("Successful recheck did not continue installation once");
   plan.Next=default;await AdvanceMaintenance(Snapshot,_=>throw new Exception("Duplicate installation probe"),false);if(queue.Count!=1)throw new Exception("Repeated poll duplicated installation");
   queue.Clear();plan.Done=true;plan.Phase="Обслуживание остановлено пользователем";ResumeConnection52(row.Host,now.AddHours(-1),now);if(!plan.Done)throw new Exception("Connection revived a stopped plan");
   var repair=new RemoteJob{Steps=new(){QueueVerifyStep52()}};PrepareReportContinuation52(repair,row.Host);if(repair.BeginPlan38)throw new Exception("Repair revived stopped installation consent");
   plan.Phase="Автоматическое продолжение выключено";PrepareReportContinuation52(repair,row.Host);if(repair.BeginPlan38)throw new Exception("Repair revived a completed/disabled plan");
   maintenancePlans.Clear();AutoServiceEnabled=false;PrepareReportContinuation52(repair,row.Host);if(repair.BeginPlan38)throw new Exception("Repair enables installation with Auto OFF");
   AutoServiceEnabled=true;PrepareReportContinuation52(repair,row.Host);if(!repair.BeginPlan38)throw new Exception("Recovery search lost automatic continuation");
   NormalizeCommandKinds34(new[]{QueueVerifyStep52()});
   System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ui-release52.txt"),"PASS: queue evidence/action/fresh-report; short/long reconnect; search then one install; cycles; stopped/Auto OFF; bounded queue verification step");
  }finally{
   Computers.Remove(row);maintenancePlans.Clear();foreach(var p in plans)maintenancePlans[p.Key]=p.Value;History.Clear();foreach(var j in jobs)History.Add(j);queue.Clear();foreach(var q in pending)queue.Enqueue(q);queueEvidence52.Clear();foreach(var e in evidence)queueEvidence52[e.Key]=e.Value;reportRecoveries.Clear();foreach(var r in recoveries)reportRecoveries[r.Key]=r.Value;
   AutoServiceEnabled=auto;if(paused)PollingControl43.Pause();else PollingControl43.Resume();SaveMaintenance();Save();Store.Write("report-queue52.json",queueEvidence52);if(tick)timer.Start();
  }
 }
 void VerifyQueueLayout52(ComputerRow row,Criterion45 report){
  var selected=ComputerGrid.SelectedItem;var tab=MainTabs.SelectedItem;int theme=Theme.Current;
  try{
   ComputerGrid.SelectedItem=row;MainTabs.SelectedItem=WsusTab;ShowCurrent();
   foreach(int palette in new[]{0,1,2})foreach(var size in new[]{(1480d,940d),(1000d,640d)}){
    Theme.Apply(palette,false);criteria45[row.Host]=new(){new("updates","Обновления · поиск завершён","Success","Success",new()),report,new("reboot","Перезагрузка · не требуется","Success","Success",new())};
    rendered45="";RenderCriteria45(row,new("Warning","Ожидается отчёт",""),false);
    CaptureContent38("queue52-"+palette+"-"+(size.Item1>1000?"wide":"compact"),size.Item1,size.Item2);
    var button=Visual33<Button>(ReadinessTree45).Single(b=>b.Content?.ToString()=="Восстановить отчётность");
    if(!button.IsEnabled||button.ToolTip?.ToString()?.Contains("5 файлов · 30 байт")!=true)throw new Exception("Queue action/hint missing in rendered criteria");
   }
  }finally{criteria45.Remove(row.Host);Theme.Apply(theme,false);ComputerGrid.SelectedItem=selected;MainTabs.SelectedItem=tab;rendered45="";ShowCurrent();}
 }

}
