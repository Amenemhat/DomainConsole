using System.Text.Json;
namespace DomainConsole;
public sealed class WsusReport {
 public List<WsusFailedUpdate> FailedUpdates {get;set;}=new();public string FailureSummary {get;set;}="";public string FailureContext {get;set;}="";public string FailureDetailsError {get;set;}="";public string FullDomainName {get;set;}="";public string LastReportedUtc {get;set;}="";public string LastSyncUtc {get;set;}="";public string LastSyncResult {get;set;}="";
 public int UnknownCount {get;set;}public int NotApplicableCount {get;set;}public int NotInstalledCount {get;set;}public int DownloadedCount {get;set;}public int InstalledCount {get;set;}public int InstalledPendingRebootCount {get;set;}public int FailedCount {get;set;}
 public bool Fresh(string started)=>DateTimeOffset.TryParse(started,out var begin)&&DateTimeOffset.TryParse(LastReportedUtc,out var at)&&at>=begin;
 public string Severity(string started){
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
 public string Format(string server,string started){bool fresh=DateTimeOffset.TryParse(started,out var begin)&&DateTimeOffset.TryParse(LastReportedUtc,out var at)&&at>=begin;return "Сервер WSUS: "+server+"\nКлиент: "+FullDomainName+"\nПолучено: "+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"\nПоследний отчёт клиента: "+LocalDateConverter.Format(LastReportedUtc)+"\nПоследний поиск / синхронизация клиента с WSUS: "+LocalDateConverter.Format(LastSyncUtc)+" · "+SyncLabel(LastSyncResult)+(SyncExplanation.Length>0?"\n"+SyncExplanation:"")+"\n"+(string.IsNullOrWhiteSpace(started)?"Свежесть относительно задания не проверялась.":fresh?"Свежий отчёт получен сервером после начала задания. Точная связь с запросом не устанавливается.":"Свежий отчёт после начала задания не подтверждён.")+"\nУстановлено: "+InstalledCount+"; скачано: "+DownloadedCount+"; не установлено: "+NotInstalledCount+"\nОшибок: "+FailedCount+"; нужна перезагрузка: "+InstalledPendingRebootCount+"; неизвестно: "+UnknownCount+"\nНеприменимо: "+NotApplicableCount+(FailedUpdates.Count>0?"\n"+string.Join("\n",FailedUpdates.Select(f=>"Ошибка установки: "+f.Title+"; KB "+f.KB+"; ID "+f.Id)):"")+(FailureSummary.Length>0?"\nПодробности WSUS и клиента:\n"+FailureSummary:FailedCount>0?"\nКод причины пока не получен. Для выбранного ПК прочитайте подробный отчёт сервера.":"")+(FailureDetailsError.Length>0?"\nДетализация недоступна: "+FailureDetailsError:"")+"\nСводка сервера по всем обновлениям; число неустановленных не равно числу одобренных и применимых сейчас.";}
}

