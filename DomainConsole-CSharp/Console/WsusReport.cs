using System.Text.Json;
namespace DomainConsole;
public sealed class WsusRequiredUpdate37 {public string Id {get;set;}="";public int Revision {get;set;}public string Title {get;set;}="";public string KB {get;set;}="";public string State {get;set;}="";}
public sealed class WsusReport {
 public List<WsusRequiredUpdate37> RequiredUpdates {get;set;}=new();public bool RequiredListComplete {get;set;}public string RequiredListError {get;set;}="";
 public int? ClientSearchCount31 {get;set;}public string ClientSearchUtc31 {get;set;}="";
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
  if(DateTimeOffset.UtcNow-at>TimeSpan.FromHours(ReportPresentation35.FreshHours))return "Stale";
  return NeededCount>0?"Needed":NeededCount==0&&RequiredComplete?"Success":"Unknown";
 }
 public static string SyncLabel(string value)=>value switch {"Succeeded"=>"успешно", "Failed" or "FailedWithErrors"=>"неудача", "Canceled" or "Cancelled"=>"отменено", "NotPerformed"=>"не выполнялось",_=>"неизвестно ("+value+")"};
 public string SyncExplanation=>LastSyncResult is "Failed" or "FailedWithErrors"?"Последний поиск клиента с WSUS завершился неудачей. Это сохранённый результат, не проверка текущей доступности. Код причины в LastSyncResult отсутствует. Выполните новый поиск; при повторении соберите клиентский журнал вокруг времени сбоя.":"";
 public string Format(string server,string started){
  string Date(string value)=>ReportPresentation35.Date(value);
  var lines=new List<string>{"## Требуется клиенту",NeededCount.HasValue?"К установке: "+(RequiredComplete?"":"≥ ")+NeededCount+(RequiredComplete?" — одобрены и применимы":" — одобрены; подсчёт применимости неполный"):"К установке: — · количество пока не определено",
   "Последний отчёт клиента: "+Date(LastReportedUtc)+" · "+ReportPresentation35.Age(LastReportedUtc),
   "Последнее соединение с WSUS: "+Date(LastSyncUtc)+" · "+SyncLabel(LastSyncResult)};
  if(FailedCount>0)lines.Add("Ошибок по отчёту WSUS: "+FailedCount);
  if(InstalledPendingRebootCount>0)lines.Add("Требуют перезагрузки по отчёту WSUS: "+InstalledPendingRebootCount);
  if(NeededCount.HasValue)lines.Add("Из них скачано: "+ApprovedDownloadedCount+"; с неудачей последней попытки: "+ApprovedFailedCount);

  if(NeededCount>0){
   lines.Add("## Необходимые обновления · "+NeededCount);
   var updates=RequiredUpdates.GroupBy(u=>u.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderByDescending(u=>u.Revision).First()).ToList();
   int number=0;foreach(var update in updates){string title=update.Title.Length>0?update.Title:"Название не получено";string kb=update.KB.Length>0?"KB"+update.KB.Replace("KB", "",StringComparison.OrdinalIgnoreCase):"";if(kb.Length>0&&!title.Contains(kb,StringComparison.OrdinalIgnoreCase))title=kb+" — "+title;lines.Add((++number)+". "+title+" · "+(update.State=="Downloaded"?"скачано":update.State=="Failed"?"ошибка последней попытки":"не скачано"));}
   if(updates.Count==0)lines.Add("Список обновлений не получен");
   else if(!RequiredListComplete||updates.Count!=NeededCount)lines.Add("Получено названий: "+updates.Count+" из "+NeededCount+" · список неполный");
   if(!RequiredComplete)lines.Add("Применимость части одобренных обновлений пока не определена");
   if(RequiredListError.Length>0)lines.Add("Ограничение списка: "+MainWindow.ConnectionBrief31(RequiredListError));
  }
  if(ApprovedUnknownCount>0)lines.Add("Одобрены, применимость не определена: "+ApprovedUnknownCount);
  if(RequiredError.Length>0)lines.Add("Подсчёт неполный: "+MainWindow.ConnectionBrief31(RequiredError));
  if(ClientSearchCount31.HasValue)lines.Add("Последний успешный поиск на клиенте: найдено "+ClientSearchCount31+" · "+Date(ClientSearchUtc31)+". Отдельный источник; одобрения WSUS не подсчитывает.");
  lines.Add("## Технические сведения · источник и актуальность");lines.Add("Сервер WSUS: "+server);lines.Add("Клиент: "+FullDomainName);lines.Add("Получено: "+DateTimeOffset.Now.ToString("dd.MM.yyyy HH:mm:ss"));lines.Add(Freshness(started));
  if(SyncExplanation.Length>0)lines.Add(SyncExplanation);
  if(FailedCount>0){lines.Add("## Сохранённые неудачи WSUS");foreach(var f in FailedUpdates){var code=f.Events.Where(e=>e.IsError&&e.ErrorCode.HasValue&&e.ErrorCode!=0).OrderByDescending(e=>e.Date).FirstOrDefault();lines.Add("KB "+f.KB+(code==null?" · код причины не получен":" · "+DomainConsole.Shared.ExecutionDiagnosis.Code(unchecked((int)code.ErrorCode!.Value))+" · "+DomainConsole.Shared.ExecutionDiagnosis.Explain(unchecked((int)code.ErrorCode.Value))));}if(FailedUpdates.Count==0)lines.Add("В сводке: "+FailedCount+". Подробности не получены.");}
  lines.Add("## Техническая статистика · все обновления");lines.Add("Установлено: "+InstalledCount+"; скачано: "+DownloadedCount+"; не установлено: "+NotInstalledCount);lines.Add("Ошибок: "+FailedCount+"; нужна перезагрузка: "+InstalledPendingRebootCount+"; неизвестно: "+UnknownCount);lines.Add("Неприменимо: "+NotApplicableCount);if(FailureDetailsError.Length>0)lines.Add(FailureDetailsError);if(FailureSummary.Length>0&&!FailureSummary.Contains("см. в разделе"))lines.Add(FailureSummary);
  return string.Join("\n",lines);
 }
}
