using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task<bool> CompletePreparedSubmission47(RemoteJob job,List<ComputerRow> selected,List<ComputerRow> frozen,Func<Task<(bool Accepted,bool Force,bool Wait)>> prepare,bool beginPlan){
  var targets=frozen.Select(r=>Store.Clone(r.Target)).ToList();
  var decision=await prepare();if(!decision.Accepted)return false;
  job.AllowUpdateConflict=decision.Force;job.QueueAfterWsus=decision.Wait;
  job.IndependentControl=IsIndependentControl(job.Steps);job.ExclusiveMaintenance=Maintenance(job.Steps,job.MicrosoftSource);
  job.ContinueOnError&=!job.Steps.Any(t=>t.Kind is "ReportRecovery" or "CacheCleanup"||t.Code.Contains("function Save-Dependents("));
  foreach(var t in targets){t.Status="Queued";t.State=null;t.Output="";job.Targets.Add(t);selected.Single(r=>r.Host==t.Host).JobId=job.Id;confirmedJobs.Add(job.Id+t.Host);queue.Enqueue((job,t));}
  History.Insert(0,job);if(beginPlan){job.BeginPlan38=true;job.PlanId37=job.Id;}
  foreach(var t in targets){RememberComputer(t.Host);if(job.OutputChannel=="WSUS"){wsusFailureDetails.Remove(t.Host);liveReports.Remove(t.Host);if(reportCache.TryGetValue(t.Host,out var cached)){cached.Started=job.Created;serverReports[t.Host]=CachedReportText(cached);reportSeverity[t.Host]=cached.Report.Severity(job.Created);}}}
  if(job.OutputChannel=="WSUS")Store.Write("wsus-report-cache.json",reportCache);
  Save();Sync(job);
  // Background completion never chooses a computer, tab, history entry or new checkbox set.
  if(targets.Any(t=>CurrentHost(t.Host)))ShowCurrent();
  return true;
 }
}
