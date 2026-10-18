using System.IO;
using System.Text;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease31(){
  if(!ExecutionDiagnosis.Explain(unchecked((int)0x800F0988)).Contains("PSFX_E_INVALID_DELTA_COMBINATION"))throw new Exception("PSFX explanation not embedded");
  if(ExecutionDiagnosis.Explain(1).Contains("S_FALSE"))throw new Exception("Exit code 1 decoded as HRESULT");
  var row=Computers.Last();var old=clientConnections.GetValueOrDefault(row.Host);var watch=rebootWatches.GetValueOrDefault(row.Host);var plan=maintenancePlans.GetValueOrDefault(row.Host);var jobs=History.ToList();var queued=queue.ToList();bool automatic=AutoServiceEnabled;
  try{
   AutoServiceEnabled=true;clientConnections[row.Host]=new(){LastSuccess=DateTimeOffset.UtcNow.AddHours(-2),LastAttempt=DateTimeOffset.UtcNow.AddHours(-2)};if(LinkKind(row.Host)!="SavedConnected"||!LinkHint(row).Contains("сохранённый"))throw new Exception("Successful old connection became unknown");
   rebootWatches[row.Host]=new(){Done=true,Phase="STALE_REBOOT_TEXT"};if(PostRebootText(row.Host).Length!=0)throw new Exception("Finished reboot text remains in active header");
   maintenancePlans.Remove(row.Host);var w=new RebootWatch();EnsureFollowupJob(row.Host,w,row.Target);if(!maintenancePlans.TryGetValue(row.Host,out var p)||!p.Install||p.JobId!=w.FollowupId)throw new Exception("Reboot followup lacks global continuation");
   var target=History.First(j=>j.Id==p.JobId).Targets.Single();target.Status="Completed";target.State=new(){Status="Completed",Ended=DateTime.UtcNow.ToString("o"),UpdatesRemaining=1};p.Next=default;
   AdvanceMaintenance(_=>Task.FromResult<RebootSnapshot?>(new(){Required=false,BootUtc=DateTime.UtcNow.AddMinutes(-10).ToString("o"),CollectedUtc=DateTime.UtcNow.ToString("o")}),_=>Task.FromResult(new LiveUpdateActivity()),false).GetAwaiter().GetResult();if(History.First(j=>j.Id==p.JobId).Steps.Single().Kind!="Updates")throw new Exception("Successful scan did not advance to install");
   var data=new PollResult{State=new(){Status="Failed",Error="HResult 0x800F0988"},Updates=new(){new(){Title="fixture cumulative update",ResultCode=4,HResult=unchecked((int)0x800F0988)}}};var output=PresentResult("fixture.invalid",data);if(!output.Contains("PSFX_E_INVALID_DELTA_COMBINATION")||output.Split("Возможные причины:").Length!=2||output.Contains("Подробности смотрите в выводе"))throw new Exception("Duplicate or missing failure card");
   if(!ConnectionBrief31("The operation was canceled.").Contains("не отменено"))throw new Exception("Probe cancellation confused with task cancellation");
   var report=new WsusReport{NeededCount=2,RequiredComplete=true,ApprovedDownloadedCount=1,LastReportedUtc=DateTime.UtcNow.ToString("o"),LastSyncResult="Succeeded"};if(!report.Format("fixture","").Contains("К установке: 2")||report.Format("fixture","").Contains("Сводка сервера по всем обновлениям;"))throw new Exception("Required update counter missing");
  }finally{AutoServiceEnabled=automatic;if(old==null)clientConnections.Remove(row.Host);else clientConnections[row.Host]=old;if(watch==null)rebootWatches.Remove(row.Host);else rebootWatches[row.Host]=watch;if(plan==null)maintenancePlans.Remove(row.Host);else maintenancePlans[row.Host]=plan;History.Clear();foreach(var j in jobs)History.Add(j);queue.Clear();foreach(var q in queued)queue.Enqueue(q);SaveMaintenance();Save();}
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"cache-full31.ps1"),CacheScripts.Build("Full",false,false),new UTF8Encoding(true));var folder=Path.Combine(Path.GetTempPath(),"DomainConsole-smoke");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"required-updates31.ps1"),RemoteService.RequiredUpdatesScript31,new UTF8Encoding(true));
 }
}
