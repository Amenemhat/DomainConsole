using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 DateTimeOffset nextRebootRead=DateTimeOffset.MinValue;bool readingReboot;
 async Task ObserveCurrentReboot(){if(TransportBudget41.Paused||readingReboot||closing||DateTimeOffset.UtcNow<nextRebootRead||ComputerGrid.SelectedItem is not ComputerRow row)return;if(AssessClient41(row).Kind=="Success")return;nextRebootRead=DateTimeOffset.UtcNow.AddMinutes(5);readingReboot=true;try{await ReadClientStatus(row);if(!closing){RefreshOutcomes();if(IsCurrent(row))ShowCurrent();}}finally{readingReboot=false;}}

 async Task ObserveRebootAfterJob(ComputerRow row,RemoteJob? job,PollResult data,Func<TargetRecord,Task<RebootSnapshot>>? readOverride47=null){var state=data.State;var target47=ConnectionStore.Capture(row.Target);var generation47=PollingControl43.Token;bool Current47()=>!closing&&!TransportBudget41.Paused&&!generation47.IsCancellationRequested&&CurrentEndpoint(target47);if(!Current47()||!CurrentIdentity(row.Target))return;if(state==null||IsCurrentExecution(state.Status)||state.Ended.Length==0)return;
  var checkKey47=(job?.Id??row.JobId)+row.Host+"reboot";if((state.RebootEvidence==null||QueueOnly32(state.RebootEvidence.Sources))&&wsusChecked.Add(checkKey47)){try{var observed=await (readOverride47!=null?readOverride47(target47):remote.ReadRebootState(target47));if(!Current47()){wsusChecked.Remove(checkKey47);return;}state.RebootEvidence=new(){Required=observed.Required,CollectedUtc=observed.CollectedUtc,Sources=observed.Sources,PendingFileOperations=observed.PendingFileOperations,ProbeVersion=observed.ProbeVersion,Error=observed.Error};ReconcileRebootState(state,observed);}catch(Exception ex){if(!Current47()){wsusChecked.Remove(checkKey47);return;}state.RebootEvidence=new(){CollectedUtc=DateTime.UtcNow.ToString("o"),Error=ShortConnectionError(ex.Message)};}}
  if(state.RebootEvidence!=null&&(!rebootSnapshots.TryGetValue(row.Host,out var old)||string.CompareOrdinal(state.RebootEvidence.CollectedUtc,old.CollectedUtc)>0)){var e=state.RebootEvidence;ApplyClientSnapshot(row.Host,new(){Required=e.Required,CollectedUtc=e.CollectedUtc,Sources=e.Sources,PendingFileOperations=e.PendingFileOperations,ProbeVersion=e.ProbeVersion,Error=e.Error,BootUtc=data.BootUtc.Length>0?data.BootUtc:old?.BootUtc??"",ClientUtc=data.ClientUtc});}

 }
 static void ReconcileRebootState(RemoteState state,RebootSnapshot snapshot){
  if(state.Status is not ("AwaitingReboot" or "RebootScheduled" or "RebootObserved"))return;
  if(!DateTimeOffset.TryParse(snapshot.CollectedUtc,out var collected)||!DateTimeOffset.TryParse(state.Ended,out var ended)||collected<ended)return;
  bool returned=DateTimeOffset.TryParse(snapshot.BootUtc,out var boot)&&boot>ended;
  if(!returned&&snapshot.Required!=false)return;
  // New positive flags are a fresh requirement and must not be cleared by observing a boot alone.
  if(snapshot.Required==true)return;
  state.Status=returned?(snapshot.Required==false?"VerifiedAfterReboot":"RebootObserved"):"Completed";
  state.RebootRequired=false;state.Updated=snapshot.CollectedUtc;state.RebootEvidence=new(){Required=snapshot.Required,CollectedUtc=snapshot.CollectedUtc,Sources=snapshot.Sources,PendingFileOperations=snapshot.PendingFileOperations,ProbeVersion=snapshot.ProbeVersion,Error=snapshot.Error};
 }
 void ReconcileRebootHistory(string host,RebootSnapshot snapshot){bool changed=false;foreach(var target in History.SelectMany(j=>j.Targets).Where(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&CurrentIdentity(t)&&t.State!=null)){
  var before=target.State!.Status;ReconcileRebootState(target.State,snapshot);if(target.State.Status!=before){target.Status=target.State.Status;changed=true;}
 }if(changed){Save();foreach(var row in Computers.Where(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase))){var target=History.FirstOrDefault(j=>j.Id==row.JobId)?.Targets.FirstOrDefault(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase));if(target!=null){row.Target.Status=target.Status;row.Target.State=target.State;row.Refresh();}}}}
 void ApplyPostBootResult(ComputerRow row,PollResult data){if(data.State!=null&&rebootSnapshots.TryGetValue(row.Host,out var snapshot))ReconcileRebootState(data.State,snapshot);}
 string ReportDisplayStatus(string host){if(Computers.FirstOrDefault(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase)) is ComputerRow row)return OverviewStatus(row).Kind;return reportSeverity.GetValueOrDefault(host,"Unknown");}
}

