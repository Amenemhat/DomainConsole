using System.Collections.Concurrent;
using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;

public sealed record CollectorActivity53(string Stage, string Reason, string Started, string Heartbeat, DateTimeOffset Received) {
 public bool Animate(DateTimeOffset now)=>Stage is "Reboot" or "Activity" && DateTimeOffset.TryParse(Heartbeat,out var at) && at<=now.AddMinutes(1) && now-at<=TimeSpan.FromSeconds(15) && now-Received<=TimeSpan.FromSeconds(20);
 public string Text=>Stage switch {"Reboot"=>"Сборщик: чтение флагов и времени загрузки", "Activity"=>"Сборщик: чтение активности обновлений", "Stopping"=>"Сборщик: подтверждение остановки", _=>"Сборщик: ожидание результата"};
 public string Details(DateTimeOffset now)=>Text+"\n"+Reason+(DateTimeOffset.TryParse(Started,out var start)?"\nС начала запуска: "+MainWindow.Age(now-start):"")+(Heartbeat.Length>0?"\nПоследний сигнал: "+Display36.Date(Heartbeat):"\nСигнал жизнеспособности пока не получен");
}
public sealed partial class RemoteService {
 static readonly ConcurrentDictionary<string,CollectorActivity53> collectorActivity53=new();
 public static CollectorActivity53? CollectorActivity(TargetRecord target)=>collectorActivity53.GetValueOrDefault(TransportBudget41.Key(target));
 static void ObserveCollector53(string key,JsonElement root,string status,string reason){
  string Field(JsonElement value,string name)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(name,out var field)&&field.ValueKind==JsonValueKind.String?field.GetString()??"":"";
  root.TryGetProperty("State",out var state);root.TryGetProperty("Run",out var run);
  bool same=Field(run,"Id").Length>0&&Field(run,"Id")==Field(state,"Id");
  collectorActivity53[key]=new(status=="Stopping"?status:same?Field(state,"Phase"):"",reason,Field(run,"StartedUtc"),same?Field(state,"UpdatedUtc"):"",DateTimeOffset.UtcNow);
 }
}
public partial class MainWindow {
 sealed record InstallerObservation53(string Endpoint,string Stamp);
 readonly Dictionary<string,InstallerObservation53> installerObservations53=new(Store.Read("installer-observations53.json",new Dictionary<string,InstallerObservation53>()),StringComparer.OrdinalIgnoreCase);
 void ObserveInstaller53(ComputerRow row,LiveUpdateActivity live){
  // Only WUA's confirmed installer state invalidates the negative result.
  // BITS downloads and a TiWorker process alone do not prove installation.
  if(!live.InstallerBusy||!DateTimeOffset.TryParse(live.CollectedUtc,out var at)||at>DateTimeOffset.UtcNow.AddMinutes(1)||DateTimeOffset.UtcNow-at>TimeSpan.FromSeconds(90))return;
  if(installerObservations53.TryGetValue(row.Host,out var old)&&DateTimeOffset.TryParse(old.Stamp,out var prior)&&prior>=at)return;
  installerObservations53[row.Host]=new(Identity40.EndpointKey(row.Target),live.CollectedUtc);
  Store.Write("installer-observations53.json",installerObservations53);
 }
 bool InstallerChanged53(ComputerRow row,string checkedAt)=>installerObservations53.TryGetValue(row.Host,out var observed)&&SavedEvidence55.Matches(observed.Endpoint,row.Target)&&DateTimeOffset.TryParse(observed.Stamp,out var at)&&at<=DateTimeOffset.UtcNow.AddMinutes(1)&&(!DateTimeOffset.TryParse(checkedAt,out var read)||at>read);
 static bool RecoverableCollector53(PollWait42? wait)=>
  wait is {Kind:"Blocked"}&&wait.Message.StartsWith("Три сбоя сборщика подряд",StringComparison.Ordinal)||
  wait is {Kind:"Unknown"}&&(wait.Message.StartsWith("Предыдущий исполнитель",StringComparison.Ordinal)||wait.Message.StartsWith("Процесс предыдущего сборщика",StringComparison.Ordinal));
 async Task<RebootSnapshot?> RecoverCollectorStatus53(TargetRecord target,Func<TargetRecord,Task<string>>? substitute=null){
  if(substitute!=null){await substitute(target);return null;}
  var generation=PollingControl43.Token;
  await remote.RepairCollector47(target);
  generation.ThrowIfCancellationRequested();
  if(closing||TransportBudget41.Paused||!CurrentEndpoint(target))throw new OperationCanceledException("Контекст восстановления изменён");
  if(statusReads.TryGetValue(target.Host,out var previous))await previous.WaitAsync(TimeSpan.FromSeconds(15),generation);
  generation.ThrowIfCancellationRequested();
  if(!CurrentEndpoint(target))throw new OperationCanceledException("Адрес клиента изменён");
  return await remote.ReadRebootState(target,generation);
 }
 void AllowStatusRetry53(string host){
  // Reopening admission does not prove successful reading. Retain the reason
  // until a fresh snapshot resolves it, but release the obsolete deadline.
  foreach(var operation in new[]{"флаги и аптайм","фоновая активность"})
   if(readFailures34.TryGetValue(host+"|"+operation,out var failure))failure.Next=DateTimeOffset.MinValue;
 }
bool NeedsRebootRead53(ComputerRow row){
 if(rebootWatches.TryGetValue(row.Host,out var watch)&&!watch.Done)return true;
 if(maintenancePlans.TryGetValue(row.Host,out var plan)&&!plan.Done)return true;
 // Missing/invalidated evidence needs a retry. A healthy same-boot snapshot
 // is not polled every five minutes merely because the WSUS card is not green.
 return !RebootViewFor40(row).Fresh;
}

