using DomainConsole.Shared;
using System.Windows;
using System.Windows.Controls;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyRelease52(){
  foreach(var label in new[]{"Службы антивируса и центра безопасности","Перезапуск служб (только название)","Службы: диагностика"}){
   var formatted=ResultPresentation36.Format("## Итог задания\nСостояние: Завершено\n## Результаты команд\n"+label+": успешно · 0\n## Вывод команды\nWinDefend: Stopped\nИсходный вывод");
   if(formatted.Contains("возобновилась ли загрузка")||formatted.Contains("Работа служб после команды")||!formatted.Contains("## Вывод команды\nWinDefend: Stopped\nИсходный вывод"))throw new Exception("Read-only service output classified as recovery");
  }
  await VerifyTreeKeyboard52();
  VerifyTreeCheckFocus52();
  await VerifyAttentionReading52();
  await VerifySearchInput52();
  await VerifyIpReconnect52();
  VerifyProgress52();
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
   var endpoint=Identity40.EndpointKey(ConnectionStore.Capture(row.Target));pendingReportRepairs52.Add(endpoint);
   if(!ReportRepairBusy52(row)||QueueCriterion52(row,original,old).Repair52)throw new Exception("Repeated preparation exposes another report repair");pendingReportRepairs52.Remove(endpoint);
   var queuedRepair=new RemoteJob{OutputChannel="WSUS",Steps=new(){QueueVerifyStep52()},Targets=new(){new(){Host=row.Host,Status="Queued"}}};History.Add(queuedRepair);
   if(!ReportRepairBusy52(row))throw new Exception("Queued repair permits another job");queuedRepair.Targets[0].Status="Completed";if(ReportRepairBusy52(row))throw new Exception("Completed repair retains submission lock");History.Remove(queuedRepair);
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
   System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"report-queue-verify52.ps1"),QueueVerifyStep52().Code,new System.Text.UTF8Encoding(true));
   System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ui-release52.txt"),"PASS: queue evidence/action/fresh-report; short/long reconnect; search then one install; cycles; stopped/Auto OFF; bounded queue verification step");
  }finally{
   Computers.Remove(row);maintenancePlans.Clear();foreach(var p in plans)maintenancePlans[p.Key]=p.Value;History.Clear();foreach(var j in jobs)History.Add(j);queue.Clear();foreach(var q in pending)queue.Enqueue(q);queueEvidence52.Clear();foreach(var e in evidence)queueEvidence52[e.Key]=e.Value;reportRecoveries.Clear();foreach(var r in recoveries)reportRecoveries[r.Key]=r.Value;
   AutoServiceEnabled=auto;if(paused)PollingControl43.Pause();else PollingControl43.Resume();SaveMaintenance();Save();Store.Write("report-queue52.json",queueEvidence52);if(tick)timer.Start();
  }
 }
 void VerifyTreeCheckFocus52(){
  var selected=ComputerGrid.SelectedItem;
  var row=Computers.First(r=>!ReferenceEquals(r,selected)&&computerNodes.ContainsKey(r.Host));
  var leaf=computerNodes[row.Host];var check=Visual33<CheckBox>((System.Windows.DependencyObject)leaf.Header).First();
  var args=new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent,Source=check};
  leaf.RaiseEvent(args);
  if(!ReferenceEquals(ComputerGrid.SelectedItem,selected))throw new Exception("Marking a different tree client steals current client");
 }
 void VerifyProgress52(){
  var row=new ComputerRow(new(){Host="progress52.fixture.invalid"});Computers.Add(row);
  var state=new RemoteState{Status="Running",Updated=DateTimeOffset.UtcNow.ToString("o")};
  var job=new RemoteJob{Steps=new(){new(){Kind="CMD"}},Targets=new(){new(){Host=row.Host,Status="Running",State=state}}};
  var oldIndex=refreshJobs39;refreshJobs39=null;
  try{
   History.Insert(0,job);confirmedJobs.Add(job.Id+row.Host);liveConnections.Add(row.Host);
   clientConnections[row.Host]=new(){LastSuccess=DateTimeOffset.UtcNow};
   RefreshActivity();if(!row.Working||!row.WorkBusy)throw new Exception("Live work without percent lost indeterminate progress");
   state.Progress=42;RefreshActivity();if(!row.Working||row.WorkBusy||row.WorkProgress!=42)throw new Exception("Determinate progress lost");
   clientConnections[row.Host].Lost38=true;RefreshActivity();if(row.Working||row.WorkBusy)throw new Exception("Offline job still animates");
   clientConnections[row.Host].Lost38=false;job.Targets[0].Status="Completed";state.Status="Completed";
   RefreshActivity();if(row.Working||row.WorkBusy)throw new Exception("Completed job still animates");
   History.Remove(job);RefreshActivity();if(row.Working||row.WorkBusy)throw new Exception("Connection-only polling animates work");
   System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ui-progress52.txt"),"PASS: fresh acknowledged work without percent is indeterminate; 42 percent is determinate; offline/completed/background-only states do not animate");
  }finally{History.Remove(job);Computers.Remove(row);confirmedJobs.Remove(job.Id+row.Host);liveConnections.Remove(row.Host);clientConnections.Remove(row.Host);refreshJobs39=oldIndex;RefreshActivity();}
 }
 void VerifyQueueLayout52(ComputerRow row,Criterion45 report){
  var selected=ComputerGrid.SelectedItem;var tab=MainTabs.SelectedItem;int theme=Theme.Current;
  try{
   ComputerGrid.SelectedItem=row;MainTabs.SelectedItem=WsusTab;ShowCurrent();
   foreach(int palette in new[]{0,1,2})foreach(var size in new[]{(1480d,940d),(1000d,640d)}){
    Theme.Apply(palette,false);criteria45[row.Host]=new(){new("updates","Обновления · поиск завершён","Success","Success",new()),report,new("reboot","Перезагрузка · не требуется","Success","Success",new())};
    rendered45="";RenderCriteria45(row,new("Warning","Ожидается отчёт",""),false);
    Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();
    CaptureContent38("queue52-"+palette+"-"+(size.Item1>1000?"wide":"compact"),size.Item1,size.Item2);
    var button=Visual33<Button>(ReadinessTree45).Single(b=>b.Content?.ToString()=="Восстановить отчётность");
    if(!button.IsEnabled||button.ToolTip?.ToString()?.Contains("5 файлов · 30 байт")!=true)throw new Exception("Queue action/hint missing in rendered criteria");
   }
  }finally{criteria45.Remove(row.Host);Theme.Apply(theme,false);ComputerGrid.SelectedItem=selected;MainTabs.SelectedItem=tab;rendered45="";ShowCurrent();}
 }

 async Task VerifyTreeKeyboard52(){
  var selected=ComputerGrid.SelectedItem;string savedOu=ou;bool open=TreeGroup.IsExpanded;
  var root=(TreeViewItem)OuTree.Items[0];
  var branch=root.Items.OfType<TreeViewItem>().LastOrDefault(x=>x.Tag is OuBranchState&&x.Items.Count>0)??root;
  bool expanded=branch.IsExpanded;var marks=Computers.Select(x=>x.Selected).ToArray();
  var scroll=FindTreeScroll(OuTree);double offset=scroll?.VerticalOffset??0;
  try{
   TreeGroup.IsExpanded=true;root.IsExpanded=true;branch.IsExpanded=false;UpdateLayout();
   Activate();SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
   branch.Focus();branch.BringIntoView();await Task.Delay(100);
   Key51(0x27);Key51(0x27,true);await Task.Delay(100);
   if(!branch.IsExpanded)throw new Exception("Keyboard Right did not expand OU");
   var focus=System.Windows.Input.Keyboard.FocusedElement;double at=scroll?.VerticalOffset??0;
   RefreshOutcomes();RefreshBranches();UpdateLayout();await Task.Delay(100);
   if(!ReferenceEquals(System.Windows.Input.Keyboard.FocusedElement,focus)||Math.Abs((scroll?.VerticalOffset??0)-at)>1||!ReferenceEquals(ComputerGrid.SelectedItem,selected))throw new Exception("Background refresh stole OU focus/scroll/client");
   Key51(0x25);Key51(0x25,true);await Task.Delay(100);
   if(branch.IsExpanded||!Computers.Select(x=>x.Selected).SequenceEqual(marks))throw new Exception("Keyboard OU collapse changed target marks");
   System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ui-tree-keyboard52.txt"),"PASS: real Right/Left keys expand/collapse OU; background outcome refresh retains focus, viewport, client and marks");
  }finally{Key51(0x27,true);Key51(0x25,true);branch.IsExpanded=expanded;TreeGroup.IsExpanded=open;ComputerGrid.SelectedItem=selected;ou=savedOu;scroll?.ScrollToVerticalOffset(offset);}
 }

}
