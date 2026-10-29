using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed record ClientAssessment41(string Kind,string Title,string Details){
 public bool InstallationSucceeded51 {get;init;}
 public string HeadlineTone51=>InstallationSucceeded51?"Success":Tone;
 public string Tone=>Kind switch{"Success"=>"Success","Error" or "SyncError"=>"Error","Needed" or "Working"=>"Info","Stale"=>"Stale","Unknown"=>"Unknown",_=>"Warning"};
 public string Glyph=>Kind switch{"Success"=>"Success","Error" or "SyncError"=>"Error","Reboot"=>"Reboot","Working"=>"Working","Needed"=>"Info","Stale"=>"Stale","Unknown"=>"Unknown",_=>"Warning"};
}
public static class ClientReadiness41 {
 public static string IPv4(string value){if(!IPAddress.TryParse(value,out var ip))return "";if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();return ip.AddressFamily==AddressFamily.InterNetwork?ip.ToString():"";}
 public static ClientAssessment41 Evaluate(WsusReport? report,bool saved,bool found,RebootView40 reboot,RemoteState? state,bool active,bool searched,int? remaining,string searchAt,string error,DateTimeOffset now,int hours,string boot=""){
  string dates=report==null?"":"Отчёт WSUS: "+Display36.Date(report.LastReportedUtc)+" · "+ReportPresentation35.AgeAt41(report.LastReportedUtc,now);
  ClientAssessment41 Result(string k,string title,string detail)=>new(k,title,detail+(dates.Length>0?"\n"+dates:""));
  if(active){string concern=report?.FailedCount>0?"Ошибок по отчёту WSUS: "+report.FailedCount:report!=null&&ReportPresentation35.Tone(report.LastReportedUtc,now,hours)!="Success"?"Отчёт WSUS устарел":"";if(reboot.Required==true)concern+=(concern.Length>0?"\n":"")+"Требуется перезагрузка";return Result("Working",state?.Stage.Length>0?state.Stage:"Выполняется задание",concern);}
  if(reboot.Required==true){
   bool installed45=(state?.Status is "Completed" or "AwaitingReboot" or "RebootScheduled" or "VerifiedAfterReboot")&&state.Results.Any(x=>x.Kind=="Updates"&&x.ExitCode==0&&DateTimeOffset.TryParse(x.Ended,out _))==true&&state.InstalledCount38>0;
   return Result("Reboot",installed45?"Установка успешна":reboot.Title,reboot.Details+(report?.FailedCount>0?"\nОшибок по сохранённому отчёту WSUS: "+report.FailedCount:"")+(state?.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired"?"\nЕсть ошибка задания; подробности в критерии обновлений":"")) with {InstallationSucceeded51=installed45};
  }
  if(report!=null&&Criteria45.Resolved45(report,state)&&state?.Status is "Completed" or "VerifiedAfterReboot")return Result("Warning","Ожидается отчёт WSUS после успешной установки","Прежняя ошибка сопоставлена с успешной установкой того же обновления; серверное подтверждение ещё не получено");
  if(state?.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired" or "SubmissionUnknown")return Result("Error","Ошибка · "+ResultPresentation36.BriefFailure(state.Error.Length>0?state.Error:ComputerRow.StatusText(state.Status)),"Проверьте причину в журнале задания"+(reboot.Required==true?"\nТребуется перезагрузка · "+reboot.Details:""));
  if(report?.FailedCount>0)return Result("Error","Ошибок по отчёту WSUS: "+report.FailedCount,"Откройте причины в отчёте WSUS"+(reboot.Required==true?"\nТребуется перезагрузка · "+reboot.Details:""));
  if(report?.LastSyncResult is "Failed" or "FailedWithErrors")return Result("SyncError","Последний поиск WSUS завершился с ошибкой","Поиск: "+Display36.Date(report.LastSyncUtc)+" · выполните диагностику связи с WSUS");
  if(reboot.Required==true)return Result("Reboot",reboot.Title,reboot.Details+(report==null?"\nОтчёт WSUS не получен":ReportPresentation35.Tone(report.LastReportedUtc,now,hours)!="Success"?"\nОтчёт WSUS устарел":""));
  if(report==null)return Result("Unknown","Отчёт WSUS не получен","Прочитайте данные сервера WSUS");
  if(!found)return Result("NoWsus","Клиент не найден в WSUS","Проверьте регистрацию клиента");
  if(ReportPresentation35.Tone(report.LastReportedUtc,now,hours)!="Success")return Result("Stale","Отчёт WSUS устарел или время некорректно","Проверьте дату отчёта и доставку отчётности");
  if(report.NeededCount>0)return Result("Needed","К установке: "+(report.RequiredComplete?"":"≥ ")+report.NeededCount,"Одобрены и применимы · установите в согласованное время");
  if(report.LastSyncResult!="Succeeded")return Result("Warning","Проверка неполная · результат поиска WSUS неизвестен","Последнее соединение: "+Display36.Date(report.LastSyncUtc));
  if(error.Length>0)return Result("Warning","Проверка неполная · данные WSUS не обновлены","Показан сохранённый результат; повторите чтение отчёта");
  if(saved)return Result("Stale","Сохранённый снимок · текущая готовность не проверена","Прочитайте отчёт WSUS и состояние клиента");
  if(report.NeededCount!=0||!report.RequiredComplete||report.RequiredError.Length>0||report.ApprovedUnknownCount>0)return Result("Warning","Проверка неполная · применимость не уточнена","Требуется полный подсчёт одобренных обновлений");
  if(!reboot.Fresh||reboot.Required!=false)return Result("Warning","Проверка неполная · флаги перезагрузки не подтверждены","К установке по отчёту: 0 · проверьте флаги на клиенте\n"+reboot.Details);
  if(DateTimeOffset.TryParse(boot,out var b)&&DateTimeOffset.TryParse(report.LastReportedUtc,out var r)&&r<b)return Result("Warning","Проверка неполная · отчёт получен до загрузки Windows","Нужен отчёт после последней загрузки");
  if(searched&&remaining>0)return Result("Warning","Данные клиента и WSUS расходятся","Последний поиск клиента: осталось "+remaining+" · "+Display36.Date(searchAt)+". Выполните новый поиск и отчёт");
  if(!searched)return Result("Warning","Поиск клиента не подтверждён","К установке по отчёту: 0 · нет сохранённого успешного поиска для этого источника");
  if(!DateTimeOffset.TryParse(searchAt,out var at)||at>now.AddMinutes(1))return Result("Warning","Время поиска клиента не подтверждено","К установке по отчёту: 0 · проверьте время и результат поиска");
  if(DateTimeOffset.TryParse(boot,out var started)&&at<started)return Result("Warning","Поиск клиента выполнен до перезагрузки","К установке по отчёту: 0 · нужна проверка после последней загрузки");
  if(remaining!=0||now-at>TimeSpan.FromHours(hours))return Result("Warning","Нужна повторная проверка обновлений","Последний подтверждённый поиск: "+Display36.Date(searchAt));
  return Result("Success","Проверено · необходимых обновлений нет","Отчёт WSUS свежий · перезагрузка не требуется\nПоиск клиента: "+Display36.Date(searchAt)+" · флаги: "+Display36.Date(reboot.Stamp));
 }
}
public partial class MainWindow {
 Dictionary<string,ClientAssessment41>? refreshClientStatus41;
 ClientAssessment41 AssessClient41(ComputerRow row){if(refreshClientStatus41!=null&&refreshClientStatus41.TryGetValue(row.Host,out var cached))return cached;var result=AssessClientCore41(row);if(refreshClientStatus41!=null)refreshClientStatus41[row.Host]=result;return result;}
 ClientAssessment41 AssessClientCore41(ComputerRow row){
  var jobs=HostJobs39(row.Host).Where(x=>CurrentIdentity(x.Target)).ToList();var operation=jobs.FirstOrDefault(x=>IsCurrentExecution(x.Target.Status)||x.Target.Status=="Queued");
  var wsus=jobs.FirstOrDefault(x=>IsWsusJob(x.Job));var target=operation.Target??wsus.Target;var state=target?.State;
  if(state!=null&&state.Status!=target!.Status){state=Store.Clone(state);state.Status=target.Status;}
  reportCache.TryGetValue(row.Host,out var cache);if(cache!=null&&!CacheMatches(cache))cache=null;
  // A newer successful client operation, rather than rereading an old server
  // row, is what can resolve a former operation failure.
  if(state?.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired"&&cache?.Found==true&&DateTimeOffset.TryParse(state.Ended,out var e)&&DateTimeOffset.TryParse(cache.Report.LastReportedUtc,out var reportAt)&&reportAt>e&&cache.Report.FailedCount==0){var newer=jobs.FirstOrDefault(x=>IsWsusJob(x.Job)&&x.Target.Status is "Completed" or "VerifiedAfterReboot"&&DateTimeOffset.TryParse(x.Target.State?.Ended,out var finish)&&finish>e);if(newer.Target!=null)state=newer.Target.State;}
  var scan=jobs.Where(x=>IsWsusJob(x.Job)&&!x.Job.MicrosoftSource&&x.Target.Status is "Completed" or "VerifiedAfterReboot" or "AwaitingReboot"&&x.Target.State!=null).Select(x=>(State:x.Target.State!,Step:x.Target.State!.Results.LastOrDefault(r=>(r.Kind=="UpdateScan"||r.Kind=="Updates")&&r.ExitCode==0))).Where(x=>x.Step!=null).OrderByDescending(x=>x.Step!.Ended).FirstOrDefault();
  var lastInstall=jobs.SelectMany(x=>x.Target.State?.Results??new List<StepResult>()).Where(x=>x.Kind=="Updates").Select(x=>x.Ended).OrderByDescending(x=>x).FirstOrDefault();
  if(scan.Step!=null&&DateTimeOffset.TryParse(lastInstall,out var installed)&&DateTimeOffset.TryParse(scan.Step.Ended,out var scanned)&&scanned<installed)scan=default;
  bool active=operation.Target!=null;var proof=RebootViewFor40(row);
  var assessment=ClientReadiness41.Evaluate(cache?.Found==true?cache.Report:null,cache!=null&&!liveReports.Contains(row.Host),cache?.Found??false,proof,state,active,scan.Step!=null,scan.State?.UpdatesRemaining,scan.Step?.Ended??"",overviewErrors.GetValueOrDefault(row.Host,""),DateTimeOffset.UtcNow,ReportPresentation35.FreshHours,rebootSnapshots.GetValueOrDefault(row.Host)?.BootUtc??"");
  if(!active&&backgroundUpdates.TryGetValue(row.Host,out var live)&&FreshActivity(live)&&(live.InstallerBusy||live.Jobs.Count>0)){assessment=new("Working","На клиенте выполняется операция",assessment.Title+"\n"+assessment.Details);active=true;}
  if(!active&&cache?.Found==false&&assessment.Kind is not ("Error" or "SyncError" or "Reboot"))assessment=new("NoWsus","Клиент не найден в WSUS","Проверьте регистрацию клиента");
  if(HeaderRebootJob(row)!=null&&assessment.Kind is not ("Error" or "SyncError"))assessment=new("Reboot","Перезагрузка назначена",assessment.Title+"\n"+assessment.Details);
  assessment=PreserveReady43(row,assessment,active,state,cache,proof,scan.Step?.Ended??"");if(assessment.Kind=="Success"&&rebootWatches.TryGetValue(row.Host,out var watch43)&&!watch43.Done&&rebootSnapshots.TryGetValue(row.Host,out var snap43)&&RebootReturned(watch43,snap43)&&ObservedAfter(snap43,watch43.RequestedUtc)&&snap43.Required==false){watch43.Done=true;watch43.Phase="Перезагрузка и готовность подтверждены";Store.Write("reboot-watches.json",rebootWatches);}
  assessment=ExplainAssessment45(row,assessment,cache,proof,state,active,scan.Step?.Ended??"",scan.State?.UpdatesRemaining);
  if(!active&&rebootWatches.TryGetValue(row.Host,out var observing51)&&!observing51.Done)assessment=assessment with{Title=RebootHeadline51(observing51,DateTimeOffset.UtcNow)};
  var observed51=operation.Target!=null?new ComputerRow(operation.Target,operation.Job.Id):new ComputerRow(target??row.Target,wsus.Job?.Id??"");
  if(LostObservation51(row.Host)||(stoppedObservations51.Count>0&&ObservationStopped51(observed51))){assessment=new("Unknown",IsCurrentExecution(observed51.Target.Status)?ObservationExpired51(observed51)||ObservationStopped51(observed51)?"Ожидание завершено · результат операции неизвестен":"Связь потеряна · результат операции неизвестен":"Связь потеряна · текущее состояние неизвестно",assessment.Title+"\nПоследнее состояние: "+Display36.Date(observed51.Target.State?.Updated??""));}
  return row.Target.AdDisabled?new("AdDisabled","Учётная запись компьютера отключена в AD",assessment.Title+"\n"+assessment.Details):assessment;
 }
 string CurrentReportText41(ComputerRow row){if(reportCache.TryGetValue(row.Host,out var c)&&CacheMatches(c)&&c.Found){c.Report.ReceivedUtc41=c.ReceivedUtc;return liveReports.Contains(row.Host)?c.Report.Format(c.Server,c.Started):CachedReportText(c);}return serverReports.GetValueOrDefault(row.Host,"Отчёт выбранного компьютера можно прочитать кнопкой «Отчёт WSUS».");}
 string reportMinute41="";
 void RefreshReportAge41(){if(ComputerGrid.SelectedItem is not ComputerRow row||MainTabs.SelectedItem!=WsusTab)return;var stamp=reportCache.GetValueOrDefault(row.Host)?.Report.LastReportedUtc??"";string minute=DateTime.Now.ToString("yyyyMMdd")+"|"+row.Host+"|"+stamp+"|"+ReportPresentation35.Age(stamp)+"|"+ReportPresentation35.Tone(stamp,DateTimeOffset.UtcNow,ReportPresentation35.FreshHours);if(reportMinute41==minute)return;reportMinute41=minute;WsusServerOutput.Text=CurrentReportText41(row);}
 async void ProbeTools41_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  var row=Current();var window=new Window{Owner=this,Title="Сборщик состояния · "+row.Name,Width=800,Height=540,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new DockPanel{Margin=new(12)};window.Content=panel;var buttons=new WrapPanel();DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);
  var inspect=new Button{Content="Проверить состояние"};var stop=new Button{Content="Остановить опрос"};var resume=new Button{Content="Проверить и восстановить"};var preview=new Button{Content="Остатки Probe"};var clean=new Button{Content="Очистить остатки",IsEnabled=false};foreach(var button in new[]{inspect,stop,resume,preview,clean})buttons.Children.Add(button);var manage43=new Button{Content="Удаление / включение…"};manage43.Click+=(s,args)=>CollectorCleanup43_Click(s!,args);buttons.Children.Add(manage43);buttons.Children.Add(new Button{Content="Закрыть",IsCancel=true});var text=new LogViewer{Text="Прочитайте состояние сборщика. Рабочие задания обновлений не останавливаются.",ShellMode="Report"};text.CopyRequested+=(s,args)=>Clipboard.SetText(text.VisibleText());panel.Children.Add(text);
  bool busy=false;async Task Run(Func<Task<string>> action){if(busy)return;busy=true;foreach(var button in new[]{inspect,stop,resume,preview,clean})button.IsEnabled=false;try{text.Text=await action();}catch(Exception ex){text.Text="Не удалось прочитать состояние опроса\n"+ex.Message;}finally{busy=false;foreach(var button in new[]{inspect,stop,resume,preview,clean})button.IsEnabled=true;}}
  inspect.Click+=async(s,args)=>await Run(()=>remote.InspectSnapshot42(row.Target));
  stop.Click+=async(s,args)=>await Run(()=>remote.InspectSnapshot42(row.Target,"Stop"));
  resume.Click+=async(s,args)=>await Run(async()=>{
   var target=ConnectionStore.Capture(row.Target);var generation=PollingControl43.Token;
   string result="Ответ восстановления устарел: профиль или режим опроса изменился. Повторный запуск не создавался.";
   await CompleteCollectorRepair47(target,generation,remote.RepairCollector47(target),()=>{
    ReadSucceeded34(row.Host,"флаги и аптайм");ReadSucceeded34(row.Host,"фоновая активность");return Task.CompletedTask;
   },value=>result=value);return result;
  });
  preview.Click+=async(s,args)=>await Run(async()=>ProbeInventoryText41(await remote.ProbeInventory41(row.Target)));
  clean.Click+=async(s,args)=>{if(MessageBox.Show(window,"Удалить подтверждённые завершённые старые Probe? Рабочие задания сохраняются.","Очистка Probe",MessageBoxButton.YesNo)==MessageBoxResult.Yes)await Run(async()=>ProbeInventoryText41(await remote.ProbeInventory41(row.Target,true)));};window.ShowDialog();
 });

 static string ProbeInventoryText41(string json){using var doc=System.Text.Json.JsonDocument.Parse(json);var root=doc.RootElement;var rows=root.GetProperty("Rows").EnumerateArray().ToList();int removed=rows.Count(x=>x.GetProperty("Result").GetString()=="Удалено"),eligible=rows.Count(x=>x.GetProperty("Result").GetString()=="Можно удалить"),skipped=rows.Count-removed-eligible;return "## Главное\nОстатки Probe: "+root.GetProperty("Total").GetInt32()+" · проверено: "+root.GetProperty("Inspected").GetInt32()+"\nУдалено: "+removed+" · можно удалить: "+eligible+" · пропущено: "+skipped+"\nЗакройте старые экземпляры консоли перед очисткой. Действующие и неизвестные задачи сохраняются.\n\n## Результаты проверки\n"+string.Join("\n",rows.Select(x=>x.GetProperty("Task").GetString()+" · "+x.GetProperty("State").GetString()+" · "+x.GetProperty("Result").GetString()+"\n"+x.GetProperty("Reason").GetString()+" · "+Display36.Date(x.GetProperty("RegisteredUtc").GetString()??"",true)))+"\n\n## Область очистки\n"+root.GetProperty("Note").GetString();}
 void PausePolling41_Click(object sender,RoutedEventArgs e){if(TransportBudget41.Paused)PollingControl43.Resume();else{PollingControl43.Pause();overviewCancel?.Cancel();foreach(var r43 in Computers)r43.SetService43("");}Store.Write("poll-paused41.json",TransportBudget41.Paused);RefreshPollButton41();Notify(TransportBudget41.Paused?"Новые служебные подключения приостановлены. Переданные задания продолжают работу.":"Служебные подключения разрешены; блокировки отдельных клиентов сохраняются.");}
 void RefreshPollButton41(){if(PausePolling41!=null)PausePolling41.IsChecked=!TransportBudget41.Paused;}
}

