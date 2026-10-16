using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 DateTimeOffset nextRebootRead=DateTimeOffset.MinValue;bool readingReboot;
 async Task ObserveCurrentReboot(){if(readingReboot||closing||DateTimeOffset.UtcNow<nextRebootRead||ComputerGrid.SelectedItem is not ComputerRow row)return;nextRebootRead=DateTimeOffset.UtcNow.AddMinutes(1);var t=ConnectionStore.Capture(row.Target);var address=ConnectionStore.Address(t);if(address.Length>0&&!ConnectionStore.HasCredential(address,ConnectionStore.Ssl(t)))return;readingReboot=true;try{var snapshot=await remote.ReadRebootState(t);if(closing)return;rebootSnapshots[row.Host]=snapshot;Store.Write("reboot-snapshots.json",rebootSnapshots);RefreshOutcomes();if(IsCurrent(row))ShowCurrent();}catch{/* Connection state is managed by the dedicated probe; no stale flag is cleared here. */}finally{readingReboot=false;}}
 async Task ObserveRebootAfterJob(ComputerRow row,RemoteJob? job,PollResult data){var state=data.State;if(state==null||IsCurrentExecution(state.Status)||state.Ended.Length==0)return;
  if(state.RebootEvidence==null&&wsusChecked.Add((job?.Id??row.JobId)+row.Host+"reboot")){try{var observed=await remote.ReadRebootState(row.Target);state.RebootEvidence=new(){Required=observed.Required,CollectedUtc=observed.CollectedUtc,Sources=observed.Sources,Error=observed.Error};}catch(Exception ex){state.RebootEvidence=new(){CollectedUtc=DateTime.UtcNow.ToString("o"),Error=ShortConnectionError(ex.Message)};}}
  if(state.RebootEvidence!=null&&(!rebootSnapshots.TryGetValue(row.Host,out var old)||string.CompareOrdinal(state.RebootEvidence.CollectedUtc,old.CollectedUtc)>0)){var e=state.RebootEvidence;rebootSnapshots[row.Host]=new(){Required=e.Required,CollectedUtc=e.CollectedUtc,Sources=e.Sources,Error=e.Error};Store.Write("reboot-snapshots.json",rebootSnapshots);}
 }
 string ReportDisplayStatus(string host){if(Computers.FirstOrDefault(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase)) is ComputerRow row&&RebootReason(row).Length>0)return "Reboot";return reportSeverity.GetValueOrDefault(host,"Unknown");}
}
