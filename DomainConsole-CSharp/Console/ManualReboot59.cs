using DomainConsole.Shared;
namespace DomainConsole;
public sealed partial class RemoteService {
 // A separate bounded read-only SYSTEM task; it does not depend on collector admission.
 public static string ManualRebootScript59 => ManualRebootTemplate59.Replace("@@BODY@@",PowerShellBridge.Literal("$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';try{$value=& {\n"+RebootStateScript+"\n}|ConvertFrom-Json -ErrorAction Stop;$value|ConvertTo-Json -Depth 12 -Compress|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'result.tmp') -Encoding UTF8;Move-Item -LiteralPath (Join-Path $PSScriptRoot 'result.tmp') -Destination (Join-Path $PSScriptRoot 'result.json') -Force}catch{[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'error.txt'),$_.Exception.Message);exit 1}"));
 public const string ManualRebootTemplate59="""
$ErrorActionPreference='Stop'
$path=Join-Path $env:ProgramData 'DomainConsoleCSharp\ManualReboot59'
New-Item -Path $path -ItemType Directory -Force|Out-Null
if((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Каталог проверки является ссылкой; запуск отменён'}
$acl=New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
foreach($sid in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier($sid)),'FullControl','ContainerInherit,ObjectInherit','None','Allow')))}
Set-Acl -LiteralPath $path -AclObject $acl
$lock=$null
try{
 try{$lock=[IO.File]::Open((Join-Path $path 'admission.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{throw 'Ручная проверка перезагрузки уже запрошена другой консолью; новый процесс не создан'}
 $name='DomainConsoleCSharp-ManualReboot59'
 $script=Join-Path $path 'collect.ps1';$exe=Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $argument='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$script+'"'
 $task=Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue
 if($task){
  $sid=[string]$task.Principal.UserId
  if($sid -ne 'S-1-5-18'){try{$sid=(New-Object Security.Principal.NTAccount($sid)).Translate([Security.Principal.SecurityIdentifier]).Value}catch{$sid='unknown'}}
  if(@($task.Actions).Count -ne 1 -or $task.Actions[0].Execute -ne $exe -or $task.Actions[0].Arguments -ne $argument -or $sid -ne 'S-1-5-18' -or $task.Settings.MultipleInstances -ne 'IgnoreNew' -or [System.Xml.XmlConvert]::ToTimeSpan($task.Settings.ExecutionTimeLimit).TotalSeconds -ne 40){throw 'Параметры задачи ручной проверки отличаются; задача не изменена'}
  if($task.State -in @('Running','Queued')){throw 'Ручная проверка перезагрузки ещё выполняется; повторный запуск не создан'}
 }
 foreach($file in @(Get-ChildItem -LiteralPath $path -Force)){if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'В каталоге проверки обнаружена ссылка; запуск отменён'}}
 $out=Join-Path $path 'result.json';$err=Join-Path $path 'error.txt'
 Remove-Item -LiteralPath $out,$err,(Join-Path $path 'result.tmp') -Force -ErrorAction SilentlyContinue
 [IO.File]::WriteAllText($script,@@BODY@@,(New-Object Text.UTF8Encoding($true)))
 if(!$task){
  $action=New-ScheduledTaskAction -Execute $exe -Argument $argument
  $principal=New-ScheduledTaskPrincipal -UserId SYSTEM -LogonType ServiceAccount -RunLevel Highest
  $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromSeconds(40)) -MultipleInstances IgnoreNew -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
  Register-ScheduledTask -TaskName $name -TaskPath '\' -Action $action -Principal $principal -Settings $settings -Description 'DomainConsole bounded read-only reboot check' -ErrorAction Stop|Out-Null
 }
 $started=[DateTime]::UtcNow
 Start-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction Stop
 $watch=[Diagnostics.Stopwatch]::StartNew()
 do{
  Start-Sleep -Milliseconds 250
  $task=Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction Stop
  if($task.State -notin @('Running','Queued')){
   if(Test-Path -LiteralPath $err){throw ([IO.File]::ReadAllText($err))}
   if(Test-Path -LiteralPath $out){
    $result=Get-Content -LiteralPath $out -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop
    $at=[DateTime]::MinValue
    if(![DateTime]::TryParse([string]$result.CollectedUtc,[ref]$at) -or $at.ToUniversalTime() -lt $started){throw 'Свежий результат проверки не подтверждён'}
    $result|ConvertTo-Json -Depth 12 -Compress
    return
   }
  }
 }while($watch.Elapsed.TotalSeconds -lt 45)
 throw 'Ручная проверка перезагрузки не вернула результат за 45 секунд. Задача ограничена 40 секундами; состояние системных служб не менялось.'
}finally{if($lock){$lock.Dispose()}}
""";
}
