using System.Text.Json;
namespace DomainConsole;
public sealed class WsusReport {
 public int? NeededCount {get;set;}public int ApprovedUnknownCount {get;set;}public int ApprovedDownloadedCount {get;set;}public int ApprovedFailedCount {get;set;}public bool RequiredComplete {get;set;}public string RequiredError {get;set;}="";
 public string TaskEndedUtc {get;set;}="";
 public List<WsusFailedUpdate> FailedUpdates {get;set;}=new();public string FailureSummary {get;set;}="";public string FailureContext {get;set;}="";public string FailureDetailsError {get;set;}="";public string FullDomainName {get;set;}="";public string LastReportedUtc {get;set;}="";public string LastSyncUtc {get;set;}="";public string LastSyncResult {get;set;}="";
 public int UnknownCount {get;set;}public int NotApplicableCount {get;set;}public int NotInstalledCount {get;set;}public int DownloadedCount {get;set;}public int InstalledCount {get;set;}public int InstalledPendingRebootCount {get;set;}public int FailedCount {get;set;}
 public bool Fresh(string started)=>DateTimeOffset.TryParse(DateTimeOffset.TryParse(TaskEndedUtc,out _)?TaskEndedUtc:started,out var begin)&&DateTimeOffset.TryParse(LastReportedUtc,out var at)&&at>=begin;
 public string Freshness(string started){if(DateTimeOffset.TryParse(TaskEndedUtc,out var ended)&&DateTimeOffset.TryParse(LastReportedUtc,out var reported))return reported<ended?"Серверный отчёт предшествует завершению задания; итог этой операции ещё не подтверждён сводкой WSUS.":"Отчёт сервера обновлён после завершения задания. Точная связь с запросом не устанавливается.";return string.IsNullOrWhiteSpace(started)?"Свежесть относительно задания не проверялась.":Fresh(started)?"Свежий отчёт получен сервером после начала задания. Точная связь с запросом не устанавливается.":"Свежий отчёт после начала задания не подтверждён.";}
 public string Severity(string started){
  if(InstalledPendingRebootCount>0)return "Reboot";
  if(FailedCount>0)return "Error";
  if(LastSyncResult is "Failed" or "FailedWithErrors")return "SyncError";
  if(InstalledPendingRebootCount>0)return "Reboot";
  if(LastSyncResult!="Succeeded")return "Unknown";
  if(!DateTimeOffset.TryParse(LastReportedUtc,out var at)||at.Year<2000)return "Unknown";
  if(DateTimeOffset.UtcNow-at>TimeSpan.FromDays(1))return "Stale";
  return "Success";
 }
 public static string SyncLabel(string value)=>value switch {"Succeeded"=>"успешно", "Failed" or "FailedWithErrors"=>"неудача", "Canceled" or "Cancelled"=>"отменено", "NotPerformed"=>"не выполнялось",_=>"неизвестно ("+value+")"};
 public string SyncExplanation=>LastSyncResult is "Failed" or "FailedWithErrors"?"Последний поиск клиента с WSUS завершился неудачей. Это сохранённый результат, не проверка текущей доступности. Код причины в LastSyncResult отсутствует. Выполните новый поиск; при повторении соберите клиентский журнал вокруг времени сбоя.":"";
 public string Format(string server,string started){
  var lines=new List<string>{"## Данные сервера WSUS","Сервер WSUS: "+server,"Клиент: "+FullDomainName,"Получено: "+DateTime.Now.ToString("dd.MM HH:mm:ss"),"## Требуется клиенту",NeededCount.HasValue?(DateTimeOffset.TryParse(LastReportedUtc,out var reportTime31)&&DateTimeOffset.UtcNow-reportTime31<TimeSpan.FromDays(1)?"К установке: ":"По сохранённому отчёту к установке: ")+NeededCount+(RequiredComplete?" — одобрены для этого клиента":" или больше — применимость части обновлений неизвестна"):"К установке: — · количество пока не определено"};
  if(NeededCount.HasValue)lines.Add("Из них скачано: "+ApprovedDownloadedCount+"; с неудачей последней попытки: "+ApprovedFailedCount);
  if(InstalledPendingRebootCount>0)lines.Add("Ожидают перезагрузки: "+InstalledPendingRebootCount);
  if(ApprovedUnknownCount>0)lines.Add("Одобрены, применимость не определена: "+ApprovedUnknownCount);
  if(RequiredError.Length>0)lines.Add("Подсчёт неполный: "+MainWindow.ConnectionBrief31(RequiredError));
  string Date(string v)=>DateTimeOffset.TryParse(v,out var at)?at.ToLocalTime().ToString("dd.MM HH:mm:ss"):"нет данных";
  lines.Add("## Актуальность данных");lines.Add("Последний отчёт клиента: "+Date(LastReportedUtc));lines.Add("Последний поиск клиента с WSUS: "+Date(LastSyncUtc)+" · "+SyncLabel(LastSyncResult));lines.Add(Freshness(started));
  if(SyncExplanation.Length>0)lines.Add(SyncExplanation);
  if(FailedCount>0){lines.Add("## Сохранённые неудачи WSUS");foreach(var f in FailedUpdates){var code=f.Events.Where(e=>e.IsError&&e.ErrorCode.HasValue&&e.ErrorCode!=0).OrderByDescending(e=>e.Date).FirstOrDefault();lines.Add("KB "+f.KB+(code==null?" · код причины не получен":" · "+DomainConsole.Shared.ExecutionDiagnosis.Code(unchecked((int)code.ErrorCode!.Value))+" · "+DomainConsole.Shared.ExecutionDiagnosis.Explain(unchecked((int)code.ErrorCode.Value))));}if(FailedUpdates.Count==0)lines.Add("В сводке: "+FailedCount+". Подробности не получены.");}
  lines.Add("## Техническая статистика · все обновления");lines.Add("Установлено: "+InstalledCount+"; скачано: "+DownloadedCount+"; не установлено: "+NotInstalledCount);lines.Add("Ошибок: "+FailedCount+"; нужна перезагрузка: "+InstalledPendingRebootCount+"; неизвестно: "+UnknownCount);lines.Add("Неприменимо: "+NotApplicableCount);if(FailureDetailsError.Length>0)lines.Add(FailureDetailsError);if(FailureSummary.Length>0&&!FailureSummary.Contains("см. в разделе"))lines.Add(FailureSummary);
  return string.Join("\n",lines);
 }
}
