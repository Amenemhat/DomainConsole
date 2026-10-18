using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class WsusFailedUpdate {
 public string Id {get;set;}="";public string Title {get;set;}="";public string KB {get;set;}="";public int Revision {get;set;}
 public List<WsusUpdateEvent> Events {get;set;}=new();public string Detail {get;set;}="";
}
public sealed class WsusUpdateEvent {
 public string UpdateId {get;set;}="";public string Date {get;set;}="";public int Revision {get;set;}public int EventId {get;set;}
 public string Type {get;set;}="";public string Message {get;set;}="";public string Status {get;set;}="";public bool IsError {get;set;}public long? ErrorCode {get;set;}
}
public sealed class ClientUpdateHistory {
 public string Id {get;set;}="";public string Date {get;set;}="";public int Revision {get;set;}public int HResult {get;set;}public int UnmappedResult {get;set;}public int ResultCode {get;set;}public int Operation {get;set;}
}
public sealed class FailureHistoryReply {public List<ClientUpdateHistory> History {get;set;}=new();public int Limit {get;set;}public int Total {get;set;}}
public sealed class WsusEventReply {public List<WsusUpdateEvent> Events {get;set;}=new();public string Error {get;set;}="";public bool Truncated {get;set;}}
public sealed partial class RemoteService {
 public Task<string> FailureHistory(TargetRecord target,List<WsusFailedUpdate> failures){
  var ids=string.Join(",",failures.Select(f=>PowerShellBridge.Literal(f.Id)));
  var script="$ids=@("+ids+");$search=New-Object -ComObject Microsoft.Update.Searcher;$total=$search.GetTotalHistoryCount();$count=[Math]::Min(500,$total);$items=@();if($count -gt 0){$items=@($search.QueryHistory(0,$count)|Where-Object {$ids -contains [string]$_.UpdateIdentity.UpdateID}|ForEach-Object {@{Id=[string]$_.UpdateIdentity.UpdateID;Revision=[int]$_.UpdateIdentity.RevisionNumber;Date=[DateTime]::SpecifyKind($_.Date,[DateTimeKind]::Utc).ToString('o');HResult=[int]$_.HResult;UnmappedResult=[int]$_.UnmappedResultCode;ResultCode=[int]$_.ResultCode;Operation=[int]$_.Operation}})};@{History=@($items);Limit=$count;Total=$total}|ConvertTo-Json -Depth 5 -Compress";
  return HistoryCore(target,script,new CancellationTokenSource(TimeSpan.FromSeconds(30)));
 }
 async Task<string> HistoryCore(TargetRecord target,string script,CancellationTokenSource cancellation){using(cancellation)return await PowerShellBridge.Execute(Invoke(target,LiveProbeWrapper(script)),cancellation.Token);}
 public static string WsusEventsScript(string computer,IEnumerable<string> updateIds,bool ssl,int port)=>
  "[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,"+(ssl?"$true":"$false")+","+port+");$name="+PowerShellBridge.Literal(computer)+";$scope=New-Object Microsoft.UpdateServices.Administration.ComputerTargetScope;$scope.NameIncludes=$name.Split('.')[0];$targets=@($s.GetComputerTargets($scope)|Where-Object {$_.FullDomainName -eq $name});if($targets.Count -ne 1){throw 'Клиент событий WSUS не найден однозначно по полному имени'};$target=$targets[0];$ids=@("+string.Join(",",updateIds.Select(PowerShellBridge.Literal))+");$until=[DateTime]::UtcNow;$since=$until.AddDays(-14);$items=@();$errorText='';$truncated=$false;try{$events=@($s.GetUpdateEventHistory($since,$until,$target)|Where-Object {$_.HasAssociatedUpdate -and $ids -contains [string]$_.UpdateId.UpdateId}|Sort-Object CreationDate -Descending);$truncated=$events.Count -gt 300;foreach($event in @($events|Select-Object -First 300)){$message='';try{$message=[string]$event.Message;if([string]::IsNullOrWhiteSpace($message)){$message='Описание события WSUS недоступно'} }catch{$message='Описание события недоступно: '+$_.Exception.Message};$code=$null;if($event.IsError){try{$code=[long]$event.ErrorCode}catch{}};$items+=@{UpdateId=[string]$event.UpdateId.UpdateId;Revision=[int]$event.UpdateId.RevisionNumber;Date=[DateTime]::SpecifyKind($event.CreationDate,[DateTimeKind]::Utc).ToString('o');EventId=[int]$event.WsusEventId;Type=[string]$event.WsusEventId;Message=$message;Status=[string]$event.Status;IsError=[bool]$event.IsError;ErrorCode=$code}}}catch{$errorText=$_.Exception.Message};@{Events=@($items);Error=$errorText;Truncated=$truncated}|ConvertTo-Json -Depth 6 -Compress";
 public async Task<WsusEventReply> WsusFailureEvents(WsusSettings settings,string computer,List<WsusFailedUpdate> updates){
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
  string output=await PowerShellBridge.Execute(Invoke(settings.Host,WsusEventsScript(computer,updates.Select(u=>u.Id),settings.Ssl,settings.Port)),timeout.Token);
  return JsonSerializer.Deserialize<WsusEventReply>(output,Store.Options)??throw new Exception("Пустой ответ событий WSUS");
 }
}
public static class WsusFailurePresentation {
 public static string EventText(WsusUpdateEvent item,bool recommend=true){
  string text=LocalDateConverter.Format(item.Date)+" · ревизия "+item.Revision+" · событие "+item.EventId+" · "+item.Type+" · состояние: "+item.Status;
  bool missing=string.IsNullOrWhiteSpace(item.Message)||item.Message.Contains("ReportingEvent.",StringComparison.OrdinalIgnoreCase)||item.Message.StartsWith("Описание события недоступно");
  text+="\n"+(missing?"Описание события WSUS не расшифровано; исходные данные: "+item.Message:item.Message);
  if(item.IsError&&item.ErrorCode.HasValue&&item.ErrorCode!=0){int code=unchecked((int)item.ErrorCode.Value);text+="\nКод события WSUS: "+ExecutionDiagnosis.Code(code)+" · "+ExecutionDiagnosis.Explain(code)+("");}
  else if(item.IsError||missing)text+="\nКод причины не получен. Номер события не является HRESULT.";
  return text;
 }
 public static string HistoryText(ClientUpdateHistory item)=>LocalDateConverter.Format(item.Date)+" · ревизия "+item.Revision+" · "+(item.Operation==1?"установка":"удаление")+" · "+(item.ResultCode switch {0=>"не начато",1=>"выполняется / результат не окончательный",2=>"успешно",3=>"с замечаниями",4=>"неудача",5=>"отменено",_=>"результат неизвестен"})+" · ResultCode "+item.ResultCode+" · HRESULT "+ExecutionDiagnosis.Code(item.HResult)+(item.HResult!=0?" · "+ExecutionDiagnosis.Explain(item.HResult):"")+(item.UnmappedResult!=0&&item.UnmappedResult!=item.HResult?" · исходный код "+ExecutionDiagnosis.Code(item.UnmappedResult):"");
 public static string Describe(WsusFailedUpdate update,IEnumerable<ClientUpdateHistory> history,bool reboot=false){
  var lines=new List<string>{update.Title+" · KB "+update.KB+" · сводка WSUS: неудача · ревизия "+update.Revision};
  var events=update.Events.OrderByDescending(e=>e.Date).ToList();
  if(events.Count==0)lines.Add("События WSUS для этого обновления за последние 14 дней не получены.");
  else{lines.Add("Последнее событие WSUS:\n"+EventText(events[0],!reboot));var failure=events.FirstOrDefault(e=>e.IsError);if(failure!=null&&failure!=events[0])lines.Add("Предыдущее событие неудачи WSUS (не причина текущего задания):\n"+EventText(failure,false));}
  var entries=history.Where(h=>h.Id.Equals(update.Id,StringComparison.OrdinalIgnoreCase)&&h.Operation==1).OrderByDescending(h=>h.Date).ToList();
  var matching=entries.Where(h=>update.Revision==0||h.Revision==update.Revision).ToList();
  var latest=matching.FirstOrDefault();var failed=matching.FirstOrDefault(h=>h.ResultCode is 3 or 4 or 5);
  if(latest==null){lines.Add("Совпадающей записи установки нет в проверенной клиентской истории (не более 500 записей). Это не доказывает отсутствие сбоя; проверьте также события скачивания.");if(entries.Count>0)lines.Add("Другая ревизия, только для справки: "+HistoryText(entries[0]));}
  else{
   lines.Add("Клиентская история, последняя попытка:\n"+HistoryText(latest));
   if(failed!=null&&failed!=latest)lines.Add("Клиентская история, прежняя неудачная попытка:\n"+HistoryText(failed));
   if(latest.ResultCode==2&&latest.HResult==0)lines.Add("Данные расходятся: клиентская история сообщает успех, сводка WSUS сохраняет неудачу. Успешная запись не объясняет серверный Failed.");
   if(latest.ResultCode==1||unchecked((uint)latest.HResult)==0x80242014)lines.Add("Результат истории ещё не окончательный; проверьте необходимость перезагрузки. Не считать эту запись новой ошибкой текущего задания.");
  }
  if(!events.Any(e=>e.IsError&&e.ErrorCode.HasValue&&e.ErrorCode!=0)&&(failed==null||failed.HResult==0))lines.Add("Код соответствующего сбоя не получен. Сбор WindowsUpdate / BITS и журнала пакета поможет уточнить причину; установка автоматически не запускается.");
  
  lines.Add("История и события описывают отдельные попытки. Их совпадение с текущим заданием не установлено.");
  return string.Join("\n",lines);
 }
}
public partial class MainWindow {
 readonly Dictionary<string,string> wsusFailureDetails=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> failureReading=new(StringComparer.OrdinalIgnoreCase);
 async Task ExplainServerFailures(ComputerRow row,WsusReport report){
  if(report.FailedCount<=0){wsusFailureDetails.Remove(row.Host);report.FailureContext="";report.FailureSummary="";if(IsCurrent(row))ShowCurrent();return;}
  if(!failureReading.Add(row.Host))return;
  try{
   var settings=ReadWsus();string server=settings.Host+":"+settings.Port;
   var lines=new List<string>{"Сводка сервера и подробные события — отдельные источники. Результат текущего задания показан выше."};
   if(report.FailureDetailsError.Length>0)lines.Add("Список обновлений неполный: "+report.FailureDetailsError);
   var history=new List<ClientUpdateHistory>();
   if(report.FailedUpdates.Count>0){
    try{var reply=await remote.WsusFailureEvents(settings,ConnectionStore.WsusName(row.Host),report.FailedUpdates);foreach(var u in report.FailedUpdates)u.Events=reply.Events.Where(e=>e.UpdateId.Equals(u.Id,StringComparison.OrdinalIgnoreCase)).ToList();if(reply.Error.Length>0)lines.Add("События WSUS недоступны: "+reply.Error);if(reply.Truncated)lines.Add("История событий WSUS ограничена 300 записями за 14 дней.");}catch(Exception ex){lines.Add("События WSUS недоступны: "+ex.Message);}
    try{var reply=JsonSerializer.Deserialize<FailureHistoryReply>(await remote.FailureHistory(row.Target,report.FailedUpdates),Store.Options);history=reply?.History??new();if(reply!=null&&reply.Total>reply.Limit)lines.Add("Проверено последних записей клиентской истории: "+reply.Limit+" из "+reply.Total+".");}catch(Exception ex){lines.Add("Клиентская история недоступна: "+ex.Message);}
    foreach(var update in report.FailedUpdates){update.Detail=WsusFailurePresentation.Describe(update,history,RebootReason(row).Length>0);lines.Add(update.Detail);}
   }else lines.Add("WSUS сообщает ошибки, но список обновлений недоступен. Причина не установлена.");
   if(!reportCache.TryGetValue(row.Host,out var cached)||!ReferenceEquals(cached.Report,report)||cached.Server!=server||server!=OverviewServer)return;
   report.FailureContext=string.Join("\n\n",lines);
   report.FailureSummary=string.Join("\n",report.FailedUpdates.Select(u=>"KB "+u.KB+": сохранённая неудача WSUS; события и коды см. в разделе «Сведения WSUS и клиентская история»."))+(RebootReason(row).Length>0?"\nКлиент подтверждает необходимость перезагрузки. Это приоритетное действие; данные WSUS показаны отдельно от клиентского флага.":"");
   wsusFailureDetails[row.Host]="## Технические подробности · WSUS и история\n"+report.FailureContext;
   serverReports[row.Host]=report.Format(server,cached.Started);Store.Write("wsus-report-cache.json",reportCache);
   if(IsCurrent(row))ShowCurrent();
  }finally{failureReading.Remove(row.Host);}
 }
}

