using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void Disconnect54_Click(object sender,RoutedEventArgs e){
  var row=Current();TransportBudget41.CancelEndpoint54(ConnectionStore.Capture(new TargetRecord{Host=row.Host}));var profile=ConnectionStore.Profiles.GetValueOrDefault(row.Host)??new();
  profile.Disconnected54=true;profile.Generation54=DateTime.UtcNow.Ticks;profile.Address="";ConnectionStore.Profiles[row.Host]=profile;ConnectionStore.Save();
  liveConnections.Remove(row.Host);liveReports.Remove(row.Host);clientConnections.Remove(row.Host);Store.Write("client-connections.json",clientConnections);
  Event31(row.Host,"manual-disconnect54",profile.Generation54.ToString(),"Подключение отключено вручную","Info","Новые обращения остановлены; переданные клиенту задания продолжают работу. Для проверки результата переподключитесь по IP.");
  RefreshOutcomes();ShowCurrent();
 }
 async void DiagnoseReport54_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{var row=Current();await ReadDelayedQueue54(row.Host);await FetchWsusReport(row);Notify("Диагностика очереди и чтение WSUS завершены; результаты доступны в критерии отчёта. Очередь не очищалась.");});
 static bool MatchesInstallHistory54(ClientUpdateHistory entry,UpdateAttempt54 intent,string jobId,DateTimeOffset now)=>Guid.TryParse(jobId,out _)&&entry.Id.Equals(intent.Id,StringComparison.OrdinalIgnoreCase)&&entry.Revision==intent.Revision&&entry.Operation==1&&entry.Initiator54.Contains(jobId,StringComparison.OrdinalIgnoreCase)&&DateTimeOffset.TryParse(entry.Date,out var at)&&DateTimeOffset.TryParse(intent.Started54,out var start)&&at>=start.AddSeconds(-1)&&at<=now.AddMinutes(1);
 readonly HashSet<string> recoveredHistory54=new();
 async Task RecoverInstallHistory54(TargetRecord endpoint,RemoteJob? job,PollResult data){
  if(job==null||data.State==null||!InactiveEvidence(data)||data.State.UpdateIntents54.Count==0)return;
  var missing=data.State.UpdateIntents54.Where(i=>i.Phase54=="Install"&&!data.Updates.Any(u=>u.Id==i.Id&&u.Revision==i.Revision&&u.ResultCode.HasValue)).ToList();
  if(missing.Count==0||!recoveredHistory54.Add(job.Id+"|"+endpoint.Host))return;
  try{
   var raw=await remote.FailureHistory(endpoint,missing.Select(i=>new WsusFailedUpdate{Id=i.Id,Revision=i.Revision}).ToList());
   if(!CurrentEndpoint(endpoint)||PollingControl43.Token.IsCancellationRequested)return;
   var history=System.Text.Json.JsonSerializer.Deserialize<FailureHistoryReply>(raw,Store.Options)??new();
   foreach(var intent in missing){
    var matches=history.History.Where(h=>MatchesInstallHistory54(h,intent,job.Id,DateTimeOffset.UtcNow)).ToList();
    if(matches.Count!=1)continue;var found=matches[0];
    data.Updates.Add(new(){Id=intent.Id,Revision=intent.Revision,Title=intent.Title,Started54=intent.Started54,Ended54=found.Date,ResultCode=found.ResultCode,HResult=found.HResult,Evidence54="Windows Update history / "+found.Initiator54});
    Event31(endpoint.Host,"install-history54",job.Id+"|"+intent.Id+"|"+intent.Revision+"|"+found.Date,"Восстановлен результат установки из истории Windows Update",found.ResultCode==2?"Success":"Warning",intent.Title+" · "+WsusFailurePresentation.HistoryText(found)+". Совпали UpdateID, ревизия, время и ID задания в инициаторе.",scope:job.Id);
   }
  }catch(Exception ex){Event31(endpoint.Host,"install-history-unavailable54",job.Id,"История установки не прочитана","Warning",ex.Message+". Результат не подменён; повторная установка не запускалась.",scope:job.Id);}
 }
 static void MergeUpdateHistory54(RemoteJob? job,string host,PollResult data){
  var target=job?.Targets.FirstOrDefault(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase));if(target==null)return;
  foreach(var update in data.Updates.Where(u=>u.ResultCode.HasValue||u.Download.HasValue)){
   string key=update.Id+"|"+update.Revision+"|"+update.Title+"|"+update.Started54;
   var previous=target.UpdateHistory54.FindIndex(u=>u.Id+"|"+u.Revision+"|"+u.Title+"|"+u.Started54==key);
   if(previous<0)target.UpdateHistory54.Add(Store.Clone<UpdateAttempt54>(update));else if(!target.UpdateHistory54[previous].ResultCode.HasValue||update.ResultCode.HasValue)target.UpdateHistory54[previous]=Store.Clone<UpdateAttempt54>(update);
  }
  foreach(var update in target.UpdateHistory54){var index=data.Updates.FindIndex(u=>u.Id==update.Id&&u.Revision==update.Revision&&u.Title==update.Title&&u.Started54==update.Started54);var saved=System.Text.Json.JsonSerializer.Deserialize<UpdateOutcome>(System.Text.Json.JsonSerializer.Serialize(update,Store.Options),Store.Options)!;if(index<0)data.Updates.Add(saved);else if(update.ResultCode.HasValue&&!data.Updates[index].ResultCode.HasValue)data.Updates[index]=saved;}
 }
 ClientAssessment41 ApplySourceResult54(ComputerRow row,ClientAssessment41 current,bool active){
  if(active)return current;
  var latest=HostJobs39(row.Host).FirstOrDefault(x=>IsWsusJob(x.Job)&&CurrentIdentity(x.Target));var state=latest.Target?.State;
  if(state==null||!(state.ManagedSource54==false||latest.Job.UpdateSource54=="WindowsUpdate")||state.Status is not ("Completed" or "VerifiedAfterReboot"))return current;
  var step=state.Results.LastOrDefault(x=>x.Kind is "Updates" or "UpdateScan"&&x.ExitCode==0);
  if(step==null)return current;
  var reboot=RebootViewFor40(row);if(reboot.Required==true)return current;
  var boot=rebootSnapshots.GetValueOrDefault(row.Host)?.BootUtc??"";
  if(Criteria45.Newer(boot,step.Ended))return new("Unknown","Последний поиск выполнен до перезагрузки","Источник: Windows Update; результат "+Display36.Date(step.Ended)+" сохранён. Ожидается проверка после загрузки.");
  int count=state.UpdatesRemaining;string source=latest.Job.UpdateSource54=="WindowsUpdate"?"Windows Update":"текущие политики клиента (без WSUS)";
  var result=new ClientAssessment41(count==0?"Success":"Needed",count==0?"Проверено · необходимых обновлений нет":"К установке по поиску клиента: "+count,"Источник: "+source+" · "+Display36.Date(step.Ended)+". Данные WSUS относятся к отдельному источнику.");
  if(criteria45.TryGetValue(row.Host,out var criteria)){
   criteria[0]=new("updates","Обновления · "+source+" · осталось: "+count,count==0?"Success":"Info",count==0?"Success":"Info",new(){new("Успешный поиск клиента: "+Display36.Date(step.Ended),"Success","Success"),new("Установлено в этом запуске: "+state.InstalledCount38,"Info","Info")});
   criteria[1]=criteria[1] with{Text="Отчёт WSUS · отдельный источник",Checks=new(criteria[1].Checks){new("Этот отчёт не подтверждает результат поиска Windows Update","Info","Info")}};
  }
  return result;
 }
 void ExpireReportWaits61(){
  bool changed=false;
  foreach(var pair in reportWaiting){
   var wait=pair.Value;if(wait.TimeoutRecorded61||!ReportWaitExpired61(wait))continue;
   wait.TimeoutRecorded61=true;changed=true;
   Event31(pair.Key,"report-delay54",wait.Id,"Отчёт WSUS задерживается","Warning","Истёк срок ожидания 10 минут. Результат клиентской операции сохранён; дальнейшее фоновое наблюдение учитывается отдельно. Сервер: "+wait.Server54,scope:wait.Id);
  }
  if(changed)Store.Write("report-waits.json",reportWaiting);
 }
 ReportWait30? CurrentReportWait61(string host){
  if(!reportWaiting.TryGetValue(host,out var wait)||(wait.Server54.Length>0&&!wait.Server54.Equals(OverviewServer,StringComparison.OrdinalIgnoreCase)))return null;
  var job=History.FirstOrDefault(j=>j.Id==wait.Id);var target=job?.Targets.FirstOrDefault(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase));
  return target!=null&&(!CurrentIdentity(target)||!CurrentEndpoint(target))?null:wait;
 }
 static bool ReportWaitExpired61(ReportWait30 wait)=>wait.Delayed54||DateTimeOffset.UtcNow>=wait.Until;
 static ReportWait30 NewReportWait54(string id,string stamp,string server=""){var now=DateTimeOffset.UtcNow;var requested=DateTimeOffset.TryParse(stamp,out var at)&&at.Year>2000&&at<=now.AddMinutes(1)?at:now;return new(){Id=id,Server54=server,Requested54=requested,Until=requested.AddMinutes(10),Next=now.AddSeconds(30)};}
 string SelectedUpdateSource54()=>UpdateSource54.SelectedIndex switch{1=>"WSUS",2=>"WindowsUpdate",_=>"Policy"};
 void CollectorDetails54_Click(object sender,RoutedEventArgs e){CollectorNotice43.Visibility=Visibility.Visible;((System.Windows.Controls.Expander)CollectorNotice43.Child).IsExpanded=true;CollectorPopup54.IsOpen=!CollectorPopup54.IsOpen;}
 void RecordReportReceipt54(string host,WsusReport report,string server){
  var previous=reportCache.GetValueOrDefault(host);
  bool valid=DateTimeOffset.TryParse(report.LastReportedUtc,out var incoming)&&incoming.Year>2000&&incoming<=DateTimeOffset.UtcNow.AddMinutes(1);
  bool newer=valid&&(previous==null||previous.Server!=server||!DateTimeOffset.TryParse(previous.Report.LastReportedUtc,out var old)||incoming>old);
  if(newer)Event31(host,"report-received54",server+"|"+report.LastReportedUtc,"Получен новый отчёт WSUS","Info","Обнаружен консолью сейчас; время отчёта на сервере: "+Display36.Date(report.LastReportedUtc)+". К установке по отчёту: "+report.NeededCount+"; ошибок: "+report.FailedCount+". Сопоставление с результатом клиента — в текущем статусе.",scope:server);
  if(valid&&reportWaiting.TryGetValue(host,out var pending)){
   var requested=pending.Requested54==default?pending.Until.AddMinutes(-15):pending.Requested54;
   if((pending.Server54.Length==0||pending.Server54==server)&&incoming>=requested){if(ReportWaitExpired61(pending))Event31(host,"report-late61",pending.Id+"|"+report.LastReportedUtc,"Получен отчёт WSUS после срока ожидания","Info","Время отчёта: "+Display36.Date(report.LastReportedUtc)+". Итог сверки — в текущем статусе; прежнее ожидание завершено.",scope:pending.Id);reportWaiting.Remove(host);Store.Write("report-waits.json",reportWaiting);}
  }
 }
 async Task ReadDelayedQueue54(string host){
  var row=Computers.FirstOrDefault(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase));if(row==null)return;
  var target=ConnectionStore.Capture(row.Target);var server=OverviewServer;var generation=PollingControl43.Token;ClientReportProbe probe;
  try{probe=await remote.ReadReportQueue(target);}catch(Exception ex){probe=new(){CollectedUtc=DateTimeOffset.UtcNow.ToString("o"),QueueError=ex.Message};}
  if(generation.IsCancellationRequested||TransportBudget41.Paused||!CurrentEndpoint(target)||server!=OverviewServer)return;
  RememberQueue52(target,reportCache.GetValueOrDefault(host)?.Report??new(),probe);RefreshOutcomes();
 }
}
