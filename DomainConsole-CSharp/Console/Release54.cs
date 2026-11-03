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
