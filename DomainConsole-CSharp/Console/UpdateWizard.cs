using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 public const string UpdateWizardScript="""
$dcUpdateWizard=$true
$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue'
'## Диагностика Windows Update'
'Снимок: '+(Get-Date).ToString('dd.MM.yyyy HH:mm:ss')
$services=Get-Service wuauserv,bits,UsoSvc,cryptsvc
$services|Select-Object Name,Status,StartType|Format-Table|Out-String
'## Политики и источник'
$p=Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate' -ErrorAction SilentlyContinue
$au=Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -ErrorAction SilentlyContinue
'WUServer: '+$p.WUServer+'; WUStatusServer: '+$p.WUStatusServer+'; UseWUServer: '+$au.UseWUServer
if($p.WUServer){try{$uri=[uri]$p.WUServer;$tcp=New-Object Net.Sockets.TcpClient;try{$task=$tcp.ConnectAsync($uri.Host,$uri.Port);if(!$task.Wait(3000)){throw 'Тайм-аут TCP'};if(!$tcp.Connected){throw 'TCP не подключён'};'TCP источника доступен; HTTP и получение отчёта этим не подтверждаются.'}finally{$tcp.Dispose()}}catch{'Предупреждение подключения: '+$_.Exception.Message}}
'## Загрузки и установщик'
try{Import-Module BitsTransfer;Get-BitsTransfer -AllUsers|Where-Object {$_.DisplayName -eq 'WU Client Download'}|Select-Object JobId,JobState,BytesTransferred,BytesTotal,ErrorDescription|Format-List|Out-String}catch{'Данные BITS недоступны: '+$_.Exception.Message}
try{'Установщик WUA занят: '+(New-Object -ComObject Microsoft.Update.Installer).IsBusy}catch{'Состояние WUA неизвестно: '+$_.Exception.Message}
'## Очередь отчётности'
foreach($name in @('EventCache','EventCache.v2')){try{$path=Join-Path "$env:windir\SoftwareDistribution" $name;$files=@();if(Test-Path $path){$files=@(Get-ChildItem $path -File -Recurse -ErrorAction Stop)};$name+': '+$files.Count+' файлов; '+[long](($files|Measure-Object Length -Sum).Sum)+' байт';if($files.Count){'Старейшее событие: '+($files|Sort-Object CreationTime|Select-Object -First 1).CreationTime}}catch{'Очередь не прочитана: '+$_.Exception.Message}}
'## Место на диске и события'
Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"|Select-Object DeviceID,FreeSpace,Size|Format-List|Out-String
Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-WindowsUpdateClient/Operational';StartTime=(Get-Date).AddHours(-2);Level=2,3} -MaxEvents 12 -ErrorAction SilentlyContinue|Select-Object TimeCreated,Id,Message|Format-List|Out-String
'## Рекомендации'
if(@($services|Where-Object {$_.StartType -eq 'Disabled'}).Count){'Рекомендуется проверить отключённые службы и доменные политики. Перезапуск не исправляет запрет политикой.'}
'Если загрузка движется медленно: сравните два снимка BITS и проверьте ограничения скорости. Долгая загрузка сама по себе не означает зависание.'
'При старом отчёте WSUS: прочитайте отчёт сервера и используйте диагностику отчётности. Наличие файлов очереди без её возраста и возраста отчёта не доказывает неисправность.'
'При ошибках целостности: сопоставьте код и пакет; очистка Download только по вашему выбору. Нечитаемые файлы требуют проверки диска.'
'Изменения не выполнялись. Выберите подходящее восстановление в помощнике; результат нужно проверить повторной диагностикой.'
""";
 async void UpdateWizard_Click(object sender,RoutedEventArgs e){var panel=new StackPanel{Margin=new(18)};var w=new Window{Owner=this,Title="Помощник Windows Update",Width=520,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};panel.Children.Add(new TextBlock{Text="Начните с диагностики. Результат появится в журнале WSUS. Затем выберите действие по обнаруженной причине; изменения автоматически не выполняются.",TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,12)});string action="";foreach(var pair in new[]{("Диагностика Windows Update","diagnose"),("Перезапустить выбранные службы…","services"),("Очистка кэша с вариантами…","cache"),("Диагностика доставки отчёта","report"),("Диагностика и восстановление Defender…","defender")}){var b=new Button{Content=pair.Item1,Padding=new(8),Margin=new(0,3,0,3)};b.Click+=(s,a)=>{action=pair.Item2;w.DialogResult=true;};panel.Children.Add(b);}if(w.ShowDialog()!=true)return;if(action=="services")RestartServices_Click(sender,e);else if(action=="cache")CacheCleanup_Click(sender,e);else if(action=="report")DeliveryDiagnostics_Click(sender,e);else if(action=="defender")Defender_Click(sender,e);else await Guard(async()=>{MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;await Submit(new(){new(){Name="Диагностика Windows Update · помощник",Kind="PowerShell",Code=UpdateWizardScript}},"WSUS");});}
}
