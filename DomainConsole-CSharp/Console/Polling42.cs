using System.Collections.Concurrent;
using System.Text.Json;
using System.IO;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class PollWait42:IOException {
 public string Kind{get;} public DateTimeOffset Next{get;}
 public PollWait42(string kind,string reason,int seconds):base(reason){Kind=kind;Next=seconds<=0?DateTimeOffset.MaxValue:DateTimeOffset.UtcNow.AddSeconds(seconds);}
}
public sealed partial class RemoteService {
 static readonly ConcurrentDictionary<string,Lazy<Task<string>>> snapshotFlights42=new();
 static readonly ConcurrentDictionary<string,(string Json,DateTimeOffset Until)> snapshotCache42=new();
 static readonly ConcurrentDictionary<string,PollWait42> snapshotWait42=new();
 static readonly ConcurrentDictionary<string,int> networkFailures42=new();
 static string Resource42(string name){using var stream=typeof(RemoteService).Assembly.GetManifestResourceStream("DomainConsole.Polling42."+name)??throw new IOException("Не найден сценарий опроса "+name);using var reader=new StreamReader(stream);return reader.ReadToEnd();}
 static string Worker42(string part,string body)=>"$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';Start-Sleep -Milliseconds 200;$ps=[PowerShell]::Create();try{$null=$ps.AddScript("+PowerShellBridge.Literal("$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';"+body)+");$pending=$ps.BeginInvoke();$stopping=$false;while(!$pending.IsCompleted){if(!$stopping -and ((Test-Path (Join-Path $PSScriptRoot 'cancel.flag')) -or (Test-Path (Join-Path $PSScriptRoot '"+part+".cancel')))){$null=$ps.BeginStop($null,$null);$stopping=$true};Start-Sleep -Milliseconds 100};$data=$ps.EndInvoke($pending);$data|Set-Content -LiteralPath (Join-Path $PSScriptRoot '"+part+".json') -Encoding UTF8}catch{[IO.File]::WriteAllText((Join-Path $PSScriptRoot '"+part+".error'),$_.Exception.Message);exit 1}finally{$ps.Dispose()}";

 public static string SnapshotScript42(string mode="Get",string? reboot=null,string? activity=null,string? supervisor=null){
  string collect=supervisor??Resource42("collector.ps1"),r=Worker42("Reboot",reboot??RebootStateScript),a=Worker42("Activity",activity??LiveUpdateScript);
  string Hash(string v)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(v))).ToLowerInvariant();
  string package=JsonSerializer.Serialize(new{Version=47,Protocol=1,Collector=Hash(collect),Reboot=Hash(r),Activity=Hash(a)});
  return Resource42("admission.ps1").Replace("@@MODE@@",mode).Replace("@@PACKAGE@@","("+PowerShellBridge.Literal(package)+"|ConvertFrom-Json)").Replace("@@COLLECTOR@@",PowerShellBridge.Literal(collect)).Replace("@@REBOOT@@",PowerShellBridge.Literal(r)).Replace("@@ACTIVITY@@",PowerShellBridge.Literal(a));
 }

 public static PollWait42? SnapshotWait42(TargetRecord target)=>snapshotWait42.GetValueOrDefault(TransportBudget41.Key(target));
 public static bool SnapshotDue42(TargetRecord target)=>!snapshotWait42.TryGetValue(TransportBudget41.Key(target),out var wait)||DateTimeOffset.UtcNow>=wait.Next;
 string Read42(TargetRecord t,string script)=>"# DC-READ42\n"+Invoke(t,script);
 public async Task<string> SnapshotPart42(TargetRecord target,string part,CancellationToken token=default){
  var key=TransportBudget41.Key(target);
  if(collectorRepairs47.ContainsKey(key))throw new PollWait42("Repairing","Выполняется адресное восстановление сборщика",10);
  if(snapshotCache42.TryGetValue(key,out var cached)&&DateTimeOffset.UtcNow<cached.Until){using var json=JsonDocument.Parse(cached.Json);return json.RootElement.GetProperty(part).GetRawText();}
  if(snapshotWait42.TryGetValue(key,out var waiting)&&DateTimeOffset.UtcNow<waiting.Next)throw waiting;
  var flight=snapshotFlights42.GetOrAdd(key,_=>new Lazy<Task<string>>(()=>Collect42(target,key),LazyThreadSafetyMode.ExecutionAndPublication));
  _ = flight.Value.ContinueWith(_=>snapshotFlights42.TryRemove(new KeyValuePair<string,Lazy<Task<string>>>(key,flight)),TaskScheduler.Default);
  try{var bundle=await flight.Value.WaitAsync(token);using var json=JsonDocument.Parse(bundle);return json.RootElement.GetProperty(part).GetRawText();}
  finally{if(flight.IsValueCreated&&flight.Value.IsCompleted)snapshotFlights42.TryRemove(new KeyValuePair<string,Lazy<Task<string>>>(key,flight));}
 }
 async Task<string> Collect42(TargetRecord target,string key,CancellationToken repairToken=default){
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(PollingControl43.Token,repairToken);deadline.CancelAfter(TimeSpan.FromSeconds(90));snapshotCancellation47[key]=deadline;string mode="Get";
  try{while(true){using var request=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);request.CancelAfter(TimeSpan.FromSeconds(30));var raw=await PowerShellBridge.Execute(Read42(target,SnapshotScript42(mode)),request.Token);using var doc=JsonDocument.Parse(raw);var root=doc.RootElement;var status=root.GetProperty("Status").GetString()??"Unknown";var reason=root.GetProperty("Reason").GetString()??"Причина не получена";
    if(status=="Ready") {string bundle=root.GetProperty("Snapshot").GetRawText();using var parsed=JsonDocument.Parse(bundle);var stamp=parsed.RootElement.GetProperty("CollectedUtc").GetDateTimeOffset();snapshotCache42[key]=(bundle,stamp.AddSeconds(45));snapshotWait42.TryRemove(key,out _);networkFailures42.TryRemove(key,out _);return bundle;}
    if(status is "Busy" or "Stopping"){mode="Read";await Task.Delay(TimeSpan.FromSeconds(10),deadline.Token);continue;}
    var wait=new PollWait42(status,reason,status=="Idle"?120:root.GetProperty("RetrySeconds").GetInt32());snapshotWait42[key]=wait;throw wait;
   }}catch(PollWait42){throw;}catch(OperationCanceledException) when(TransportBudget41.Paused||PollingControl43.Cancelled){throw;}catch(Exception ex){int failures=networkFailures42.AddOrUpdate(key,1,(_,n)=>Math.Min(3,n+1));var wait=CollectorFailure48(ex,failures);snapshotWait42[key]=wait;throw wait;}finally{snapshotCancellation47.TryRemove(new KeyValuePair<string,CancellationTokenSource>(key,deadline));}

 }
 internal static PollWait42 CollectorFailure48(Exception ex,int failures){
  bool access=MainWindow.AccessError38(ex.Message);
  bool format=ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException;
  int delay=failures<=1?120:failures==2?300:900;
  return new PollWait42(format?"InvalidResult":access?"AccessDenied":"Unreachable",format?"Сборщик вернул некорректный результат; нужна диагностика или восстановление":access?"Нет прав для опроса: "+MainWindow.ShortConnectionError(ex.Message):"Опрос не завершён: "+MainWindow.ShortConnectionError(ex.Message)+". Следующая попытка через "+(delay/60)+" мин",(access||format)?0:delay);
 }
 public async Task<string> InspectSnapshot42(TargetRecord target,string mode="Inspect") {if(mode=="Recover")return "## Опрос клиента\n"+await RepairCollector47(target)+"\nСостояние: Recovered";using var read48=mode=="Inspect"?PollingControl43.Read():null;using var timeout=CancellationTokenSource.CreateLinkedTokenSource(mode=="Inspect"?PollingControl43.Token:CancellationToken.None);timeout.CancelAfter(TimeSpan.FromSeconds(30));var text=await PowerShellBridge.Execute("# DC-MANUAL41\n"+Invoke(target,SnapshotScript42(mode)),timeout.Token);using var doc=JsonDocument.Parse(text);var root=doc.RootElement;var status=root.GetProperty("Status").GetString();if(mode=="Recover"&&status=="Recovered"){snapshotWait42.TryRemove(TransportBudget41.Key(target),out _);snapshotCache42.TryRemove(TransportBudget41.Key(target),out _);networkFailures42.TryRemove(TransportBudget41.Key(target),out _);TransportBudget41.Reset(target);}return "## Опрос клиента\n"+root.GetProperty("Reason").GetString()+"\nСостояние: "+status+"\n\n## Технические сведения опроса\n"+JsonSerializer.Serialize(root,Store.Options);}
}

