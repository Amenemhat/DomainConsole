using System.IO;
using System.Text;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyAutomaticRecovery30(){
  var now=DateTimeOffset.UtcNow;var oldReport=new WsusReport{LastReportedUtc=now.AddDays(-4).ToString("o")};var client=new ClientReportProbe{CollectedUtc=now.ToString("o"),UseWUServer=1,WUServer="http://wsus.invalid:8530",WUStatusServer="http://wsus.invalid:8530",Queues=new(){new(){Name="EventCache"},new(){Name="EventCache.v2",Files=120,Bytes=5000,OldestUtc=now.AddDays(-3).ToString("o")}}};
  if(!ReportRecoveryPolicy30.Recommended(client,oldReport,24,now))throw new Exception("Confirmed backlog not eligible");
  if(ReportRecoveryPolicy30.Recommended(client,new(){LastReportedUtc=now.ToString("o")},24,now))throw new Exception("Recent report permits cleanup");
  var partial=Store.Clone(client);partial.QueueError="Access denied";if(ReportRecoveryPolicy30.Recommended(partial,oldReport,24,now))throw new Exception("Incomplete queue permits cleanup");
  partial=Store.Clone(client);partial.Queues[1].Files=0;if(ReportRecoveryPolicy30.Recommended(partial,oldReport,24,now))throw new Exception("Empty queue permits cleanup");
  partial=Store.Clone(client);partial.Queues[1].OldestUtc=now.AddHours(-1).ToString("o");if(ReportRecoveryPolicy30.Recommended(partial,oldReport,24,now))throw new Exception("New events permit cleanup");
  partial=Store.Clone(client);partial.CollectedUtc=now.AddHours(-1).ToString("o");if(ReportRecoveryPolicy30.Recommended(partial,oldReport,24,now))throw new Exception("Stale client probe permits cleanup");
  var previous=new ReportRecovery30{CreatedUtc=now.AddDays(-2).ToString("o"),Done=true};if(ReportRecoveryPolicy30.NewEpisode(previous,oldReport.LastReportedUtc,now)||!ReportRecoveryPolicy30.NewEpisode(previous,now.AddDays(-1).ToString("o"),now))throw new Exception("Report episode suppression invalid");
  var row=Computers.Last();var savedHistory=History.ToList();var savedQueue=queue.ToList();var savedRecords=reportRecoveries.ToList();var cached=reportCache.GetValueOrDefault(row.Host);var reboot=rebootSnapshots.GetValueOrDefault(row.Host);bool enabled=AutomaticReportRecoveryEnabled;bool disabled=row.Target.AdDisabled;var watched=rebootWatches.GetValueOrDefault(row.Host);
  try{
   History.Clear();queue.Clear();reportRecoveries.Clear();reportCache.Remove(row.Host);rebootWatches.Remove(row.Host);rebootSnapshots[row.Host]=new(){Required=false,CollectedUtc=now.ToString("o"),BootUtc=now.AddDays(-5).ToString("o")};row.Target.AdDisabled=false;Store.Write("automatic-report-recovery-enabled.json",true);
   var trigger=new RemoteJob{OutputChannel="WSUS",Steps=new(){new(){Kind="UpdateScan"}},Targets=new(){ConnectionStore.Capture(row.Target)}};trigger.Targets[0].Status="Completed";trigger.Targets[0].State=new(){Status="Completed",ReportRequested=true,Ended=now.ToString("o")};History.Add(trigger);
   ScheduleReportRecovery(row,oldReport,client);ScheduleReportRecovery(row,oldReport,client);if(reportRecoveries.Count!=1)throw new Exception("Recommendation scheduling duplicates");var record=reportRecoveries[row.Host];
   Task<WsusReport> Report(TargetRecord _)=>Task.FromResult(oldReport);Task<ClientReportProbe> Queue(TargetRecord _)=>Task.FromResult(client);Task<LiveUpdateActivity> Busy(TargetRecord _)=>Task.FromResult(new LiveUpdateActivity{InstallerBusy=true});Task<LiveUpdateActivity> Ready(TargetRecord _)=>Task.FromResult(new LiveUpdateActivity());
   AdvanceReportRecovery(Report,Queue,Busy,false).GetAwaiter().GetResult();if(record.JobId.Length>0||queue.Count>0)throw new Exception("Busy installer permits auto repair");record.Next=default;
   AdvanceReportRecovery(Report,Queue,Ready,false).GetAwaiter().GetResult();var repair=History.Single(j=>j.Id==record.JobId);if(repair.AutoReboot||repair.Steps.Any(s=>s.Kind=="Updates")||repair.Steps.Last().Kind!="UpdateScan"||!repair.ExclusiveMaintenance)throw new Exception("Auto recovery installs/reboots or loses scan");
   if(maintenancePlans.Values.Any(p=>p.JobId==repair.Id))throw new Exception("Report-only recovery acquired an installation plan");
   var id=record.JobId;QueueReportRecovery(record);reportRecoveries[row.Host]=Store.Clone(record);RestoreReportRecoveryQueues();if(History.Count(j=>j.Id==id)!=1||queue.Count(q=>q.Job.Id==id)!=1)throw new Exception("Restart duplicates recovery");if(CanTriggerReportRecovery(repair,row.Host))throw new Exception("Recovery search recursively triggers cleanup");
   record=reportRecoveries[row.Host];repair.Targets[0].Status="Completed";repair.Targets[0].State=new(){Status="Completed",Started=now.AddMinutes(-1).ToString("o"),Ended=now.ToString("o")};record.Next=default;
   Store.Write("automatic-report-recovery-enabled.json",false);AdvanceReportRecovery(Report,Queue,Ready,false).GetAwaiter().GetResult();
   if(!record.Done||record.Phase!="Очередь проверена; ожидается новый отчёт")throw new Exception("Completed repair not verified after automatic repair disabled, or old server report claimed fresh");
   Store.Write("automatic-report-recovery-enabled.json",true);record.Done=false;record.VerificationAttempts52=0;
   var incomplete=Store.Clone(client);incomplete.QueueError="fixture unreadable queue";
   for(int attempt=0;attempt<3;attempt++){record.Next=default;AdvanceReportRecovery(Report,_=>Task.FromResult(incomplete),Ready,false).GetAwaiter().GetResult();}
   if(!record.Done||record.VerificationAttempts52!=3||record.Phase!="Требуется действие"||History.Count(j=>j.Id==id)!=1)throw new Exception("Incomplete repair verification repeats cleanup or retries forever");
   record.Done=false;repair.Targets[0].Status="Failed";record.Next=default;AdvanceReportRecovery(Report,Queue,Ready,false).GetAwaiter().GetResult();if(!record.Done||record.Phase!="Требуется действие")throw new Exception("Failed recovery retries automatically");
   var count=History.Count;History.Insert(0,trigger);ScheduleReportRecovery(row,oldReport,client);if(History.Count!=count+1||reportRecoveries[row.Host].JobId!=id)throw new Exception("Same old report starts a new episode");
   record.Done=false;repair.Targets[0].Status="Queued";repair.Targets[0].State=null;CancelUnsentReportRecoveries(row.Host);if(!record.Done||repair.Targets[0].Status!="Cancelled"||queue.Any(q=>q.Job.Id==id))throw new Exception("Unsent recovery survives cancellation");
  }finally{History.Clear();foreach(var j in savedHistory)History.Add(j);queue.Clear();foreach(var q in savedQueue)queue.Enqueue(q);reportRecoveries.Clear();foreach(var e in savedRecords)reportRecoveries[e.Key]=e.Value;row.Target.AdDisabled=disabled;if(cached!=null)reportCache[row.Host]=cached;if(reboot==null)rebootSnapshots.Remove(row.Host);else rebootSnapshots[row.Host]=reboot;if(watched!=null)rebootWatches[row.Host]=watched;Store.Write("automatic-report-recovery-enabled.json",enabled);Save();SaveReportRecoveries();}
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"automatic-queue-recovery.ps1"),AutomaticQueueRecoveryCode(24,"http://wsus.invalid:8530","http://wsus.invalid:8530"),new UTF8Encoding(true));
 }
}
