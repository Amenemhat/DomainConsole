using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 DateTimeOffset nextRebootRead=DateTimeOffset.MinValue;bool readingReboot;
 async Task ObserveCurrentReboot(){if(readingReboot||closing||DateTimeOffset.UtcNow<nextRebootRead||ComputerGrid.SelectedItem is not ComputerRow row)return;nextRebootRead=DateTimeOffset.UtcNow.AddMinutes(1);readingReboot=true;try{await ReadClientStatus(row);if(!closing){RefreshOutcomes();if(IsCurrent(row))ShowCurrent();}}finally{readingReboot=false;}}

 async Task ObserveRebootAfterJob(ComputerRow row,RemoteJob? job,PollResult data){var state=data.State;if(!CurrentIdentity(row.Target))return;if(state==null||IsCurrentExecution(state.Status)||state.Ended.Length==0)return;
  if(state.RebootEvidence==null&&wsusChecked.Add((job?.Id??row.JobId)+row.Host+"reboot")){try{var observed=await remote.ReadRebootState(row.Target);state.RebootEvidence=new(){Required=observed.Required,CollectedUtc=observed.CollectedUtc,Sources=observed.Sources,Error=observed.Error};}catch(Exception ex){state.RebootEvidence=new(){CollectedUtc=DateTime.UtcNow.ToString("o"),Error=ShortConnectionError(ex.Message)};}}
  if(state.RebootEvidence!=null&&(!rebootSnapshots.TryGetValue(row.Host,out var old)||string.CompareOrdinal(state.RebootEvidence.CollectedUtc,old.CollectedUtc)>0)){var e=state.RebootEvidence;rebootSnapshots[row.Host]=new(){Required=e.Required,CollectedUtc=e.CollectedUtc,Sources=e.Sources,Error=e.Error,BootUtc=old?.BootUtc??"",ClientUtc=old?.ClientUtc??"",UptimeSeconds=old?.UptimeSeconds,UptimeReceivedUtc=old?.UptimeReceivedUtc??"",UptimeCollectedUtc=old?.UptimeCollectedUtc??old?.CollectedUtc??""};Store.Write("reboot-snapshots.json",rebootSnapshots);}
 }
 void ApplyPostBootResult(ComputerRow row,PollResult data){var state=data.State;if(state==null||state.Status is not ("AwaitingReboot" or "RebootScheduled"))return;if(!rebootSnapshots.TryGetValue(row.Host,out var proof)||proof.Required!=false||!ObservedAfter(proof,state.Ended)||!DateTimeOffset.TryParse(proof.BootUtc,out var boot)||!DateTimeOffset.TryParse(state.Ended,out var end)||boot<=end)return;state.Status="VerifiedAfterReboot";state.RebootRequired=false;state.Updated=proof.CollectedUtc;state.RebootEvidence=new(){Required=false,CollectedUtc=proof.CollectedUtc};}
 string ReportDisplayStatus(string host){if(Computers.FirstOrDefault(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase)) is ComputerRow row&&RebootReason(row).Length>0)return "Reboot";return reportSeverity.GetValueOrDefault(host,"Unknown");}
}
