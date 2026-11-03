using System.IO;
using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyRelease53(){
  var now=DateTimeOffset.UtcNow;string boot=now.AddDays(-3).ToString("o");
  var proof=new RebootSnapshot{Required=false,BootUtc=boot,CollectedUtc=now.ToString("o")};
  foreach(var elapsed in new[]{TimeSpan.FromMinutes(6),TimeSpan.FromHours(12),TimeSpan.FromDays(1)}){
   var restored=JsonSerializer.Deserialize<RebootSnapshot>(JsonSerializer.Serialize(proof,Store.Options),Store.Options)!;
   var view=DescribeReboot40(restored,boot,now+elapsed);
   if(!view.Fresh||view.Required!=false)throw new Exception("Elapsed time/restart erased complete negative flags");
  }
  if(DescribeReboot40(proof,now.AddSeconds(1).ToString("o"),now.AddMinutes(2)).Required!=null)throw new Exception("New boot reused old flags");
  proof.Error="CBS access denied";if(DescribeReboot40(proof,boot,now).Required!=null)throw new Exception("Partial negative accepted");
  proof.Error="";proof.Required=true;if(DescribeReboot40(proof,boot,now.AddDays(2)).Required!=true)throw new Exception("Positive requirement expired");
  var signal=new CollectorActivity53("Reboot","Чтение",now.AddSeconds(-10).ToString("o"),now.ToString("o"),now);
  if(!signal.Animate(now)||signal.Animate(now.AddMinutes(1))||(signal with{Stage="Stopping"}).Animate(now))throw new Exception("Collector animation misrepresents liveness");
  foreach(var kind in new[]{"Disabled","AccessDenied","InvalidResult","RepairFailed","Cooldown","Busy"})if(RecoverableCollector53(new(kind,"fixture",0)))throw new Exception("Automatic recovery bypassed protected refusal: "+kind);
  if(RecoverableCollector53(new("Blocked","На клиенте более новая версия",0))||!RecoverableCollector53(new("Blocked","Три сбоя сборщика подряд: fixture",0))||!RecoverableCollector53(new("Unknown","Предыдущий исполнитель удерживает блокировку",120)))throw new Exception("Automatic collector recovery classification incorrect");
  var row=new ComputerRow(new(){Host="status53.fixture.invalid",Name="Status fixture"});Computers.Add(row);
  var pending=queue.ToList();var savedPlans=maintenancePlans.ToList();bool savedAuto=AutoServiceEnabled;var paused=TransportBudget41.Paused;
  try{
   PollingControl43.Resume();
   var selected53=ComputerGrid.SelectedItem;var tab53=MainTabs.SelectedItem;int theme53=Theme.Current;double width53=Width,height53=Height;
   try{ComputerGrid.SelectedItem=row;MainTabs.SelectedItem=WsusTab;
    RemoteService.WithCollectorActivity53(ConnectionStore.Capture(row.Target),signal,()=>{
     foreach(int theme in new[]{0,1,2})foreach(var size in new[]{(1480d,940d),(1000d,640d)}){
      Theme.Apply(theme,false);Width=size.Item1;Height=size.Item2;RefreshCollector44(row.Host);UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();
      if(CollectorStage53.Visibility!=System.Windows.Visibility.Visible||!CollectorBusy53.IsIndeterminate||!CollectorNoticeText43.Text.Contains("Последний сигнал"))throw new Exception("Collector stage/liveness missing from header");
      CaptureContent38("collector53-"+theme+"-"+(size.Item1>1000?"wide":"compact"),size.Item1,size.Item2,true);
      CollectorDetails54_Click(this,new());UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
      if(!CollectorPopup54.IsOpen||CollectorNotice43.Visibility!=System.Windows.Visibility.Visible)throw new Exception("Collector details popup did not open beside connection");
      var popup54=(System.Windows.FrameworkElement)CollectorPopup54.Child;popup54.UpdateLayout();
      if(popup54.ActualWidth<100||popup54.ActualHeight<50)throw new Exception("Collector popup has no rendered content");
      var image54=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(popup54.ActualWidth),(int)Math.Ceiling(popup54.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);image54.Render(popup54);
      var encoder54=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder54.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image54));
      using(var file54=File.Create(Path.Combine(Path.GetTempPath(),"ui-collector54-popup-"+theme+"-"+(size.Item1>1000?"wide":"compact")+".png")))encoder54.Save(file54);CollectorPopup54.IsOpen=false;
     }
    });
   }finally{Theme.Apply(theme53,false);Width=width53;Height=height53;ComputerGrid.SelectedItem=selected53;MainTabs.SelectedItem=tab53;ShowCurrent();}
   var beforeInstall=now.AddMinutes(-1).ToString("o");
   ObserveInstaller53(row,new(){CollectedUtc=now.ToString("o"),Busy=true,InstallationEvidence="TiWorker"});
   if(InstallerChanged53(row,beforeInstall))throw new Exception("Download/process alone invalidated reboot flags");
   ObserveInstaller53(row,new(){CollectedUtc=now.ToString("o"),InstallerBusy=true});
   installerObservations53[row.Host]=Store.Clone(installerObservations53[row.Host]);
   if(!InstallerChanged53(row,beforeInstall)||InstallerChanged53(row,now.AddSeconds(1).ToString("o")))throw new Exception("External installation/restart/fresh flag ordering failed");
   var server=OverviewServer;
   var report=new WsusReport{LastReportedUtc=now.ToString("o"),LastSyncResult="Succeeded",NeededCount=0,RequiredComplete=true};
   if(!ApplyOverviewReply53(row,new(){Host=row.Host,Found=true,Report=report},server))throw new Exception("Successful report rejected");
   var saved=reportCache[row.Host];
   foreach(var reply in new OverviewReply?[]{null,new(){Found=false},new(){Found=true},new(){Error="Access denied"}}){
    if(ApplyOverviewReply53(row,reply,server)||!ReferenceEquals(saved,reportCache[row.Host])||!overviewErrors.ContainsKey(row.Host))throw new Exception("Failed lookup erased previous report or lacked reason");
   }
   if(!ApplyOverviewReply53(row,new(){Found=true,Report=report},server)||overviewErrors.ContainsKey(row.Host))throw new Exception("New success did not resolve lookup failure");
   var reboot=new RebootView40(false,true,now.ToString("o"),Array.Empty<string>(),"");
   PreserveReady43(row,new("Success","Проверено",""),false,null,saved,reboot,"scan");
   if(PreserveReady43(row,new("Working","Выполняется",""),true,null,saved,reboot,"scan").Kind!="Working"||!readyProofs43.ContainsKey(row.Host))throw new Exception("Transient work erased historical proof or was hidden");
   if(PreserveReady43(row,new("Error","Ошибка",""),false,null,saved,reboot,"scan").Kind!="Error")throw new Exception("Saved proof hides current error");
   if(PreserveReady43(row,new("Stale","Снимок",""),false,null,saved,reboot,"scan").Kind!="Success")throw new Exception("Historical success cannot be restored");
   var target=ConnectionStore.Capture(row.Target);var watch=new RebootWatch{JobId=Guid.NewGuid().ToString(),Confirmed=true,ExpectedUtc=now.AddMinutes(-5)};rebootWatches[row.Host]=watch;
   readFailures34[row.Host+"|флаги и аптайм"]=new(){Next=DateTimeOffset.MaxValue,Error="blocked"};
   int resets=0;Task<string> Reset(TargetRecord _){resets++;return Task.FromResult("Stopped");}
   await TryResetRebootPoll51(row.Host,watch,target,Reset);await TryResetRebootPoll51(row.Host,watch,target,Reset);
   if(resets!=1||!watch.PollResetAttempted51||watch.Next<=now||!ReadDue34(row.Host+"|флаги и аптайм"))throw new Exception("Post-boot collector recovery is not bounded/durable");
   watch.PollResetAttempted51=false;PollingControl43.Pause();await TryResetRebootPoll51(row.Host,watch,target,Reset);if(resets!=1)throw new Exception("Master OFF launched recovery");
   PollingControl43.Resume();AutoServiceEnabled=true;maintenancePlans.Clear();
   var plan=new MaintenancePlan{Host=row.Host,Identity=target.ConnectionIdentity,Phase="Ожидается перезагрузка",BootUtc=boot,Install=true};maintenancePlans[row.Host]=plan;
   await RemoteService.WithCollectorWait53(target,new("Blocked","Три сбоя сборщика подряд: fixture",0),async()=>{
    int planResets=0;Task<string> ResetPlan(TargetRecord _){planResets++;return Task.FromResult("Stopped");}
    repairingCollectors47.Add(row.Host);
    await AdvanceMaintenance(_=>throw new Exception("Concurrent recovery should not read"),_=>throw new Exception("Concurrent recovery should not inspect installation"),false,resetProbe53:ResetPlan);
    if(planResets!=0||plan.PollRecoveryAttempted53)throw new Exception("Parallel collector recovery was not joined");
    repairingCollectors47.Remove(row.Host);plan.Next=default;
    await AdvanceMaintenance(_=>throw new Exception("Blocked snapshot should not run"),_=>throw new Exception("Blocked installer check should not run"),false,resetProbe53:ResetPlan);
    plan=Store.Clone(plan);maintenancePlans[row.Host]=plan;plan.Next=default;
    await AdvanceMaintenance(_=>throw new Exception("Exhausted recovery should not run"),_=>throw new Exception("Unexpected installer check"),false,resetProbe53:ResetPlan);
    if(planResets!=1||!plan.PollRecoveryAttempted53)throw new Exception("Maintenance collector recovery repeated across restart");
   });
   if(queue.Count!=pending.Count)throw new Exception("Status observation launched work");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"ui-release53.txt"),"PASS: same-boot flags at 6m/12h/1d and JSON restart; new boot/partial/positive; heartbeat animation; missing/partial/error WSUS preserves report; new success clears limitation; historical proof survives work but not current error; one durable recovery; OFF; no update submission");
  }finally{
   maintenancePlans.Clear();foreach(var entry in savedPlans)maintenancePlans[entry.Key]=entry.Value;AutoServiceEnabled=savedAuto;SaveMaintenance();
   readFailures34.Remove(row.Host+"|флаги и аптайм");readFailures34.Remove(row.Host+"|фоновая активность");
   installerObservations53.Remove(row.Host);Store.Write("installer-observations53.json",installerObservations53);
   Computers.Remove(row);reportCache.Remove(row.Host);serverReports.Remove(row.Host);reportSeverity.Remove(row.Host);overviewErrors.Remove(row.Host);liveReports.Remove(row.Host);readyProofs43.Remove(row.Host);rebootWatches.Remove(row.Host);
   Store.Write("ready-proofs43.json",readyProofs43);Store.Write("wsus-report-cache.json",reportCache);Store.Write("reboot-watches.json",rebootWatches);
   if(paused)PollingControl43.Pause();else PollingControl43.Resume();
  }
 }
}

public sealed partial class RemoteService {
 internal static void WithCollectorActivity53(TargetRecord target,CollectorActivity53 activity,Action test){string key=TransportBudget41.Key(target);var old=collectorActivity53.GetValueOrDefault(key);collectorActivity53[key]=activity;try{test();}finally{if(old==null)collectorActivity53.TryRemove(key,out _);else collectorActivity53[key]=old;}}
 internal static async Task WithCollectorWait53(TargetRecord target,PollWait42 wait,Func<Task> test){
  string key=TransportBudget41.Key(target);var old=snapshotWait42.GetValueOrDefault(key);snapshotWait42[key]=wait;
  try{await test();}finally{if(old==null)snapshotWait42.TryRemove(key,out _);else snapshotWait42[key]=old;}
 }
}
