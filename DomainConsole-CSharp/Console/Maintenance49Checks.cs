using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyMaintenance49(){
  var savedPlans=maintenancePlans.ToList();var savedJobs=History.ToList();var savedQueue=queue.ToList();
  bool auto=AutoServiceEnabled,tick=timer.IsEnabled,paused=TransportBudget41.Paused;timer.Stop();
  var row=new ComputerRow(new(){Host="maintenance49.fixture.invalid",Name="Maintenance fixture"});Computers.Add(row);
  try{
   PollingControl43.Resume();AutoServiceEnabled=true;
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
