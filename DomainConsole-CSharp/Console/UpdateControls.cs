using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async void CacheCleanup_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{var dialog=new CacheChoice(this);if(dialog.ShowDialog()!=true)return;MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;await Submit(new(){new(){Name="Очистка кэша: "+dialog.Mode+(dialog.Backup?" · резервная копия":""),Kind="CacheCleanup",Code=CacheScripts.Build(dialog.Mode,dialog.Backup,dialog.Catroot)}},"WSUS");});
 async void WaitingDiagnostics_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;await Submit(new(){new(){Name="Диагностика долгого выполнения",Kind="PowerShell",Code="Get-CimInstance Win32_Process -Filter \"Name='MRT.exe' OR Name='MsMpEng.exe' OR Name='TiWorker.exe'\"|Select-Object Name,ProcessId,ParentProcessId,CreationDate|Format-Table;Get-WinEvent -FilterHashtable @{LogName='System';Id=7031,7034;StartTime=(Get-Date).AddHours(-2)} -MaxEvents 10 -ErrorAction SilentlyContinue|Format-List TimeCreated,Message;$p=Join-Path $env:windir 'debug\\mrt.log';if(Test-Path $p){Get-Content $p -Tail 30}"}},"Commands");});
 void SaveOperationSettings_Click(object sender,RoutedEventArgs e)=>Guard(()=>{if(!int.TryParse(StallMinutesBox.Text,out var minutes)||minutes<1||minutes>120)throw new Exception("Порог ожидания: 1–120 минут.");Store.Write("stall-minutes.json",minutes);Theme.Apply(ThemeBox.SelectedIndex);Notify("Настройки ожидания и палитры сохранены.");});
 static bool Maintenance(List<CommandStep> steps,bool microsoft)=>microsoft||steps.Any(s=>s.Kind is "CacheCleanup" or "ReportRecovery"||System.Text.RegularExpressions.Regex.IsMatch(s.Code,@"(?i)\b(?:sfc|dism|Repair-WindowsImage|Stop-Service|Restart-Service|Remove-Item|shutdown)\b|net\s+stop|SoftwareDistribution|catroot2"));
 async void Recommendation_Click(object sender,RoutedEventArgs e){var r=ActiveRow();string action=r.Target.State?.RecommendationAction??"";if(action=="Download")CacheCleanup_Click(sender,e);else if(action=="Queue")RecoverReport_Click(sender,e);else WaitingDiagnostics_Click(sender,e);}
}
