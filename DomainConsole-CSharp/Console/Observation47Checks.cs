using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyObservation51(){
  const string host="observation51.fixture.invalid";
  bool paused=TransportBudget41.Paused;var target=ConnectionStore.Capture(new TargetRecord{Host=host});target.Status="Running";
  var job=new RemoteJob{Created=DateTimeOffset.UtcNow.AddDays(-3).ToString("o"),Targets=new(){target}};var row=new ComputerRow(target,job.Id);History.Insert(0,job);
  int calls=0;Task<PollResult> Read(TargetRecord _,string id,bool selected){calls++;throw new RemoteDataException("fixture: outcome is unknown");}
  try{
   PollingControl43.Resume();clientConnections[host]=new(){Lost38=true,Error="WinRM fixture timeout",LastAttempt=DateTimeOffset.UtcNow,LastSuccess=DateTimeOffset.UtcNow.AddDays(-3)};
   for(int i=0;i<10;i++)await Poll(row,false,Read);
   if(calls!=0||!ObservationExpired51(row)||target.Status!="Running")throw new Exception("51 lost job generated polling calls or fabricated completion");
   clientConnections[host]=new(){LastSuccess=DateTimeOffset.UtcNow};stoppedObservations51.Add(ObservationKey51(row));
   for(int i=0;i<10;i++)await Poll(row,false,Read);
   if(calls!=0)throw new Exception("51 manually stopped observation was automatically resumed");
   PollingControl43.Pause();await Poll(row,false,Read);if(calls!=0)throw new Exception("51 master OFF allowed a network read");PollingControl43.Resume();
   await Poll(row,true,Read);if(calls!=1||target.Status!="Running"||History.Count(j=>j.Id==job.Id)!=1||queue.Any(q=>q.Job.Id==job.Id))throw new Exception("51 explicit result check duplicated/replayed the operation");
   var watch51=new RebootWatch{JobId=job.Id,Confirmed=true,ExpectedUtc=DateTimeOffset.UtcNow.AddMinutes(-1)};rebootWatches[host]=watch51;int resets51=0;
   Task<string> Reset51(TargetRecord _){resets51++;return Task.FromResult("cooldown");}
   PollingControl43.Pause();await TryResetRebootPoll51(host,watch51,target,Reset51);if(resets51!=0||watch51.PollResetAttempted51)throw new Exception("51 reset bypassed master OFF");PollingControl43.Resume();
   await TryResetRebootPoll51(host,watch51,target,Reset51);watch51=Store.Clone(watch51);rebootWatches[host]=watch51;await TryResetRebootPoll51(host,watch51,target,Reset51);
   if(resets51!=1||watch51.Done||!watch51.LastError.Contains("пока не получен")||watch51.Next<=DateTimeOffset.UtcNow)throw new Exception("51 reset repeated after restart or claimed a fresh result");

  }finally{
   rebootWatches.Remove(host);Store.Write("reboot-watches.json",rebootWatches);stoppedObservations51.Remove(ObservationKey51(row));History.Remove(job);clientConnections.Remove(host);liveConnections.Remove(host);polling.Remove(job.Id+host);readFailures34.Remove(host+"|результат задания");
   if(eventWrites39.TryGetValue(host,out var writes))await writes;
   if(paused)PollingControl43.Pause();else PollingControl43.Resume();
  }
 }
 async Task VerifyObsoleteJobObservation47(){
  const string host="job-observation47.fixture.invalid";
  ConnectionStore.Profiles.TryGetValue(host,out var saved);bool paused=TransportBudget41.Paused;
  try{
   foreach(bool fail in new[]{false,true})foreach(bool cancel in new[]{false,true}){
    PollingControl43.Resume();ConnectionStore.Profiles[host]=new(){AuthMode="Current",Address="192.0.2.61",Identity="fixture"};
    var target=ConnectionStore.Capture(new(){Host=host});target.Status="Running";target.Output="retained output";
    var job=new RemoteJob{Targets=new(){target}};History.Insert(0,job);var row=new ComputerRow(target,job.Id);
    try{
     var reply=new TaskCompletionSource<PollResult>(TaskCreationOptions.RunContinuationsAsynchronously);bool entered=false;
     var pending=Poll(row,true,(_,_,_)=>{entered=true;return reply.Task;});
     if(!entered)throw new Exception("Job observation fixture never entered transport");
     if(cancel){PollingControl43.Pause();PollingControl43.Resume();}else ConnectionStore.Profiles[host].Address="192.0.2.62";
     if(fail)reply.SetException(new RemoteDataException("obsolete malformed result"));else reply.SetResult(new(){State=new(){Id=job.Id,Status="Completed",Ended=DateTimeOffset.UtcNow.ToString("o")}});
     await pending;
     if(target.Status!="Running"||target.State!=null||target.Output!="retained output"||clientConnections.ContainsKey(host)||readFailures34.ContainsKey(host+"|результат задания"))throw new Exception("Obsolete job observation changed history, output or connection");
    }finally{History.Remove(job);polling.Remove(job.Id+host);}
   }
   foreach(bool fail in new[]{false,true})foreach(bool cancel in new[]{false,true}){
    PollingControl43.Resume();ConnectionStore.Profiles[host]=new(){AuthMode="Current",Address="192.0.2.61",Identity="fixture"};
    var target=ConnectionStore.Capture(new(){Host=host});var job=new RemoteJob{Targets=new(){target}};var row=new ComputerRow(target,job.Id);
    var state=new RemoteState{Id=job.Id,Status="Completed",Ended=DateTimeOffset.UtcNow.ToString("o")};var reply=new TaskCompletionSource<RebootSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);bool entered=false;
    var pending=ObserveRebootAfterJob(row,job,new(){State=state},_=>{entered=true;return reply.Task;});
    if(!entered)throw new Exception("Post-job reboot fixture never entered transport");
    if(cancel){PollingControl43.Pause();PollingControl43.Resume();}else ConnectionStore.Profiles[host].Address="192.0.2.62";
    if(fail)reply.SetException(new Exception("obsolete reboot failure"));else reply.SetResult(new(){Required=true,CollectedUtc=DateTimeOffset.UtcNow.ToString("o")});
    await pending;
    if(state.RebootEvidence!=null||rebootSnapshots.ContainsKey(host)||wsusChecked.Contains(job.Id+host+"reboot"))throw new Exception("Obsolete reboot observation changed flags or prevented a fresh retry");
   }
   string wsusHost47=WsusHost.Text,wsusPort47=WsusPort.Text;bool? wsusSsl47=WsusSsl.IsChecked;
   try{
    WsusHost.Text="report-observation47.fixture.invalid";WsusPort.Text="8530";WsusSsl.IsChecked=false;
    foreach(bool fail in new[]{false,true})foreach(bool cancel in new[]{false,true}){
     PollingControl43.Resume();ConnectionStore.Profiles[host]=new(){AuthMode="Current",Address="192.0.2.61",Identity="fixture"};
     var row=new ComputerRow(ConnectionStore.Capture(new(){Host=host}));serverReports[host]="retained report";
     var reply=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);bool entered=false;
     var pending=FetchWsusReport(row,(_,_)=>{entered=true;return reply.Task;});
     if(!entered)throw new Exception("WSUS observation fixture never entered transport");
     if(cancel){PollingControl43.Pause();PollingControl43.Resume();}else ConnectionStore.Profiles[host].Address="192.0.2.62";
     if(fail)reply.SetException(new Exception("obsolete WSUS failure"));else reply.SetResult("{malformed obsolete response");
     await pending;
     if(serverReports[host]!="retained report"||reportCache.ContainsKey(host)||overviewErrors.ContainsKey(host))throw new Exception("Obsolete WSUS response changed saved report or error");
    }
   }finally{WsusHost.Text=wsusHost47;WsusPort.Text=wsusPort47;WsusSsl.IsChecked=wsusSsl47;serverReports.Remove(host);reportCache.Remove(host);overviewErrors.Remove(host);reportReading.Remove(host);}
  }finally{if(saved==null)ConnectionStore.Profiles.Remove(host);else ConnectionStore.Profiles[host]=saved;clientConnections.Remove(host);liveConnections.Remove(host);rebootSnapshots.Remove(host);readFailures34.Remove(host+"|результат задания");if(paused)PollingControl43.Pause();else PollingControl43.Resume();}
 }
 async Task VerifyObsoleteReportDetails47(){
  const string host="report-details47.fixture.invalid";
  ConnectionStore.Profiles.TryGetValue(host,out var saved);bool paused=TransportBudget41.Paused;
  string originalHost=WsusHost.Text,originalPort=WsusPort.Text;bool? originalSsl=WsusSsl.IsChecked;
  bool enabled=AutomaticReportRecoveryEnabled;var savedRecoveries=reportRecoveries.ToList();reportRecoveries.Clear();
  try{
   WsusHost.Text="report-source47.fixture.invalid";WsusPort.Text="8530";Store.Write("automatic-report-recovery-enabled.json",true);
   foreach(string phase in new[]{"queue","events","history","recovery"})foreach(bool fail in new[]{false,true})foreach(string change in new[]{"client","ssl","generation","report"}){
    PollingControl43.Resume();WsusSsl.IsChecked=false;ConnectionStore.Profiles[host]=new(){AuthMode="Current",Address="192.0.2.71",Identity="fixture"};
    var target=ConnectionStore.Capture(new(){Host=host});var row=new ComputerRow(target);Computers.Add(row);
    var report=new WsusReport{LastReportedUtc=DateTimeOffset.UtcNow.AddDays(-3).ToString("o"),FailedCount=1,FailedUpdates=new(){new(){Id="fixture-update",Title="Fixture update"}}};
    reportCache[host]=new(){Report=report,Server=OverviewServer};serverReports[host]="retained report";automaticReportChecks.Remove(host);
    var trigger=new RemoteJob{OutputChannel="WSUS",Steps=new(){new(){Kind="UpdateScan"}},Targets=new(){target}};target.Status="Completed";target.State=new(){Status="Completed",ReportRequested=true,Ended=DateTimeOffset.UtcNow.ToString("o")};History.Insert(0,trigger);
    var client=new ClientReportProbe{CollectedUtc=DateTimeOffset.UtcNow.ToString("o"),UseWUServer=1,WUServer="http://report-source47.fixture.invalid:8530",WUStatusServer="http://report-source47.fixture.invalid:8530",Queues=new(){new(){Name="EventCache",Files=2,Bytes=20,OldestUtc=DateTimeOffset.UtcNow.AddDays(-3).ToString("o")},new(){Name="EventCache.v2"}}};
    var gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);bool entered=false;int nextCalls=0;var beforeJobs=History.Count;var beforeQueue=queue.Count;
    async Task Delay(){entered=true;await gate.Task;if(fail)throw new Exception("obsolete details fixture failure");}
    Task pending;
    try{
     if(phase=="queue")pending=CheckOldReport(row,report,async _=>{await Delay();return client;});
     else if(phase=="recovery"){
      ScheduleReportRecovery(row,report,client);if(!reportRecoveries.TryGetValue(host,out var recovery))throw new Exception("Recovery fixture was not admitted");recovery.Next=default;
      pending=AdvanceReportRecovery(async _=>{await Delay();return report;},_=>{nextCalls++;return Task.FromResult(client);},_=>Task.FromResult(new LiveUpdateActivity()),false);
     }else pending=ExplainServerFailures(row,report,async (_,_,_)=>{if(phase=="events")await Delay();return new();},async (_,_)=>{if(phase=="history")await Delay();else nextCalls++;return "{\"History\":[]}";});
     if(!entered)throw new Exception("Report details fixture never entered "+phase);
     if(change=="client")ConnectionStore.Profiles[host].Address="192.0.2.72";
     else if(change=="ssl")WsusSsl.IsChecked=true;
     else if(change=="generation"){PollingControl43.Pause();PollingControl43.Resume();}
     else {reportCache[host]=new(){Report=new(),Server=OverviewServer};if(phase=="recovery")reportRecoveries[host].Done=true;}
     gate.SetResult(true);await pending;
     if(nextCalls!=0||History.Count!=beforeJobs||queue.Count!=beforeQueue||serverReports[host]!="retained report"||report.FailureContext.Length>0||report.FailedUpdates[0].Detail.Length>0||wsusFailureDetails.ContainsKey(host)||deliveryReports.ContainsKey(trigger.Id+host)||deliveryReports.ContainsKey(host))throw new Exception("Obsolete report details changed state or launched another action: "+phase+"/"+change);
     if(phase=="queue"&&(reportRecoveries.ContainsKey(host)||automaticReportChecks.ContainsKey(host)))throw new Exception("Obsolete queue probe scheduled recovery or delayed fresh retry");
     // A fresh request under the new generation must still work (positive control).
     if(phase=="queue"){
      PollingControl43.Resume();WsusSsl.IsChecked=false;ConnectionStore.Profiles[host].Address="192.0.2.71";reportCache[host]=new(){Report=report,Server=OverviewServer};
      await CheckOldReport(row,report,_=>Task.FromResult(client));
      if(!reportRecoveries.ContainsKey(host)||!deliveryReports.ContainsKey(trigger.Id+host))throw new Exception("Fresh queue diagnosis discarded after obsolete response");
     }
    }finally{History.Remove(trigger);Computers.Remove(row);reportRecoveries.Remove(host);reportCache.Remove(host);serverReports.Remove(host);automaticReportChecks.Remove(host);failureReading.Remove(host);deliveryReports.Remove(trigger.Id+host);deliveryReports.Remove(host);}
   }
  }finally{WsusHost.Text=originalHost;WsusPort.Text=originalPort;WsusSsl.IsChecked=originalSsl;Store.Write("automatic-report-recovery-enabled.json",enabled);reportRecoveries.Clear();foreach(var item in savedRecoveries)reportRecoveries[item.Key]=item.Value;SaveReportRecoveries();if(saved==null)ConnectionStore.Profiles.Remove(host);else ConnectionStore.Profiles[host]=saved;if(paused)PollingControl43.Pause();else PollingControl43.Resume();}
 }

}
