using System.Windows;
namespace DomainConsole;
public partial class MainWindow {
 void CopyComputer35_Click(object sender,RoutedEventArgs e){if(ComputerGrid.SelectedItem is not ComputerRow row)return;Clipboard.SetText(row.Host);NameCopy35.ToolTip="Скопировано: "+row.Host;Notify("Имя компьютера скопировано");}
 void RefreshClientReport35(ComputerRow row){
  if(!reportCache.TryGetValue(row.Host,out var cached)||!cached.Found||!CacheMatches(cached)){HeaderReport35.Text="Отчёт WSUS: нет данных";HeaderReport35.Foreground=Theme.Brush("TextMuted");HeaderReport35.ToolTip="Отчёт клиента ещё не получен";HeaderContact35.Text="";return;}
  var report=cached.Report;HeaderReport35.Text="Отчёт WSUS: "+ReportPresentation35.Age(report.LastReportedUtc);HeaderReport35.Foreground=Theme.Tone(ReportPresentation35.Tone(report.LastReportedUtc,DateTimeOffset.UtcNow,ReportPresentation35.FreshHours));
  HeaderReport35.ToolTip="Последний отчёт клиента: "+ReportPresentation35.Date(report.LastReportedUtc)+"\nСвежий: до "+ReportPresentation35.FreshHours+" ч; устаревший: до "+ReportPresentation35.FreshHours*2+" ч; старше — требуется проверка отчётности. Порог задаётся в настройках отчётности.";
  HeaderContact35.Text="WSUS: "+(DateTimeOffset.TryParse(report.LastSyncUtc,out var at)&&at.Year>=2000?at.ToLocalTime().ToString("dd.MM HH:mm"):"нет соединений");HeaderContact35.ToolTip="Последнее соединение клиента с WSUS: "+ReportPresentation35.Date(report.LastSyncUtc)+" · "+WsusReport.SyncLabel(report.LastSyncResult)+"\nОтдельный источник; это не проверка связи WinRM.";
 }
}
