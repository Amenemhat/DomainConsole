using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
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
}
