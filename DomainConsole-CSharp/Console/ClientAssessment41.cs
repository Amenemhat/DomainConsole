using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed record ClientAssessment41(string Kind,string Title,string Details){
 public string Tone=>Kind switch{"Success"=>"Success","Error" or "SyncError"=>"Error","Needed" or "Working"=>"Info",_=>"Warning"};
 public string Glyph=>Kind switch{"Success"=>"Success","Error" or "SyncError"=>"Error","Reboot"=>"Reboot","Working"=>"Working","Needed"=>"Info",_=>"Warning"};
}
public static class ClientReadiness41 {
 public static string IPv4(string value){if(!IPAddress.TryParse(value,out var ip))return "";if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();return ip.AddressFamily==AddressFamily.InterNetwork?ip.ToString():"";}
 public static ClientAssessment41 Evaluate(WsusReport? report,bool saved,bool found,RebootView40 reboot,RemoteState? state,bool active,bool searched,int? remaining,string searchAt,string error,DateTimeOffset now,int hours,string boot=""){
  string dates=report==null?"":"Отчёт WSUS: "+Display36.Date(report.LastReportedUtc,true)+" · "+ReportPresentation35.AgeAt41(report.LastReportedUtc,now);
  ClientAssessment41 Result(string k,string title,string detail)=>new(k,title,detail+(dates.Length>0?"\n"+dates:""));
  if(active)return Result("Working",state?.Stage.Length>0?state.Stage:"Выполняется задание","Ход операции отображается по последнему полученному снимку");
  if(state?.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired" or "SubmissionUnknown")return Result("Error","Ошибка · "+MainWindow.ConnectionBrief31(state.Error.Length>0?state.Error:ComputerRow.StatusText(state.Status)),"Проверьте причину в журнале задания"+(reboot.Fresh&&reboot.Required==true?"\nТребуется перезагрузка · "+reboot.Details:""));
  if(report?.FailedCount>0)return Result("Error","Ошибок по отчёту WSUS: "+report.FailedCount,"Откройте причины в отчёте WSUS"+(reboot.Fresh&&reboot.Required==true?"\nТребуется перезагрузка · "+reboot.Details:""));
  if(report?.LastSyncResult is "Failed" or "FailedWithErrors")return Result("SyncError","Последний поиск WSUS завершился с ошибкой","Поиск: "+Display36.Date(report.LastSyncUtc,true)+" · выполните диагностику связи с WSUS");
  if(reboot.Fresh&&reboot.Required==true)return Result("Reboot","Требуется перезагрузка",reboot.Details);
  if(report==null)return Result("Unknown","Отчёт WSUS не получен","Прочитайте данные сервера WSUS");
  if(!found)return Result("NoWsus","Клиент не найден в WSUS","Проверьте регистрацию клиента");
  if(ReportPresentation35.Tone(report.LastReportedUtc,now,hours)!="Success")return Result("Stale","Отчёт WSUS устарел или время некорректно","Проверьте дату отчёта и доставку отчётности");
  if(report.NeededCount>0)return Result("Needed","К установке: "+(report.RequiredComplete?"":"≥ ")+report.NeededCount,"Одобрены и применимы · установите в согласованное время");
  if(error.Length>0)return Result("Warning","Проверка неполная · данные WSUS не обновлены","Показан сохранённый результат; повторите чтение отчёта");
  if(saved)return Result("Stale","Сохранённый снимок · текущая готовность не проверена","Прочитайте отчёт WSUS и состояние клиента");
  if(report.NeededCount!=0||!report.RequiredComplete||report.RequiredError.Length>0||report.ApprovedUnknownCount>0)return Result("Warning","Проверка неполная · применимость не уточнена","Требуется полный подсчёт одобренных обновлений");
  if(!reboot.Fresh||reboot.Required!=false)return Result("Warning","Проверка неполная · флаги перезагрузки не подтверждены","К установке по отчёту: 0 · проверьте флаги на клиенте\n"+reboot.Details);
  if(DateTimeOffset.TryParse(boot,out var b)&&DateTimeOffset.TryParse(report.LastReportedUtc,out var r)&&r<b)return Result("Warning","Проверка неполная · отчёт получен до загрузки Windows","Нужен отчёт после последней загрузки");
  if(searched&&remaining>0)return Result("Warning","Данные клиента и WSUS расходятся","Последний поиск клиента: осталось "+remaining+" · "+Display36.Date(searchAt,true)+". Выполните новый поиск и отчёт");
  if(!searched||remaining!=0||!DateTimeOffset.TryParse(searchAt,out var at)||now-at>TimeSpan.FromHours(hours)||at>now.AddMinutes(1)||DateTimeOffset.TryParse(boot,out var started)&&at<started)return Result("Warning","Проверка неполная · нет актуального поиска клиента","К установке по отчёту: 0 · выполните проверку обновлений");
  return Result("Success","Проверено · необходимых обновлений нет","Отчёт WSUS свежий · перезагрузка не требуется\nПоиск клиента: "+Display36.Date(searchAt,true)+" · флаги: "+Display36.Date(reboot.Stamp,true));
 }
}
public partial class MainWindow {
 ClientAssessment41 AssessClient41(ComputerRow row){
  var jobs=HostJobs39(row.Host).Where(x=>CurrentIdentity(x.Target)).ToList();var operation=jobs.FirstOrDefault(x=>IsCurrentExecution(x.Target.Status));
  var wsus=jobs.FirstOrDefault(x=>IsWsusJob(x.Job));var target=operation.Target??wsus.Target;var state=target?.State;
  if(state!=null&&state.Status!=target!.Status){state=Store.Clone(state);state.Status=target.Status;}
  reportCache.TryGetValue(row.Host,out var cache);if(cache!=null&&!CacheMatches(cache))cache=null;
  // A newer successful client operation, rather than rereading an old server
  // row, is what can resolve a former operation failure.
  if(state?.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired"&&cache?.Found==true&&DateTimeOffset.TryParse(state.Ended,out var e)&&DateTimeOffset.TryParse(cache.Report.LastReportedUtc,out var reportAt)&&reportAt>e&&cache.Report.FailedCount==0){var newer=jobs.FirstOrDefault(x=>IsWsusJob(x.Job)&&x.Target.Status is "Completed" or "VerifiedAfterReboot"&&DateTimeOffset.TryParse(x.Target.State?.Ended,out var finish)&&finish>e);if(newer.Target!=null)state=newer.Target.State;}
  var scan=jobs.Where(x=>IsWsusJob(x.Job)&&!x.Job.MicrosoftSource&&x.Target.Status is "Completed" or "VerifiedAfterReboot"&&x.Target.State!=null).Select(x=>(State:x.Target.State!,Step:x.Target.State!.Results.LastOrDefault(r=>r.Kind=="UpdateScan"&&r.ExitCode==0))).Where(x=>x.Step!=null).OrderByDescending(x=>x.Step!.Ended).FirstOrDefault();
  var lastInstall=jobs.SelectMany(x=>x.Target.State?.Results??new List<StepResult>()).Where(x=>x.Kind=="Updates").Select(x=>x.Ended).OrderByDescending(x=>x).FirstOrDefault();
  if(scan.Step!=null&&DateTimeOffset.TryParse(lastInstall,out var installed)&&DateTimeOffset.TryParse(scan.Step.Ended,out var scanned)&&scanned<installed)scan=default;
  bool active=operation.Target!=null;var proof=RebootViewFor40(row);
  var assessment=ClientReadiness41.Evaluate(cache?.Found==true?cache.Report:null,cache!=null&&!liveReports.Contains(row.Host),cache?.Found??false,proof,state,active,scan.Step!=null,scan.State?.UpdatesRemaining,scan.Step?.Ended??"",overviewErrors.GetValueOrDefault(row.Host,""),DateTimeOffset.UtcNow,ReportPresentation35.FreshHours,rebootSnapshots.GetValueOrDefault(row.Host)?.BootUtc??"");
  if(!active&&backgroundUpdates.TryGetValue(row.Host,out var live)&&FreshActivity(live)&&(live.InstallerBusy||live.Jobs.Count>0))return new("Working","На клиенте выполняется операция",assessment.Title+"\n"+assessment.Details);
  if(!active&&cache?.Found==false&&assessment.Kind!="Error")assessment=new("NoWsus","Клиент не найден в WSUS","Проверьте регистрацию клиента");
  if(HeaderRebootJob(row)!=null&&assessment.Kind is not ("Error" or "SyncError"))assessment=new("Reboot","Перезагрузка назначена",assessment.Title+"\n"+assessment.Details);
  return row.Target.AdDisabled?new("AdDisabled","Учётная запись компьютера отключена в AD",assessment.Title+"\n"+assessment.Details):assessment;
 }
 string CurrentReportText41(ComputerRow row){if(reportCache.TryGetValue(row.Host,out var c)&&CacheMatches(c)&&c.Found){c.Report.ReceivedUtc41=c.ReceivedUtc;return liveReports.Contains(row.Host)?c.Report.Format(c.Server,c.Started):CachedReportText(c);}return serverReports.GetValueOrDefault(row.Host,"Отчёт выбранного компьютера можно прочитать кнопкой «Отчёт WSUS».");}
 string reportMinute41="";
 void RefreshReportAge41(){if(ComputerGrid.SelectedItem is not ComputerRow row||MainTabs.SelectedItem!=WsusTab)return;string minute=DateTimeOffset.UtcNow.ToString("yyyyMMddHHmm")+"|"+row.Host;if(reportMinute41==minute)return;reportMinute41=minute;WsusServerOutput.Text=CurrentReportText41(row);}
 async void ProbeTools41_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  var row=Current();var json=await remote.ProbeInventory41(row.Target);using var report=System.Text.Json.JsonDocument.Parse(json);int total=report.RootElement.GetProperty("Total").GetInt32();string preview=string.Join("\n",report.RootElement.GetProperty("Rows").EnumerateArray().Select(x=>x.GetProperty("Task").GetString()+" · "+x.GetProperty("State").GetString()+" · "+x.GetProperty("Result").GetString()+" · "+x.GetProperty("Reason").GetString()));
  var window=new Window{Owner=this,Title="Остатки Probe · "+row.Name,Width=760,Height=490,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new DockPanel{Margin=new(12)};window.Content=panel;var buttons=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);
  var clean=new Button{Content="Удалить подтверждённые остатки"};var resume=new Button{Content="Возобновить опрос клиента"};buttons.Children.Add(clean);buttons.Children.Add(resume);buttons.Children.Add(new Button{Content="Закрыть",IsCancel=true});var text=new TextBox{Text="Найдено Probe: "+total+"\nЗакройте старые экземпляры консоли перед очисткой. Maintenance и рабочие задания сохраняются.\n\n"+preview,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(text);
  clean.Click+=async(s,args)=>{if(MessageBox.Show(window,"Удалить только подтверждённые завершённые Probe старше 10 минут? Действующие и неизвестные задачи будут пропущены.","Очистка Probe",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;clean.IsEnabled=false;try{text.Text=await remote.ProbeInventory41(row.Target,true);}catch(Exception ex){text.Text=ex.Message;}finally{clean.IsEnabled=true;}};
  resume.Click+=async(s,args)=>{resume.IsEnabled=false;try{await remote.ResumeProbe41(row.Target);text.Text="Следующая ограниченная проверка разрешена. Старые задачи и процессы не остановлены.";}catch(Exception ex){text.Text=ex.Message;}finally{resume.IsEnabled=true;}};window.ShowDialog();
 });
 void PausePolling41_Click(object sender,RoutedEventArgs e){TransportBudget41.Paused=!TransportBudget41.Paused;Store.Write("poll-paused41.json",TransportBudget41.Paused);RefreshPollButton41();Notify(TransportBudget41.Paused?"Новые служебные подключения приостановлены. Переданные задания продолжают работу.":"Служебные подключения разрешены; блокировки отдельных клиентов сохраняются.");}
 void RefreshPollButton41(){if(PausePolling41!=null)PausePolling41.Content=TransportBudget41.Paused?"Возобновить опрос":"Пауза опроса";}
}
