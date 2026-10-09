using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public static class DefenderScripts {
 public const string Diagnose="""
$ProgressPreference='SilentlyContinue'
'## Диагностика Defender'
Get-CimInstance Win32_OperatingSystem|Select-Object Caption,Version,BuildNumber|Format-List
Get-Service WinDefend,WdNisSvc -ErrorAction SilentlyContinue|Select-Object Name,Status,StartType|Format-Table
try{Get-MpComputerStatus -ErrorAction Stop|Select-Object AMProductVersion,AMEngineVersion,AntivirusSignatureVersion,AntivirusEnabled,RealTimeProtectionEnabled,AntivirusSignatureLastUpdated|Format-List}catch{'Состояние Defender недоступно: '+$_.Exception.Message}
$platform=Join-Path $env:ProgramData 'Microsoft\Windows Defender\Platform'
Get-ChildItem $platform -Directory -ErrorAction SilentlyContinue|Select-Object Name,LastWriteTime|Format-Table
$before=@(Get-Process MsMpEng,MRT -ErrorAction SilentlyContinue|Select-Object Name,Id,CPU,StartTime)
Start-Sleep -Seconds 5
'Два снимка CPU; отсутствие изменения за 5 секунд не доказывает зависание.'
$before|Format-Table
Get-Process MsMpEng,MRT -ErrorAction SilentlyContinue|Select-Object Name,Id,CPU,StartTime|Format-Table
foreach($log in @('System','Application')){Get-WinEvent -FilterHashtable @{LogName=$log;StartTime=(Get-Date).AddHours(-12);Id=7031,7034,1000,1001} -MaxEvents 100 -ErrorAction SilentlyContinue|Where-Object {$_.Message -match 'MsMpEng|Defender|Защитник'}|Select-Object -First 10 TimeCreated,Id,Message|Format-List}
Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-Windows Defender/Operational';StartTime=(Get-Date).AddHours(-12);Level=2,3} -MaxEvents 10 -ErrorAction SilentlyContinue|Select-Object TimeCreated,Id,Message|Format-List
$mrt=Join-Path $env:windir 'debug\mrt.log';if(Test-Path $mrt){'## MRT · последние записи';Get-Item $mrt|Select-Object LastWriteTime,Length;Get-Content $mrt -Tail 25}
'При падениях MsMpEng и ошибках платформы можно рассмотреть откат или сброс платформы после завершения установки. Тайм-аут сам по себе не является основанием сброса. ResetPlatform возвращает встроенную платформу; затем проверьте версии и обновите Defender через WSUS.'
""";
 public static string Recover(bool reset)=>"$mode="+PowerShellBridge.Literal(reset?"-ResetPlatform":"-RevertPlatform")+";"+RecoverBody;
 public const string RecoverBody="""
$ErrorActionPreference='Stop'
if((New-Object -ComObject Microsoft.Update.Installer).IsBusy){throw 'WUA выполняет установку. Восстановление Defender отменено.'}
if(@(Get-CimInstance Win32_Process|Where-Object {$_.Name -eq 'MRT.exe' -or $_.Name -match '^Windows-KB890830.*\.exe$'}).Count){throw 'MRT ещё выполняется. Сначала завершите или остановите его установку.'}
$root=Join-Path $env:ProgramData 'Microsoft\Windows Defender\Platform'
$exe=$null
$folders=@(Get-ChildItem $root -Directory -ErrorAction SilentlyContinue|Where-Object {$_.Name -match '^\d+\.\d+\.\d+\.\d+-\d+$'}|Sort-Object {[Version]($_.Name.Split('-')[0])} -Descending)
foreach($folder in $folders){$candidate=Join-Path $folder.FullName 'MpCmdRun.exe';if(Test-Path $candidate){$exe=$candidate;break}}
if(!$exe){$exe=Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe'}
if(!(Test-Path $exe)){throw 'MpCmdRun.exe не найден. Сброс не выполнялся.'}
$signature=Get-AuthenticodeSignature $exe
if($signature.Status -ne 'Valid' -or !$signature.SignerCertificate -or $signature.SignerCertificate.Subject -notmatch 'Microsoft'){throw 'Подпись MpCmdRun не подтверждена. Операция отменена.'}
'Исполнитель: '+$exe
'Действие: '+$mode
& $exe $mode
$code=$LASTEXITCODE
'Код MpCmdRun: '+$code
if($code -ne 0){$log=Join-Path $env:TEMP 'MpCmdRun.log';if(Test-Path $log){Get-Content $log -Encoding Unicode -Tail 40};throw ('Восстановление не выполнено; код '+$code+'. Дальнейшие действия автоматически не запускаются.')}
Get-Service WinDefend|Select-Object Name,Status,StartType|Format-Table
try{Get-MpComputerStatus -ErrorAction Stop|Select-Object AMProductVersion,AMEngineVersion,AntivirusSignatureVersion,AntivirusEnabled,RealTimeProtectionEnabled|Format-List}catch{'После восстановления статус пока недоступен: '+$_.Exception.Message}
'Команда завершена. Проверьте устойчивость службы и повторите обновление через WSUS. Перезагрузка автоматически не выполняется.'
""";
}
public partial class MainWindow {
 async void Defender_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  var window=new Window{Owner=this,Title="Defender · диагностика и восстановление",Width=510,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};var panel=new StackPanel{Margin=new(18)};panel.Children.Add(new TextBlock{Text="Сначала соберите диагностику. Восстановление запускайте после завершения обновлений.",TextWrapping=TextWrapping.Wrap});var choice=new ComboBox{Margin=new(0,12,0,12),ItemsSource=new[]{"Диагностика (без изменений)","Откат к предыдущей платформе — RevertPlatform","Сброс к встроенной платформе — ResetPlatform"},SelectedIndex=0};panel.Children.Add(choice);panel.Children.Add(new TextBlock{Text="Сброс меняет платформу антивируса. После него проверьте защиту и установите актуальные обновления через WSUS. Компонент Defender не удаляется.",TextWrapping=TextWrapping.Wrap});var button=new Button{Content="Продолжить",Margin=new(0,14,0,0),IsDefault=true};button.Click+=(s,args)=>window.DialogResult=true;panel.Children.Add(button);window.Content=panel;if(window.ShowDialog()!=true)return;
  MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;bool diagnose=choice.SelectedIndex==0;await Submit(new(){new(){Name=diagnose?"Диагностика Defender":choice.SelectedIndex==1?"Defender · откат платформы":"Defender · сброс платформы",Kind=diagnose?"PowerShell":"CacheCleanup",Code=diagnose?DefenderScripts.Diagnose:DefenderScripts.Recover(choice.SelectedIndex==2)}},diagnose?"Commands":"WSUS");
 });
}
