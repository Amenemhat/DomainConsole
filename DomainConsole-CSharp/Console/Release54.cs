using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
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
