using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyMaintenance49(){
  var savedPlans=maintenancePlans.ToList();var savedJobs=History.ToList();var savedQueue=queue.ToList();
  bool auto=AutoServiceEnabled,tick=timer.IsEnabled,paused=TransportBudget41.Paused;timer.Stop();
  var row=new ComputerRow(new(){Host="maintenance49.fixture.invalid",Name="Maintenance fixture"});Computers.Add(row);
  try{
   PollingControl43.Pause();bool endpointBlocked49=false,endpointProgress49=false;
   try{await remote.ProbeEndpoint(new(){Host="paused49.invalid"},_=>endpointProgress49=true);}catch(OperationCanceledException){endpointBlocked49=true;}
   if(!endpointBlocked49||endpointProgress49)throw new Exception("Endpoint setup probe bypassed master OFF");
   PollingControl43.Resume();await VerifyEndpointDiagnostics49();
   PollingControl43.Resume();AutoServiceEnabled=true;
   if(!ForcedServiceActivity49(new(){InstallerBusy=true,CollectedUtc=DateTimeOffset.UtcNow.ToString("o")}).Contains("есть признаки установки")||!ForcedServiceActivity49(null).Contains("не подтверждена"))throw new Exception("Forced-service confirmation hid known/unknown installation activity");
   var candidate=new ServiceProcessCandidate{Pid=99999,CreatedUtc=DateTimeOffset.UtcNow.ToString("o"),Path="fixture",Services=new(){"bits"}};
   if(ForcedServiceSteps36(new[]{candidate,Store.Clone(candidate)}).Count!=1)throw new Exception("Same process was scheduled twice");
   var changed=Store.Clone(candidate);changed.Services.Add("wuauserv");bool rejected=false;
   try{ForcedServiceSteps36(new[]{candidate,changed});}catch(InvalidOperationException){rejected=true;}
   if(!rejected)throw new Exception("Conflicting process snapshots were silently merged");
   foreach(bool? required in new bool?[]{true,false,null}){
    maintenancePlans.Clear();History.Clear();queue.Clear();
    var target=ConnectionStore.Capture(row.Target);target.Status="Completed";target.State=new(){Status="Completed",Ended=DateTimeOffset.UtcNow.AddHours(-1).ToString("o"),UpdatesRemaining=1};
    var job=new RemoteJob{OutputChannel="WSUS",Targets=new(){target},Steps=new(){new(){Kind="Updates"}}};History.Add(job);BeginMaintenance(job);
    var plan=maintenancePlans[row.Host];plan.Phase="Ожидается перезагрузка";plan.BootUtc=DateTimeOffset.UtcNow.AddDays(-1).ToString("o");
    var boot=DateTimeOffset.UtcNow.AddMinutes(-10).ToString("o");
    Task<RebootSnapshot?> Snapshot(ComputerRow _)=>Task.FromResult<RebootSnapshot?>(new(){Required=required,BootUtc=boot,CollectedUtc=DateTimeOffset.UtcNow.ToString("o")});
    await AdvanceMaintenance(Snapshot,_=>Task.FromResult(new LiveUpdateActivity()),false);
    if(!plan.SearchAfterBoot49||queue.Count!=0)throw new Exception("New boot bypassed readiness/search checkpoint");
    // Simulate restart with the actual serialized continuation intent.
    plan=Store.Clone(plan);maintenancePlans[row.Host]=plan;plan.ReadyAfter=default;plan.Next=default;
    await AdvanceMaintenance(Snapshot,_=>Task.FromResult(new LiveUpdateActivity{InstallerBusy=true}),false);
    if(queue.Count!=0||!plan.SearchAfterBoot49||plan.Done)throw new Exception("Busy WUA consumed post-boot checkpoint");
    plan.Next=default;await AdvanceMaintenance(Snapshot,_=>Task.FromResult(new LiveUpdateActivity()),false);
    if(queue.Count!=1||queue.Peek().Job.Steps.Single().Kind!="UpdateScan"||plan.SearchAfterBoot49)throw new Exception("Post-boot recovery did not perform one diagnostic search for true/false/unknown flags");
    plan.Next=default;await AdvanceMaintenance(Snapshot,_=>throw new Exception("Duplicate readiness call"),false);
    if(queue.Count!=1)throw new Exception("Post-boot search duplicated");
   }
   maintenancePlans.Clear();History.Clear();queue.Clear();
   var incompatible49=new RemoteJob{Steps=new(){new(){Kind="Updates"}},Targets=new(){new(){Host=row.Host,Status="Queued"}}};History.Add(incompatible49);
   var checkpoint49=new MaintenancePlan{Host=row.Host,Install=true,SearchAfterBoot49=true,NextJobId=incompatible49.Id};maintenancePlans[row.Host]=checkpoint49;
   QueueMaintenanceJob(checkpoint49,ConnectionStore.Capture(row.Target));
   if(queue.Count!=0||!checkpoint49.Done||!checkpoint49.Detail.Contains("контрольной точке"))throw new Exception("Post-boot checkpoint reused a saved installation instead of a search");
   var savedWatches49=rebootWatches.ToList();var toggle49=AutoServiceToggle.IsChecked;
   try{
    rebootWatches.Clear();rebootWatches[row.Host]=new(){Scan=true,Done=false};rebootWatches[row.Host+".old"]=new(){Scan=true,Done=true};
    AutoServiceToggle.IsChecked=false;AutoService_Changed(AutoServiceToggle,new System.Windows.RoutedEventArgs());
    var persisted49=Store.Read("reboot-watches.json",new Dictionary<string,RebootWatch>());
    if(persisted49.Count!=2||persisted49.Values.Any(w=>w.Scan))throw new Exception("Auto OFF lost reboot cancellation across restart");
   }finally{rebootWatches.Clear();foreach(var w in savedWatches49)rebootWatches[w.Key]=w.Value;Store.Write("reboot-watches.json",rebootWatches);AutoServiceToggle.IsChecked=toggle49;}
   var preservedWatches49=rebootWatches.ToList();
   try{
    foreach(string change in new[]{"disabled","replaced","generation","none"}){
     maintenancePlans.Clear();History.Clear();queue.Clear();AutoServiceEnabled=false;PollingControl43.Resume();
     var watch49=new RebootWatch{Scan=true};rebootWatches[row.Host]=watch49;
     var ready49=new TaskCompletionSource<LiveUpdateActivity>(TaskCreationOptions.RunContinuationsAsynchronously);
     var pending49=ContinueAfterReboot(row.Host,watch49,ConnectionStore.Capture(row.Target),_=>ready49.Task);
     if(change=="disabled")watch49.Scan=false;
     if(change=="replaced")rebootWatches[row.Host]=new(){Scan=true};
     if(change=="generation"){PollingControl43.Pause();PollingControl43.Resume();}
     ready49.SetResult(new());await pending49;
     if(queue.Count!=(change=="none"?1:0))throw new Exception("Obsolete reboot readiness dispatched follow-up: "+change);
    }
   }finally{rebootWatches.Clear();foreach(var w in preservedWatches49)rebootWatches[w.Key]=w.Value;Store.Write("reboot-watches.json",rebootWatches);}
   foreach(string change in new[]{"auto","endpoint","generation","plan","none"}){
    maintenancePlans.Clear();History.Clear();queue.Clear();AutoServiceEnabled=true;PollingControl43.Resume();ConnectionStore.Profiles.Remove(row.Host);
    var target=ConnectionStore.Capture(row.Target);target.Status="Completed";target.State=new(){Status="Completed",Ended=DateTimeOffset.UtcNow.ToString("o"),UpdatesRemaining=1};
    var job=new RemoteJob{OutputChannel="WSUS",Targets=new(){target}};History.Add(job);BeginMaintenance(job);
    var plan=maintenancePlans[row.Host];var gate=new TaskCompletionSource<LiveUpdateActivity>();bool entered=false;
    var pending=AdvanceMaintenance(_=>Task.FromResult<RebootSnapshot?>(new(){Required=false,BootUtc=DateTimeOffset.UtcNow.AddDays(-1).ToString("o"),CollectedUtc=DateTimeOffset.UtcNow.ToString("o")}),_=>{entered=true;return gate.Task;},false);
    if(!entered)throw new Exception("Maintenance race fixture did not reach readiness");
    if(change=="auto")AutoServiceEnabled=false;
    if(change=="endpoint")ConnectionStore.Profiles[row.Host]=new(){Address="192.0.2.49",AuthMode="Current"};
    if(change=="generation"){PollingControl43.Pause();PollingControl43.Resume();}
    if(change=="plan")maintenancePlans[row.Host]=Store.Clone(plan);
    gate.SetResult(new());await pending;
    if(queue.Count!=(change=="none"?1:0))throw new Exception("Obsolete maintenance response queued work: "+change);
   }
  }finally{
   ConnectionStore.Profiles.Remove(row.Host);Computers.Remove(row);maintenancePlans.Clear();foreach(var p in savedPlans)maintenancePlans[p.Key]=p.Value;
   History.Clear();foreach(var j in savedJobs)History.Add(j);queue.Clear();foreach(var q in savedQueue)queue.Enqueue(q);
   AutoServiceEnabled=auto;if(paused)PollingControl43.Pause();else PollingControl43.Resume();SaveMaintenance();Save();if(tick)timer.Start();
  }
 }
}
