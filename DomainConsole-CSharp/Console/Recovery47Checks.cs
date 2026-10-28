using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyRecoveryPipeline47(){
  var plans=maintenancePlans.ToList();var oldQueue=queue.ToList();var oldJobs=History.ToList();bool auto=AutoServiceEnabled;bool tick=timer.IsEnabled;timer.Stop();
  var row=new ComputerRow(new(){Host="recovery-pipeline47.fixture.invalid",Name="Recovery fixture"});var selection=ComputerGrid.SelectedItem;var tab=MainTabs.SelectedItem;
  Computers.Add(row);maintenancePlans.Clear();queue.Clear();
  try{
   AutoServiceEnabled=true;var target=ConnectionStore.Capture(row.Target);target.Status="InterventionRequired";
   var state=new RemoteState{Status="Running",HasUpdateSteps=true,ExecutorPid37=int.MaxValue,ExecutorStarted37=DateTimeOffset.UtcNow.AddMinutes(-10).ToString("o"),Started=DateTimeOffset.UtcNow.AddMinutes(-10).ToString("o"),Updated=DateTimeOffset.UtcNow.AddMinutes(-3).ToString("o")};
   var data=new PollResult{State=state,AgentAlive=false,TaskState="Ready",BootUtc=DateTimeOffset.UtcNow.AddDays(-1).ToString("o")};Reconcile(data);target.State=data.State;
   var job=new RemoteJob{OutputChannel="WSUS",Targets=new(){target}};History.Insert(0,job);BeginMaintenance(job);ReconcilePlan44(row.Host,job.Id,data);
   var p=maintenancePlans[row.Host];if(!p.RecoveryCheck47||p.Done)throw new Exception("Same-boot poll discarded recoverable plan");
   p.ReadyAfter=DateTimeOffset.UtcNow.AddSeconds(-1);p.Next=DateTimeOffset.UtcNow;
   int releases=0,probes=0;
   Task<RebootSnapshot?> Status(ComputerRow r){probes++;return Task.FromResult<RebootSnapshot?>(new(){Required=false,BootUtc=data.BootUtc,CollectedUtc=DateTimeOffset.UtcNow.ToString("o")});}
   await AdvanceMaintenance(Status,t=>Task.FromResult(new LiveUpdateActivity()),false,(t,id)=>{releases++;return Task.FromResult(false);});
   if(queue.Count!=0||p.JobId!=job.Id||probes!=0)throw new Exception("Unconfirmed release allowed recovery");
   p.Next=DateTimeOffset.UtcNow;
   await AdvanceMaintenance(Status,t=>Task.FromResult(new LiveUpdateActivity{InstallerBusy=true}),false,(t,id)=>{releases++;return Task.FromResult(true);});
   if(queue.Count!=0||p.JobId.Length!=0||!p.RecoveryCheck47)throw new Exception("Busy installer allowed new search or lost recovery state");
   p.Next=DateTimeOffset.UtcNow;
   await AdvanceMaintenance(Status,t=>Task.FromResult(new LiveUpdateActivity()),false,(t,id)=>throw new Exception("Released plan checked twice"));
   if(queue.Count!=1||queue.Peek().Job.Steps.Single().Kind!="UpdateScan"||releases!=2||probes<2)throw new Exception("Recovery pipeline did not produce exactly one fresh search");
   p.Next=DateTimeOffset.UtcNow;await AdvanceMaintenance(Status,t=>throw new Exception("Active search probed again"),false);
   if(queue.Count!=1||!ReferenceEquals(ComputerGrid.SelectedItem,selection)||MainTabs.SelectedItem!=tab)throw new Exception("Recovery duplicated search or stole navigation");
  }finally{
   maintenancePlans.Clear();foreach(var x in plans)maintenancePlans[x.Key]=x.Value;
   queue.Clear();foreach(var x in oldQueue)queue.Enqueue(x);History.Clear();foreach(var x in oldJobs)History.Add(x);Computers.Remove(row);AutoServiceEnabled=auto;SaveMaintenance();Save();if(tick)timer.Start();
  }
 }
}