 // A missing or incomplete lookup is not a replacement for a previously read report.
 // Keep its timestamp and identity; expose the failed lookup separately.
 bool ApplyOverviewReply53(ComputerRow row,OverviewReply? reply,string server,bool persist58=true){
  string error=reply==null?"Нет ответа WSUS для клиента":reply.Error;
  if(error.Length==0&&reply!.Found&&reply.Report==null)error="WSUS подтвердил клиента, но не вернул отчёт";
  if(error.Length==0&&reply!.Found){
   var started=reportCache.GetValueOrDefault(row.Host)?.Started??"";
   SaveReportCache(row.Host,reply.Report!,server,started,false,persist58);
   overviewErrors.Remove(row.Host);serverReports[row.Host]=reply.Report!.Format(server,started);reportSeverity[row.Host]=reply.Report.Severity(started);return true;
  }
  if(error.Length==0)error="Клиент не найден при последнем чтении WSUS; прежние результаты сохранены";
  overviewErrors[row.Host]=error;
  if(reportCache.TryGetValue(row.Host,out var existing)&&CacheMatches(existing)){
   existing.LookupError53=error;existing.LookupUtc53=DateTimeOffset.UtcNow.ToString("o");serverReports[row.Host]=CachedReportText(existing)+(existing.Found?"":"\n\n## Последнее чтение WSUS\n"+Display36.Date(existing.LookupUtc53)+" · "+error);return false;
  }
  if(reply is {Found:false,Error:""}){
   reportCache[row.Host]=new(){Found=false,Host41=row.Host,Endpoint41=Identity40.EndpointKey(new TargetRecord{Host=row.Host}),Server=server,ReceivedUtc=DateTimeOffset.UtcNow.ToString("o")};
   liveReports.Add(row.Host);serverReports[row.Host]="Клиент не найден в WSUS. Доступность проверяется отдельно.";
  }
  if(!serverReports.ContainsKey(row.Host))serverReports[row.Host]="Не удалось прочитать данные WSUS: "+error;
  return false;
 }
}
