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
  var frozen=ConnectionStore.Capture(target);string key=TransportBudget41.Key(frozen);
  var flight=collectorRepairs47.GetOrAdd(key,_=>new Lazy<Task<string>>(()=>RepairCore47(frozen,key),LazyThreadSafetyMode.ExecutionAndPublication));
  _=flight.Value.ContinueWith(_=>collectorRepairs47.TryRemove(new KeyValuePair<string,Lazy<Task<string>>>(key,flight)),TaskScheduler.Default);
  return flight.Value;
 }
 async Task<string> RepairCore47(TargetRecord target,string key){
  using var read=PollingControl43.Read();
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(PollingControl43.Token);deadline.CancelAfter(TimeSpan.FromSeconds(150));
  try{
  if(snapshotCancellation47.TryGetValue(key,out var oldCancel)){try{oldCancel.Cancel();}catch(ObjectDisposedException){}}
  if(snapshotFlights42.TryGetValue(key,out var old)&&old.IsValueCreated){try{await old.Value.WaitAsync(TimeSpan.FromSeconds(15),deadline.Token);}catch(TimeoutException){throw new IOException("Прежнее подключение ещё не завершено. Второй сборщик не запущен; повторите после освобождения подключения.");}catch(OperationCanceledException) when(!deadline.IsCancellationRequested){}catch(PollWait42){} }
  async Task Execute(bool enable){
   while(true){deadline.Token.ThrowIfCancellationRequested();try{await RemoveCollector43(target,enable,deadline.Token);return;}
    catch(PollWait42 busy) when(busy.Kind=="Busy"){await Task.Delay(2000,deadline.Token);}
   }
  }
  await Execute(false);await Execute(true);
  snapshotWait42.TryRemove(key,out _);snapshotCache42.TryRemove(key,out _);networkFailures42.TryRemove(key,out _);
  string bundle=await Collect42(target,key,deadline.Token);
  using var parsed=JsonDocument.Parse(bundle);
  if(!parsed.RootElement.GetProperty("Healthy").GetBoolean()||!parsed.RootElement.GetProperty("Complete").GetBoolean())throw new IOException("Сборщик установлен, но полный свежий результат не получен.");
  return "Сборщик восстановлен · свежий снимок получен: "+Display36.Date(parsed.RootElement.GetProperty("CollectedUtc").GetString()??"");
  }catch(Exception ex){snapshotCache42.TryRemove(key,out _);snapshotWait42[key]=new PollWait42("RepairFailed","Восстановление не подтверждено: "+MainWindow.ShortConnectionError(ex.Message),0);throw;}
 }
}
public partial class MainWindow {
 readonly HashSet<string> repairingCollectors47=new(StringComparer.OrdinalIgnoreCase);
 static bool EventMatches47(ClientEvent31 item,int filter)=>filter switch{1=>item.Kind is "Error" or "Warning",2=>item.Kind=="Error",3=>item.Kind=="Warning",_=>true};
 async void CollectorRepair47_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  var row=Current();if(TransportBudget41.Paused){Notify("Для восстановления включите опрос.");return;}
  if(!repairingCollectors47.Add(row.Host))return;
  try{RefreshCollector44(row.Host);Event31(row.Host,"collector-repair47",DateTimeOffset.UtcNow.ToString("o"),"Восстановление сборщика","Info","Останавливается только собственный сборщик. Установки обновлений продолжаются.");
   var result=await remote.RepairCollector47(row.Target);
   ReadSucceeded34(row.Host,"флаги и аптайм");ReadSucceeded34(row.Host,"фоновая активность");
   await ReadClientStatus(row,force:true);Event31(row.Host,"collector-repair47",DateTimeOffset.UtcNow.ToString("o"),"Сборщик восстановлен","Success",result);Notify(result);
  }catch(Exception ex){Event31(row.Host,"collector-repair47",DateTimeOffset.UtcNow.ToString("o"),"Восстановление сборщика не подтверждено","Warning",ShortConnectionError(ex.Message));throw;}
  finally{repairingCollectors47.Remove(row.Host);if(CurrentHost(row.Host)){RefreshCollector44(row.Host);ShowCurrent();}}
 });
}
