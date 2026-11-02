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
  collectorActivity53[key]=new(status=="Stopping"?status:Field(state,"Phase"),reason,Field(run,"StartedUtc"),Field(state,"UpdatedUtc"),DateTimeOffset.UtcNow);
 }
}
public partial class MainWindow {
 // A missing or incomplete lookup is not a replacement for a previously read report.
 // Keep its timestamp and identity; expose the failed lookup separately.
 bool ApplyOverviewReply53(ComputerRow row,OverviewReply? reply,string server){
  string error=reply==null?"Нет ответа WSUS для клиента":reply.Error;
  if(error.Length==0&&reply!.Found&&reply.Report==null)error="WSUS подтвердил клиента, но не вернул отчёт";
  if(error.Length==0&&reply!.Found){
   var started=reportCache.GetValueOrDefault(row.Host)?.Started??"";
   SaveReportCache(row.Host,reply.Report!,server,started,false);
   overviewErrors.Remove(row.Host);serverReports[row.Host]=reply.Report!.Format(server,started);reportSeverity[row.Host]=reply.Report.Severity(started);return true;
  }
  if(error.Length==0)error="Клиент не найден при последнем чтении WSUS; прежние результаты сохранены";
  overviewErrors[row.Host]=error;
  if(reportCache.TryGetValue(row.Host,out var existing)&&CacheMatches(existing)&&existing.Found){
   serverReports[row.Host]=CachedReportText(existing)+"\n\n## Последнее чтение WSUS\n"+error;return false;
  }
  if(reply is {Found:false,Error:""}){
   reportCache[row.Host]=new(){Found=false,Host41=row.Host,Endpoint41=Identity40.EndpointKey(new TargetRecord{Host=row.Host}),Server=server,ReceivedUtc=DateTimeOffset.UtcNow.ToString("o")};
   liveReports.Add(row.Host);serverReports[row.Host]="Клиент не найден в WSUS. Доступность проверяется отдельно.";
  }
  return false;
 }
}
