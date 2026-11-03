using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void Disconnect54_Click(object sender,RoutedEventArgs e){
  var row=Current();var profile=ConnectionStore.Profiles.GetValueOrDefault(row.Host)??new();
  profile.Disconnected54=true;profile.Generation54=DateTime.UtcNow.Ticks;profile.Address="";ConnectionStore.Profiles[row.Host]=profile;ConnectionStore.Save();
  liveConnections.Remove(row.Host);liveReports.Remove(row.Host);clientConnections.Remove(row.Host);Store.Write("client-connections.json",clientConnections);
  Event31(row.Host,"manual-disconnect54",profile.Generation54.ToString(),"Подключение отключено вручную","Info","Новые обращения остановлены; переданные клиенту задания продолжают работу. Для проверки результата переподключитесь по IP.");
  RefreshOutcomes();ShowCurrent();
 }
 async void DiagnoseReport54_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{var row=Current();await ReadDelayedQueue54(row.Host);await FetchWsusReport(row);Notify("Диагностика очереди и чтение WSUS завершены; результаты доступны в критерии отчёта. Очередь не очищалась.");});
 static void MergeUpdateHistory54(RemoteJob? job,string host,PollResult data){
  var target=job?.Targets.FirstOrDefault(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase));if(target==null)return;
  foreach(var update in data.Updates.Where(u=>u.ResultCode.HasValue||u.Download.HasValue)){
   string key=update.Id+"|"+update.Revision+"|"+update.Title+"|"+update.Started54;
   var previous=target.UpdateHistory54.FindIndex(u=>u.Id+"|"+u.Revision+"|"+u.Title+"|"+u.Started54==key);
   if(previous<0)target.UpdateHistory54.Add(Store.Clone<UpdateAttempt54>(update));else if(!target.UpdateHistory54[previous].ResultCode.HasValue||update.ResultCode.HasValue)target.UpdateHistory54[previous]=Store.Clone<UpdateAttempt54>(update);
  }
  foreach(var update in target.UpdateHistory54){if(!data.Updates.Any(u=>u.Id==update.Id&&u.Revision==update.Revision&&u.Title==update.Title&&u.Started54==update.Started54))data.Updates.Add(System.Text.Json.JsonSerializer.Deserialize<UpdateOutcome>(System.Text.Json.JsonSerializer.Serialize(update,Store.Options),Store.Options)!);}
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
 string SelectedUpdateSource54()=>UpdateSource54.SelectedIndex switch{1=>"WSUS",2=>"WindowsUpdate",_=>"Policy"};
 void CollectorDetails54_Click(object sender,RoutedEventArgs e){CollectorNotice43.Visibility=Visibility.Visible;((System.Windows.Controls.Expander)CollectorNotice43.Child).IsExpanded=true;CollectorPopup54.IsOpen=!CollectorPopup54.IsOpen;}
 void RecordReportReceipt54(string host,WsusReport report,string server){
  var previous=reportCache.GetValueOrDefault(host);
  bool newer=DateTimeOffset.TryParse(report.LastReportedUtc,out var incoming)&&incoming<=DateTimeOffset.UtcNow.AddMinutes(1)&&
   (previous==null||previous.Server!=server||!DateTimeOffset.TryParse(previous.Report.LastReportedUtc,out var old)||incoming>old);
  if(!newer)return;
  Event31(host,"report-received54",server+"|"+report.LastReportedUtc,"Получен новый отчёт WSUS","Info","Обнаружен консолью сейчас; время отчёта на сервере: "+Display36.Date(report.LastReportedUtc)+". К установке по отчёту: "+report.NeededCount+"; ошибок: "+report.FailedCount+". Сопоставление с результатом клиента — в текущем статусе.",scope:server);
  if(reportWaiting.TryGetValue(host,out var pending)){
   var requested=pending.Requested54==default?pending.Until.AddMinutes(-15):pending.Requested54;
   if(incoming>=requested){reportWaiting.Remove(host);Store.Write("report-waits.json",reportWaiting);}
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
