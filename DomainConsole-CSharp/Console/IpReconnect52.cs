using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 readonly Dictionary<string,Task<bool>> ipConnectionFlights52=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,DateTimeOffset> ipConnectionRetry52=new(StringComparer.OrdinalIgnoreCase);
 async Task<bool> EnsureIpConnection52(ComputerRow row,Func<TargetRecord,Task<string>>? check=null){
  var target=ConnectionStore.Capture(row.Target);
  if(ConnectionStore.Address(target).Length==0)return true;
  var generation=PollingControl43.Token;
  bool Current52()=>!closing&&!TransportBudget41.Paused&&!generation.IsCancellationRequested&&CurrentEndpoint(target)&&CurrentIdentity(target);
  if(!Current52())return false;
  if(ConnectionFresh(row.Host)&&clientConnections[row.Host].Error.Length==0)return true;
  string key=TransportBudget41.Key(target);
  if(ipConnectionFlights52.TryGetValue(key,out var flight))return await flight&&Current52();
  if(ipConnectionRetry52.TryGetValue(key,out var next)&&DateTimeOffset.UtcNow<next)return false;
  var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
  ipConnectionFlights52[key]=completion.Task;
  var started=DateTimeOffset.UtcNow;bool success=false;
  Notify(row.Name+": проверка связи по IP…");
  try{
   await (check??remote.CheckConnection)(target);
   if(!Current52())return false;
   ConnectionSucceeded(row.Host,started,verifiedEndpoint52:true);success=clientConnections.TryGetValue(row.Host,out var observed)&&observed.EvidenceStarted38==started&&observed.Error.Length==0;if(success)ipConnectionRetry52.Remove(key);return success;
  }catch(Exception ex){
   if(Current52()){ConnectionFailed(row.Host,ex.Message,started);ipConnectionRetry52[key]=DateTimeOffset.UtcNow.AddMinutes(1);}
   return false;
  }finally{ipConnectionFlights52.Remove(key);completion.TrySetResult(success);RefreshOutcomes();if(IsCurrent(row))UpdateWsusStage();}
 }
 async Task VerifyIpReconnect52(){
  const string host="vpn52.fixture.invalid";
  bool paused=TransportBudget41.Paused;var saved=ConnectionStore.Profiles.GetValueOrDefault(host);
  var row=new ComputerRow(new(){Host=host,Name="VPN fixture"});Computers.Add(row);
  int jobs=History.Count,queued=queue.Count,calls=0;
  try{
   PollingControl43.Resume();ConnectionStore.Profiles[host]=new(){Address="192.0.2.152",Identity="vpn-fixture52",ComputerName="VPN-FIXTURE",AuthMode="Current"};
   void Offline(){clientConnections[host]=new(){Lost38=true,Error="WinRM fixture unavailable",LastSuccess=DateTimeOffset.UtcNow.AddDays(-1),LastAttempt=DateTimeOffset.UtcNow};}
   Offline();statusLimitations[host]="Нет учётных данных для IP; подключение не проверено";var gate=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
   var requests=Enumerable.Range(0,8).Select(_=>EnsureIpConnection52(row,t=>{calls++;if(t.ConnectionAddress!="192.0.2.152")throw new Exception("IP capture lost");return gate.Task;})).ToList();
   if(calls!=1)throw new Exception("Concurrent IP actions duplicated connection checks");
   gate.SetResult("verified");var results=await Task.WhenAll(requests);
   if(results.Any(x=>!x)||!ConnectionFresh(host)||LostObservation51(host)||LinkKind(host)!="Connected")throw new Exception("IP connection did not release stale offline state");
   await EnsureIpConnection52(row,_=>throw new Exception("Fresh verified IP was checked twice"));
   Offline();Task<string> Fail(TargetRecord _){calls++;throw new System.IO.IOException("WinRM fixture VPN disconnected");}
   if(await EnsureIpConnection52(row,Fail))throw new Exception("Disconnected VPN accepted");
   int failedCalls=calls;for(int i=0;i<8;i++)if(await EnsureIpConnection52(row,Fail))throw new Exception("Cooldown returned success");
   if(calls!=failedCalls)throw new Exception("Disconnected VPN generated repeated checks");
   string key=TransportBudget41.Key(ConnectionStore.Capture(row.Target));ipConnectionRetry52[key]=DateTimeOffset.UtcNow.AddSeconds(-1);
   if(!await EnsureIpConnection52(row,_=>{calls++;return Task.FromResult("returned");})||calls!=failedCalls+1)throw new Exception("VPN return did not permit one retry");
   foreach(string change in new[]{"address","identity","master","newer-failure"}){
    Offline();var delayed=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var pending=EnsureIpConnection52(row,_=>delayed.Task);
    if(change=="address")ConnectionStore.Profiles[host].Address="192.0.2.153";
    else if(change=="identity")ConnectionStore.Profiles[host].Identity="replacement52";
    else if(change=="master")PollingControl43.Pause();
    else ConnectionFailed(host,"WinRM newer failure",DateTimeOffset.UtcNow.AddSeconds(1));
    delayed.SetResult("obsolete");if(await pending||ConnectionFresh(host))throw new Exception("Obsolete IP response restored connection: "+change);
    PollingControl43.Resume();ConnectionStore.Profiles[host].Address="192.0.2.152";ConnectionStore.Profiles[host].Identity="vpn-fixture52";
   }
   PollingControl43.Pause();if(await EnsureIpConnection52(row,_=>throw new Exception("Master OFF opened connection")))throw new Exception("Master OFF accepted IP action");
   if(History.Count!=jobs||queue.Count!=queued||ipConnectionFlights52.Count!=0)throw new Exception("Connection checks created jobs or retained flights");
   System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ui-ip-reconnect52.txt"),"PASS: stale offline IP restored; 8 parallel actions share one check; disconnected VPN cooldown; one retry after cooldown; stale endpoint/identity/master response discarded; no jobs or retained flights");
  }finally{Computers.Remove(row);clientConnections.Remove(host);statusLimitations.Remove(host);liveConnections.Remove(host);ipConnectionRetry52.Clear();if(saved==null)ConnectionStore.Profiles.Remove(host);else ConnectionStore.Profiles[host]=saved;if(paused)PollingControl43.Pause();else PollingControl43.Resume();Store.Write("client-connections.json",clientConnections);}
 }
}