public partial class MainWindow {
 readonly Dictionary<string,string> pollEventKeys42=new(StringComparer.OrdinalIgnoreCase);
 void RecordPollWait42(string host,string operation,PollWait42 wait){
  string key=host+"|"+operation;readFailures34[key]=new(){Next=wait.Next,Error=wait.Message,Last=DateTimeOffset.UtcNow};
  string title=wait.Kind switch{"Busy"=>"Опрос уже выполняется","Cooldown"=>"Опрос ожидает повторной проверки","Blocked"=>"Состояние клиента не обновлено","Unknown"=>"Проверяется завершение прежнего опроса",_=>"Состояние клиента не обновлено"};
  string details="Служебный снимок не получен; сохранённые данные оставлены. "+wait.Message+(wait.Next==DateTimeOffset.MaxValue?" · автоматический повтор остановлен":" · следующая проверка: "+Display36.Date(wait.Next.ToString("o")));
  statusLimitations[host]=details;string signature=wait.Kind+"|"+wait.Message;
  string eventKey43=host+"|collector";if(pollEventKeys42.GetValueOrDefault(eventKey43)!=signature){pollEventKeys42[eventKey43]=signature;Event31(host,"poll42",signature,title,"Warning",details);}
  var folder=Path.Combine(Store.Root,"Diagnostics");Directory.CreateDirectory(folder);string name=TransportBudget41.Key(new(){Host=host});Store.Write("Diagnostics/poll42-"+name+".json",new{Operation=operation,Kind=wait.Kind,Error=wait.Message,Next=wait.Next});
 }
}
public partial class MainWindow {
 readonly Dictionary<string,(DateTimeOffset Next,string Signature)> jobPoll42=new();
 bool JobPollDue42(ComputerRow row)=>!jobPoll42.TryGetValue(row.JobId+row.Host,out var value)||DateTimeOffset.UtcNow>=value.Next;
 void JobPollObserved42(ComputerRow row,PollResult data,TimeSpan elapsed){string key=row.JobId+row.Host;string signature=data.State==null?"":data.State.Status+"|"+data.State.Stage+"|"+data.State.Progress;int seconds=row.Target.Status is "AwaitingReboot" or "RebootScheduled"?120:row.Target.State?.Ended.Length>0?120:elapsed.TotalSeconds>5?30:jobPoll42.TryGetValue(key,out var old)&&old.Signature==signature?20:10;jobPoll42[key]=(DateTimeOffset.UtcNow.AddSeconds(seconds),signature);}
}
