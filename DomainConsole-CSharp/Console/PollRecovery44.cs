using System.Text.Json;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed partial class RemoteService {
 public async Task<string> ResetPolling44(TargetRecord target){target=ConnectionStore.Capture(target);using var read44=PollingControl43.Read();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(PollingControl43.Token);deadline.CancelAfter(TimeSpan.FromSeconds(100));while(true){var raw=await PowerShellBridge.Execute("# DC-MANUAL41\n"+Invoke(target,SnapshotScript42("Reset")),deadline.Token);using var doc=JsonDocument.Parse(raw);var root=doc.RootElement;var status=root.GetProperty("Status").GetString()??"Unknown";if(status is "Busy" or "Stopping"){await Task.Delay(2000,deadline.Token);continue;}var reason=root.GetProperty("Reason").GetString()??"";if(status!="Cooldown")throw new InvalidOperationException("Сброс опроса не подтверждён · "+status+": "+reason);if(status=="Cooldown"){var key=TransportBudget41.Key(target);snapshotCache42.TryRemove(key,out _);snapshotWait42[key]=new PollWait42("Cooldown",reason,120);networkFailures42.TryRemove(key,out _);}return reason;}}
}
public partial class MainWindow {
 void RefreshCollector44(string host){if(CollectorNotice43==null)return;var wait=RemoteService.SnapshotWait42(ConnectionStore.Capture(new TargetRecord{Host=host}));CollectorRepair47.Visibility=wait?.Kind is "Blocked" or "Disabled" or "Unknown" or "Unreachable" or "RepairFailed" or "InvalidResult" or "AccessDenied"||repairingCollectors47.Contains(host)?Visibility.Visible:Visibility.Collapsed;CollectorRepair47.IsEnabled=!TransportBudget41.Paused&&!repairingCollectors47.Contains(host);CollectorRepair47.Content=repairingCollectors47.Contains(host)?"Восстановление опроса…":wait?.Kind=="Disabled"?"Включить опрос":"Восстановить опрос";CollectorNotice43.Visibility=wait==null?Visibility.Collapsed:Visibility.Visible;PollReset44.IsEnabled=!TransportBudget41.Paused&&!repairingCollectors47.Contains(host); if(wait==null)return;var seconds=(int)Math.Ceiling((wait.Next-DateTimeOffset.UtcNow).TotalSeconds);CollectorNoticeText43.Text=wait.Message+"\n"+(TransportBudget41.Paused?"Опрос выключен — автоматический повтор приостановлен":wait.Next==DateTimeOffset.MaxValue?"Автоматический повтор остановлен; доступна адресная диагностика":seconds>0?"Повторная проверка через "+seconds/60+" мин "+seconds%60+" с":"Повторная проверка разрешена по расписанию")+". Задания обновлений не останавливаются.";}
 async void PollReset44_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  if(TransportBudget41.Paused){Notify("Для повторной проверки включите опрос.");return;}
  var row=Current();var target=ConnectionStore.Capture(row.Target);var generation=PollingControl43.Token;
  if(!repairingCollectors47.Add(row.Host))return;
  try{
   RefreshCollector44(row.Host);
   await CompleteCollectorRepair47(target,generation,remote.ResetPolling44(target),()=>{
    ReadSucceeded34(row.Host,"флаги и аптайм");ReadSucceeded34(row.Host,"фоновая активность");return Task.CompletedTask;
   },message=>{Event31(row.Host,"poll-reset44",DateTimeOffset.UtcNow.ToString("o"),"Восстановление служебной проверки","Info",message);if(CurrentHost(row.Host))Notify(message);});
  }catch(OperationCanceledException) when(generation.IsCancellationRequested||TransportBudget41.Paused){}
  catch(Exception) when(closing||!CurrentEndpoint(target)){}
  finally{repairingCollectors47.Remove(row.Host);if(CurrentHost(row.Host)){RefreshCollector44(row.Host);ShowCurrent();}}
 });
}
