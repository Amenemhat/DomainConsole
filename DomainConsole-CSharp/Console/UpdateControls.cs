using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async void CacheCleanup_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{var dialog=new CacheChoice(this);if(dialog.ShowDialog()!=true)return;MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;await Submit(new(){new(){Name="Очистка кэша: "+dialog.Mode+(dialog.Backup?" · резервная копия":""),Kind="CacheCleanup",Code=CacheScripts.Build(dialog.Mode,dialog.Backup,dialog.Catroot)},new(){Name="WSUS · поиск после очистки кэша",Kind="UpdateScan"}},"WSUS");});
 async void WaitingDiagnostics_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;await Submit(new(){new(){Name="Диагностика долгого выполнения",Kind="PowerShell",Code=DownloadDiagnosticScript}},"Commands");});
 public const string DownloadDiagnosticScript="""
$ProgressPreference='SilentlyContinue'
Get-Service bits,wuauserv,UsoSvc|Select-Object Name,Status,StartType|Format-Table -AutoSize
try {Import-Module BitsTransfer -ErrorAction Stop;$jobs=@(Get-BitsTransfer -AllUsers -ErrorAction Stop|Where-Object {$_.DisplayName -eq 'WU Client Download'}|Select-Object -First 8)}catch{Write-Output ('Снимок BITS недоступен: '+$_.Exception.Message);$jobs=@()}
if(!$jobs.Count){'Заданий BITS Windows Update не найдено.'}
$before=@{}
foreach($j in $jobs){$before[[string]$j.JobId]=[decimal]$j.BytesTransferred;'BITS '+$j.JobId+'; состояние: '+$j.JobState+'; приоритет: '+$j.Priority+'; получено: '+$j.BytesTransferred+'; размер: '+$(if($j.BytesTotal -eq [uint64]::MaxValue){'неизвестен'}else{$j.BytesTotal});'Создано: '+$j.CreationTime+'; изменено: '+$j.ModificationTime+'; политика стоимости: '+$j.TransferPolicy;if($j.ErrorDescription){'Ошибка BITS: '+$j.ErrorDescription};$j.FileList|Select-Object -First 1|ForEach-Object {'Источник: '+$_.RemoteName;'Файл: '+$_.LocalName}}
if($jobs.Count){Start-Sleep -Seconds 10;foreach($j in $jobs){$fresh=Get-BitsTransfer -AllUsers -ErrorAction SilentlyContinue|Where-Object JobId -eq $j.JobId;if($fresh){'Повторный снимок '+$j.JobId+'; состояние: '+$fresh.JobState+'; получено за 10 сек: '+([decimal]$fresh.BytesTransferred-$before[[string]$j.JobId])+' байт'}else{'Задание '+$j.JobId+' больше не найдено; завершение проверяется по WUA / отчёту.'}}}
'Отсутствие движения за 10 секунд не доказывает зависание. При длительной остановке загрузки и отсутствии установки рекомендуется перезапуск BITS, UsoSvc и Windows Update без очистки кэша.'
try{'Установщик WUA занят: '+(New-Object -ComObject Microsoft.Update.Installer).IsBusy}catch{'Состояние установщика неизвестно: '+$_.Exception.Message}
Get-CimInstance Win32_Process -Filter "Name='MRT.exe' OR Name='MsMpEng.exe' OR Name='TiWorker.exe'"|Select-Object Name,ProcessId,ParentProcessId,CreationDate|Format-Table -AutoSize
Get-WinEvent -FilterHashtable @{LogName='System';Id=7031,7034;StartTime=(Get-Date).AddHours(-2)} -MaxEvents 10 -ErrorAction SilentlyContinue|Format-List TimeCreated,Message
$p=Join-Path $env:windir 'debug\mrt.log';if(Test-Path $p){Get-Content $p -Tail 30}
""";
 void SaveOperationSettings_Click(object sender,RoutedEventArgs e)=>Guard(()=>{if(!int.TryParse(StallMinutesBox.Text,out var minutes)||minutes<1||minutes>120)throw new Exception("Порог ожидания: 1–120 минут.");Store.Write("stall-minutes.json",minutes);Theme.Apply(ThemeBox.SelectedIndex);RefreshActivity();Notify("Настройки ожидания и палитры сохранены.");});
 static bool Maintenance(List<CommandStep> steps,bool microsoft)=>NeedsUpdateCheck(steps,microsoft);
 async void Recommendation_Click(object sender,RoutedEventArgs e){var r=ActiveRow();string action=r.Target.State?.RecommendationAction??"";if(action=="Memory")MemoryDiagnostics_Click(sender,e);else if(action=="Download")CacheCleanup_Click(sender,e);else if(action=="Defender")Defender_Click(sender,e);else if(action=="Queue")RecoverReport_Click(sender,e);else WaitingDiagnostics_Click(sender,e);}
}
