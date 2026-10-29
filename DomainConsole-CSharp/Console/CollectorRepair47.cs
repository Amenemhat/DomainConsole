using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed partial class RemoteService {
 static readonly ConcurrentDictionary<string,Lazy<Task<string>>> collectorRepairs47=new();
 static readonly ConcurrentDictionary<string,CancellationTokenSource> snapshotCancellation47=new();
 public Task<string> RepairCollector47(TargetRecord target){
  if(TransportBudget41.Paused)throw new OperationCanceledException("Опрос выключен; включите его перед восстановлением сборщика.");
  var frozen=ConnectionStore.Capture(target);string key=TransportBudget41.Key(frozen);
  var flight=collectorRepairs47.GetOrAdd(key,_=>new Lazy<Task<string>>(()=>RepairCore47(frozen,key),LazyThreadSafetyMode.ExecutionAndPublication));
  _=flight.Value.ContinueWith(_=>collectorRepairs47.TryRemove(new KeyValuePair<string,Lazy<Task<string>>>(key,flight)),TaskScheduler.Default);
  return flight.Value;
 }
 public static string RepairScript48()=>Resource42("repair48.ps1").Replace("@@REMOVE@@",RemoveCollectorScript43("Remove",true)).Replace("@@ENABLE@@",RemoveCollectorScript43("Enable",true)).Replace("@@GET@@",SnapshotScript42()).Replace("@@READ@@",SnapshotScript42("Read"));
 async Task<string> RepairCore47(TargetRecord target,string key){
  using var read=PollingControl43.Read();
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(PollingControl43.Token);deadline.CancelAfter(TimeSpan.FromSeconds(150));
  try{
  if(snapshotCancellation47.TryGetValue(key,out var oldCancel)){try{oldCancel.Cancel();}catch(ObjectDisposedException){}}
  if(snapshotFlights42.TryGetValue(key,out var old)&&old.IsValueCreated){try{await old.Value.WaitAsync(TimeSpan.FromSeconds(15),deadline.Token);}catch(TimeoutException){throw new IOException("Прежнее подключение ещё не завершено. Второй сборщик не запущен; повторите после освобождения подключения.");}catch(OperationCanceledException) when(!deadline.IsCancellationRequested){}catch(PollWait42){}catch(Exception) when(old.Value.IsCompleted&&!deadline.IsCancellationRequested){} }
  string bundle;
  while(true){
   deadline.Token.ThrowIfCancellationRequested();
   try{
    var raw=await PowerShellBridge.Execute("# DC-MANUAL41\n"+Invoke(target,RepairScript48()),deadline.Token);
    using var reply=JsonDocument.Parse(raw);var status=reply.RootElement.GetProperty("Status").GetString();
    if(status=="Busy"){await Task.Delay(2000,deadline.Token);continue;}
    if(status!="Recovered")throw new IOException("Восстановление сборщика не подтверждено");
    bundle=reply.RootElement.GetProperty("Snapshot").GetRawText();break;
   }catch(PollWait42 busy) when(busy.Kind=="Busy"){await Task.Delay(2000,deadline.Token);}
  }
  using var parsed=JsonDocument.Parse(bundle);
  if(!parsed.RootElement.GetProperty("Healthy").GetBoolean()||!parsed.RootElement.GetProperty("Complete").GetBoolean())throw new IOException("Сборщик установлен, но полный свежий результат не получен.");
  var stamp=parsed.RootElement.GetProperty("CollectedUtc").GetDateTimeOffset();
  snapshotWait42.TryRemove(key,out _);snapshotCache42[key]=(bundle,stamp.AddSeconds(45));networkFailures42.TryRemove(key,out _);
  return "Сборщик восстановлен · свежий снимок получен: "+Display36.Date(parsed.RootElement.GetProperty("CollectedUtc").GetString()??"");
  }catch(Exception ex){snapshotCache42.TryRemove(key,out _);snapshotWait42[key]=new PollWait42("RepairFailed","Восстановление не подтверждено: "+MainWindow.ShortConnectionError(ex.Message),0);throw;}
 }
}
public partial class MainWindow {
 readonly HashSet<string> repairingCollectors47=new(StringComparer.OrdinalIgnoreCase);
 static bool EventMatches47(ClientEvent31 item,int filter)=>filter switch{1=>item.Kind is "Error" or "Warning",2=>item.Kind=="Error",3=>item.Kind=="Warning",_=>true};
 async Task<bool> CompleteCollectorRepair47(TargetRecord target,CancellationToken generation,Task<string> repair,Func<Task> refresh,Action<string> publish){
  bool Current()=>!closing&&!TransportBudget41.Paused&&!generation.IsCancellationRequested&&CurrentEndpoint(target);
  var result=await repair;if(!Current())return false;
  await refresh();if(!Current())return false;
  publish(result);return true;
 }
 async void CollectorRepair47_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  var row=Current();if(TransportBudget41.Paused){Notify("Для восстановления включите опрос.");return;}
  var target=ConnectionStore.Capture(row.Target);var generation=PollingControl43.Token;
  if(!repairingCollectors47.Add(row.Host))return;
  try{RefreshCollector44(row.Host);Event31(row.Host,"collector-repair47",DateTimeOffset.UtcNow.ToString("o"),"Восстановление сборщика","Info","Останавливается только собственный сборщик. Установки обновлений продолжаются.");
   await CompleteCollectorRepair47(target,generation,remote.RepairCollector47(target),async()=>{
    ReadSucceeded34(row.Host,"флаги и аптайм");ReadSucceeded34(row.Host,"фоновая активность");
    await ReadClientStatus(row,force:true);
   },result=>{Event31(row.Host,"collector-repair47",DateTimeOffset.UtcNow.ToString("o"),"Сборщик восстановлен","Success",result);if(CurrentHost(row.Host))Notify(result);});
  }catch(Exception ex){if(closing||generation.IsCancellationRequested||!CurrentEndpoint(target))return;Event31(row.Host,"collector-repair47",DateTimeOffset.UtcNow.ToString("o"),"Восстановление сборщика не подтверждено","Warning",ShortConnectionError(ex.Message));throw;}
  finally{repairingCollectors47.Remove(row.Host);if(CurrentHost(row.Host)){RefreshCollector44(row.Host);ShowCurrent();}}
 });
}
