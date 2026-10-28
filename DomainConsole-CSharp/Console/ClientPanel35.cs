using DomainConsole.Shared;
using System.Windows;
namespace DomainConsole;
public partial class MainWindow {
 void HeaderErrors36_Click(object sender,RoutedEventArgs e){MainTabs.SelectedItem=WsusTab;WsusServerOutput.SetExpanded("Сохранённые неудачи WSUS",true);WsusServerOutput.BringIntoView();}
 void CopyComputer35_Click(object sender,RoutedEventArgs e){if(ComputerGrid.SelectedItem is not ComputerRow row)return;Clipboard.SetText(Identity40.ShortName(row.Host));NameCopy35.ToolTip="Скопировано: "+Identity40.ShortName(row.Host);Notify("Имя компьютера скопировано");}
 void RefreshClientReport35(ComputerRow row){
  if(!reportCache.TryGetValue(row.Host,out var cached)||!cached.Found||!CacheMatches(cached)){HeaderReport35.Text="Отчёт WSUS: нет данных";HeaderReport35.Foreground=Theme.Brush("TextMuted");HeaderReport35.ToolTip="Отчёт клиента ещё не получен";HeaderContact35.Text="";HeaderErrors36.Visibility=Visibility.Collapsed;return;}
  var report=cached.Report;HeaderErrors36.Visibility=report.FailedCount>0?Visibility.Visible:Visibility.Collapsed;HeaderErrors36.Content="Ошибок WSUS: "+report.FailedCount;HeaderErrors36.ToolTip="Сохранённые ошибки по отчёту WSUS от "+Display36.Date(report.LastReportedUtc,true)+"\n"+HeaderFailureHint36(report.FailedUpdates)+"\nНажмите, чтобы открыть отчёт WSUS";HeaderReport35.Text="Отчёт WSUS: "+ReportPresentation35.Age(report.LastReportedUtc);HeaderReport35.Foreground=Theme.Tone(ReportPresentation35.Tone(report.LastReportedUtc,DateTimeOffset.UtcNow,ReportPresentation35.FreshHours));
  _=AssessClient41(row);var reportCriterion47=criteria45.GetValueOrDefault(row.Host)?.FirstOrDefault(c=>c.Key=="report");if(reportCriterion47!=null){HeaderReport35.Foreground=Theme.Tone(reportCriterion47.Tone);HeaderErrors36.Foreground=Theme.Tone(reportCriterion47.Tone);}
  HeaderReport35.ToolTip="Последний отчёт клиента: "+ReportPresentation35.Date(report.LastReportedUtc)+"\nЦвет соответствует критерию «Отчёт WSUS»: учитывает результат обмена, изменения клиента и подтверждённую готовность. Порог первичной проверки: "+ReportPresentation35.FreshHours+" ч.";
  HeaderContact35.Text="WSUS: "+(DateTimeOffset.TryParse(report.LastSyncUtc,out var at)&&at.Year>=2000?at.ToLocalTime().ToString("dd.MM HH:mm"):"время неизвестно");HeaderContact35.ToolTip="Последнее соединение клиента с WSUS: "+ReportPresentation35.Date(report.LastSyncUtc)+" · "+WsusReport.SyncLabel(report.LastSyncResult)+"\nОтдельный источник; это не проверка связи WinRM.";
 }
 static string HeaderFailureHint36(System.Collections.Generic.IEnumerable<WsusFailedUpdate> updates){
  var failure=updates.SelectMany(u=>u.Events).Where(e=>e.IsError&&e.ErrorCode.HasValue&&e.ErrorCode!=0).OrderByDescending(e=>e.Date).FirstOrDefault();
  if(failure==null)return "Причина сохранённой неудачи пока не получена";
  int code=unchecked((int)failure.ErrorCode!.Value);
  return "Сохранённое событие: "+Display36.Date(failure.Date,true)+"\n"+ExecutionDiagnosis.Code(code)+" · "+ExecutionDiagnosis.Explain(code);
 }

}
