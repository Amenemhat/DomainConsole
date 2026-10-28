$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue'
$mode='@@MODE@@'
$root=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$path=Join-Path $root 'Snapshot41';$disabled=Join-Path $root 'collector-disabled43.json'
function PlainPath([string]$p){if(Test-Path -LiteralPath $p){if((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw ('Ссылка вместо каталога/файла: '+$p)}}}
PlainPath $root;PlainPath $path
New-Item -ItemType Directory -Path $root -Force|Out-Null
$acl=New-Object Security.AccessControl.DirectorySecurity;$acl.SetAccessRuleProtection($true,$false)
foreach($id in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier($id)),'FullControl','ContainerInherit,ObjectInherit','None','Allow')))}
Set-Acl -LiteralPath $root -AclObject $acl

PlainPath $disabled
if($mode -ne 'Enable'){[IO.File]::WriteAllText($disabled,'{"Disabled":true,"Protocol":1}',(New-Object Text.UTF8Encoding($false)))}
$lock=$null;$gate=$null;$removed=@();$skipped=@()
try{
 if(!(Test-Path -LiteralPath $path)){New-Item -ItemType Directory -Path $path -Force|Out-Null}
 PlainPath (Join-Path $path 'admission.lock')
 try{$lock=[IO.File]::Open((Join-Path $path 'admission.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{throw 'Допуск сборщика занят; повторите очистку после завершения текущего подключения. Запрет новых запусков сохранён'}
 if($mode -eq 'Enable'){if(Test-Path -LiteralPath $disabled){Remove-Item -LiteralPath $disabled -Force -ErrorAction Stop};if(Test-Path -LiteralPath $disabled){throw 'Запрет установки не снят'};@{Status='Enabled';Reason='Запрет установки снят; сборщик будет проверен и установлен при следующем разрешённом опросе'}|ConvertTo-Json -Compress;return}
 $name='DomainConsoleCSharp-Snapshot41';$task=Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue
 $exe=Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe';$script=Join-Path $path 'collect.ps1'
 if(!$task -and (Test-Path -LiteralPath (Join-Path $path 'collect.ps1'))){$body=[IO.File]::ReadAllText((Join-Path $path 'collect.ps1'));if($body -notlike '*DcReadJob42*' -and $body -notlike '*$dcRebootJobId*'){throw 'Принадлежность файлов без задачи не подтверждена; сохранены'}}
 $dcArguments43='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$script+'"'
 if($task){
  $sid=[string]$task.Principal.UserId;try{if($sid -ne 'S-1-5-18'){$sid=(New-Object Security.Principal.NTAccount($sid)).Translate([Security.Principal.SecurityIdentifier]).Value}}catch{$sid='unknown'}
  $limit=[System.Xml.XmlConvert]::ToTimeSpan($task.Settings.ExecutionTimeLimit).TotalSeconds
  if(@($task.Actions).Count -ne 1 -or $task.Actions[0].Execute -ne $exe -or $task.Actions[0].Arguments -ne $dcArguments43 -or $sid -ne 'S-1-5-18' -or $limit -notin @(40,60) -or $task.Settings.MultipleInstances -ne 'IgnoreNew'){throw 'Принадлежность существующей задачи сборщику не подтверждена; задача и файлы сохранены, автоматический запуск запрещён'}
  PlainPath (Join-Path $path 'cancel.flag');[IO.File]::WriteAllText((Join-Path $path 'cancel.flag'),'cancel')
  $clock=[Diagnostics.Stopwatch]::StartNew()
  do{$task=Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction Stop;if($task.State -notin @('Running','Queued')){break};Start-Sleep -Milliseconds 200}while($clock.Elapsed.TotalSeconds -lt 5)
  if($task.State -in @('Running','Queued')){Stop-ScheduledTask -InputObject $task -ErrorAction Stop;Start-Sleep -Milliseconds 500}
 }
 # Terminate only exact PowerShell file invocations captured after ownership proof.
 $owned=@()
 foreach($p in @(Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction Stop)){
  foreach($file in @('collect.ps1','Reboot.ps1','Activity.ps1')){
   $expected='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+(Join-Path $path $file)+'"'
   $command=[string]$p.CommandLine
   if($task -and $p.ExecutablePath -eq $exe -and $command -match ('^(?:"'+[regex]::Escape($exe)+'"|'+[regex]::Escape($exe)+')\s+'+[regex]::Escape($expected)+'$')){$owned+=$p}
  }
 }
 foreach($p in $owned){$again=Get-CimInstance Win32_Process -Filter ('ProcessId='+$p.ProcessId) -ErrorAction SilentlyContinue;if($again -and $again.CreationDate -eq $p.CreationDate -and $again.CommandLine -eq $p.CommandLine -and $again.ExecutablePath -eq $exe){$reply=Invoke-CimMethod -InputObject $again -MethodName Terminate -Arguments @{Reason=[uint32]0} -ErrorAction Stop;if($reply.ReturnValue -ne 0){throw ('Не удалось завершить процесс сборщика '+$p.ProcessId)}}}
 Start-Sleep -Milliseconds 300
 $left=@(Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction Stop|Where-Object {$_.CommandLine -and ($_.CommandLine.IndexOf($path+'\',[StringComparison]::OrdinalIgnoreCase) -ge 0)})
 if($left.Count){throw 'Остались процессы с путём сборщика; файлы не удалены, требуется сверка принадлежности'}
 PlainPath (Join-Path $path 'execution.lock')
 try{$gate=[IO.File]::Open((Join-Path $path 'execution.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{throw 'Завершение сборщика не подтверждено; файлы сохранены'}
 if($task){$again=Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction Stop;if($again.State -in @('Running','Queued') -or $again.Actions[0].Arguments -ne $dcArguments43){throw 'Задача изменилась во время очистки'};Unregister-ScheduledTask -TaskName $name -TaskPath '\' -Confirm:$false -ErrorAction Stop;$removed+=$name}
 $files=@('collect.ps1','Reboot.ps1','Activity.ps1','result.json','result.tmp','state.json','state.tmp','run.json','run.tmp','history.json','cancel.flag','error.txt','Reboot.json','Activity.json','Reboot.error','Activity.error','Reboot.cancel','Activity.cancel','blocked.flag','package43.json','package43.tmp')
 foreach($file in $files){$p=Join-Path $path $file;PlainPath $p;if(Test-Path -LiteralPath $p){Remove-Item -LiteralPath $p -Force -ErrorAction Stop;$removed+=$file}}
 $gate.Dispose();$gate=$null
 # Keep admission.lock while held so another console cannot create a second lock inode.
 foreach($f in @(Get-ChildItem -LiteralPath $path -Force|Where-Object {$_.Name -notin @('admission.lock','execution.lock')})){$skipped+=$f.Name}
 @{Status=$(if($skipped.Count){'Partial'}else{'Removed'});Reason='Сборщик удалён и повторный запуск запрещён. Задания обновлений и системные службы сохранены. Сохранены минимальный запрет установки и файлы блокировки.';Removed=@($removed);Unknown=@($skipped)}|ConvertTo-Json -Depth 6 -Compress
}catch{@{Status='Partial';Reason=$_.Exception.Message;Removed=@($removed);Disabled=(Test-Path -LiteralPath $disabled)}|ConvertTo-Json -Depth 6 -Compress}
finally{if($gate){$gate.Dispose()};if($lock){$lock.Dispose()}}
