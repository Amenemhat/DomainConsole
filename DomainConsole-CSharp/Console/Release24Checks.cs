using System.IO;
using System.Text;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease24(){
  if(LikelyShell("$x = Get-Service bits")!="PowerShell"||LikelyShell("Get-CimInstance Win32_OperatingSystem | Select-Object Caption")!="PowerShell"||LikelyShell("echo %windir%")!="CMD"||LikelyShell("hostname")!=null||LikelyShell("powershell.exe -NoProfile -Command Get-Service")!=null)throw new Exception("Shell hint classification invalid");
  if(!IsIndependentControl(new(){new(){Kind="CMD",Code="shutdown.exe /r /t 10"}})||IsIndependentControl(new(){new(){Code="shutdown.exe /r /t 10 & hostname"}}))throw new Exception("Independent control classification invalid");
  var job=new RemoteJob{Created=DateTimeOffset.UtcNow.AddHours(-1).ToString("o"),Steps=new(){new(){Code="Get-Service bits",Kind="PowerShell"}},Targets=new(){new(){Host="alpha.invalid",Name="ALPHA"},new(){Host="beta.invalid",Name="BETA"}}};
  var entry=new HistoryEntry(job,job.Targets[1]);if(!HistoryMatches(entry,1,"beta","Get-Service",null)||HistoryMatches(entry,2,"beta","",null)||HistoryMatches(entry,1,"alpha","",null)||HistoryMatches(entry,0,"","","alpha.invalid"))throw new Exception("Per-client history filter invalid");
  var absent=new PollResult{TaskState="Missing",AgentAlive=false};ReconcileMissing(absent,job,DateTimeOffset.UtcNow);if(absent.State?.Status!="InterventionRequired")throw new Exception("Missing status retains active queue");
  var running=new PollResult{TaskState="Running",AgentAlive=false};ReconcileMissing(running,job,DateTimeOffset.UtcNow);if(running.State!=null)throw new Exception("Live scheduled task was discarded");
  var reboot=new PollResult{BootUtc=DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),TaskState="Ready",AgentAlive=false,State=new(){Started=job.Created,Status="Queued"}};Reconcile(reboot);if(reboot.State.Status!="InterventionRequired")throw new Exception("Reboot left dead queue active");
  var fresh=new PollResult{TaskState="Missing",AgentAlive=false};ReconcileMissing(fresh,new RemoteJob(),DateTimeOffset.UtcNow);if(fresh.State!=null)throw new Exception("New job has no startup grace");
  if(RecommendationRelevant(new(){Recommendation="old"},"Completed")||!RecommendationRelevant(new(){Recommendation="failure"},"Failed"))throw new Exception("Stale recommendation remains after success");
  if(!ExecutionDiagnosis.Explain(unchecked((int)0x8007000E)).Contains("память")||!ExecutionDiagnosis.Recommend(unchecked((int)0x8007000E)).Contains("подкачки"))throw new Exception("Memory error guidance missing");
  var row=Current();var currentJob=ChannelJob(row,MainTabs.SelectedItem==WsusTab);if(currentJob!=null){var target=currentJob.Targets.First(t=>t.Host==row.Host);var oldStatus=target.Status;var oldState=target.State;var oldBackground=backgroundUpdates.GetValueOrDefault(row.Host);try{target.Status="Completed";target.State=new(){Status="Completed",Ended=DateTimeOffset.UtcNow.ToString("o"),Recommendation="OLD"};backgroundUpdates[row.Host]=new(){CollectedUtc=DateTimeOffset.UtcNow.ToString("o"),Downloads=new(){new(){State="Transferring",Received=10,Total=100}}};UpdateWsusStage();if(CurrentProgress.Visibility!=Visibility.Collapsed||CurrentProgress.IsIndeterminate||BackgroundProgress.Visibility!=Visibility.Visible||RecommendationButton.Visibility!=Visibility.Collapsed)throw new Exception("Background transfer changes completed job progress");}finally{target.Status=oldStatus;target.State=oldState;if(oldBackground==null)backgroundUpdates.Remove(row.Host);else backgroundUpdates[row.Host]=oldBackground;UpdateWsusStage();}}
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"cancel-job.ps1"),RemoteService.CancellationScript("00000000-0000-0000-0000-000000000024","now"),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"memory-diagnostic.ps1"),MemoryDiagnosticsScript,new UTF8Encoding(true));
 }
}
