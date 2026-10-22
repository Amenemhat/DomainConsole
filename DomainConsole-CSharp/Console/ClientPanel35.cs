using DomainConsole.Shared;
using System.Windows;
namespace DomainConsole;
public partial class MainWindow {
 void HeaderErrors36_Click(object sender,RoutedEventArgs e){MainTabs.SelectedItem=WsusTab;WsusServerOutput.SetExpanded("Сохранённые неудачи WSUS",true);WsusServerOutput.BringIntoView();}
 void CopyComputer35_Click(object sender,RoutedEventArgs e){if(ComputerGrid.SelectedItem is not ComputerRow row)return;Clipboard.SetText(row.Host);NameCopy35.ToolTip="Скопировано: "+row.Host;Notify("Имя компьютера скопировано");}
 void RefreshClientReport35(ComputerRow row){
  if(!reportCache.TryGetValue(row.Host,out var cached)||!cached.Found||!CacheMatches(cached)){HeaderReport35.Text="Отчёт WSUS: нет данных";HeaderReport35.Foreground=Theme.Brush("TextMuted");HeaderReport35.ToolTip="Отчёт клиента ещё не получен";HeaderContact35.Text="";HeaderErrors36.Visibility=Visibility.Collapsed;return;}
  var report=cached.Report;HeaderErrors36.Visibility=report.FailedCount>0?Visibility.Visible:Visibility.Collapsed;HeaderErrors36.Content="Ошибок WSUS: "+report.FailedCount;HeaderErrors36.ToolTip="Сохранённые ошибки по отчёту WSUS от "+Display36.Date(report.LastReportedUtc,true)+"\n"+HeaderFailureHint36(report.FailedUpdates)+"\nНажмите, чтобы открыть отчёт WSUS";HeaderReport35.Text="Отчёт WSUS: "+ReportPresentation35.Age(report.LastReportedUtc);HeaderReport35.Foreground=Theme.Tone(ReportPresentation35.Tone(report.LastReportedUtc,DateTimeOffset.UtcNow,ReportPresentation35.FreshHours));
  HeaderReport35.ToolTip="Последний отчёт клиента: "+ReportPresentation35.Date(report.LastReportedUtc)+"\nСвежий: до "+ReportPresentation35.FreshHours+" ч; старше — устаревший отчёт (серые часы). Порог задаётся в настройках отчётности.";
  HeaderContact35.Text="WSUS: "+(DateTimeOffset.TryParse(report.LastSyncUtc,out var at)&&at.Year>=2000?at.ToLocalTime().ToString("dd.MM HH:mm"):"время неизвестно");HeaderContact35.ToolTip="Последнее соединение клиента с WSUS: "+ReportPresentation35.Date(report.LastSyncUtc)+" · "+WsusReport.SyncLabel(report.LastSyncResult)+"\nОтдельный источник; это не проверка связи WinRM.";
 }
 static string HeaderFailureHint36(System.Collections.Generic.IEnumerable<WsusFailedUpdate> updates){
  var failure=updates.SelectMany(u=>u.Events).Where(e=>e.IsError&&e.ErrorCode.HasValue&&e.ErrorCode!=0).OrderByDescending(e=>e.Date).FirstOrDefault();
  if(failure==null)return "Причина сохранённой неудачи пока не получена";
  int code=unchecked((int)failure.ErrorCode!.Value);
  return "Сохранённое событие: "+Display36.Date(failure.Date,true)+"\n"+ExecutionDiagnosis.Code(code)+" · "+ExecutionDiagnosis.Explain(code);
 }

}
