$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue'
$mode='@@MODE@@'
$path=Join-Path $env:ProgramData 'DomainConsoleCSharp\Snapshot41'
New-Item -Path $path -ItemType Directory -Force|Out-Null
$acl=New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
foreach($sid in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier($sid)),'FullControl','ContainerInherit,ObjectInherit','None','Allow')))}
Set-Acl -LiteralPath $path -AclObject $acl
function Read-Json($name){$f=Join-Path $path $name;if(Test-Path -LiteralPath $f){if((Get-Item -LiteralPath $f).Length -gt 1048576){throw 'Снимок служебного опроса превышает 1 МБ'};try{return Get-Content -LiteralPath $f -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop}catch{return $null}};return $null}
function Reply($status,$reason,$seconds,$snapshot=$null){@{Status=$status;Reason=$reason;RetrySeconds=$seconds;Snapshot=$snapshot;Run=$run;State=(Read-Json 'state.json')}|ConvertTo-Json -Depth 28 -Compress}
function Save-Run{$run|ConvertTo-Json -Depth 6 -Compress|Set-Content -LiteralPath (Join-Path $path 'run.tmp') -Encoding UTF8;Move-Item -LiteralPath (Join-Path $path 'run.tmp') -Destination (Join-Path $path 'run.json') -Force}
$lock=$null;$run=$null
try{
 try{$lock=[IO.File]::Open((Join-Path $path 'admission.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{Reply 'Busy' 'Проверку уже координирует другая консоль' 10;return}
 $name='DomainConsoleCSharp-Snapshot41';$exe=Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe';$script=Join-Path $path 'collect.ps1'
 $arguments='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$script+'"'
 $task=Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue
 if($task){$seconds=[System.Xml.XmlConvert]::ToTimeSpan($task.Settings.ExecutionTimeLimit).TotalSeconds
  if(@($task.Actions).Count -ne 1 -or $task.Actions[0].Execute -ne $exe -or $task.Actions[0].Arguments -ne $arguments -or $task.Principal.UserId -notin @('SYSTEM','S-1-5-18') -or $task.Settings.MultipleInstances -ne 'IgnoreNew' -or $seconds -notin @(40,60)){Reply 'Blocked' 'Параметры существующей задачи отличаются; требуется диагностика' 0;return}}
 $run=Read-Json 'run.json';$snapshot=Read-Json 'result.json';$state=Read-Json 'state.json'
 $running=$task -and $task.State -in @('Running','Queued')
 if($running){
  $started=if($run){[DateTime]::Parse($run.StartedUtc).ToUniversalTime()}else{(Get-ScheduledTaskInfo -InputObject $task).LastRunTime.ToUniversalTime()}
  $age=([DateTime]::UtcNow-$started).TotalSeconds
  if($mode -eq 'Stop' -or $age -ge 45){[IO.File]::WriteAllText((Join-Path $path 'cancel.flag'),'cancel')}
  # A command to stop is not proof of stop. Hold admission while rechecking.
  if($age -ge 65){Stop-ScheduledTask -InputObject $task -ErrorAction Stop;Reply 'Stopping' 'Превышен предел сборщика; проверяется остановка' 10;return}
  Reply $(if($mode -eq 'Stop'){'Stopping'}else{'Busy'}) $(if($mode -eq 'Stop'){'Запрошено штатное завершение сборщика'}elseif($age -ge 20){'Сборщик отвечает дольше обычного'}else{'Сборщик выполняется'}) 10;return
 }
 # OS task state alone is insufficient: a previous worker must also be gone.
 $execution=$null
 try{$execution=[IO.File]::Open((Join-Path $path 'execution.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{Reply 'Unknown' 'Предыдущий исполнитель ещё удерживает блокировку; новый не запущен' 120;return}
 try{
  $workers=@(Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction Stop|Where-Object {$_.CommandLine -and ($_.CommandLine.IndexOf($script,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or $_.CommandLine.IndexOf((Join-Path $path 'Reboot.ps1'),[StringComparison]::OrdinalIgnoreCase) -ge 0 -or $_.CommandLine.IndexOf((Join-Path $path 'Activity.ps1'),[StringComparison]::OrdinalIgnoreCase) -ge 0)})
  if($workers.Count){Reply 'Unknown' 'Процесс предыдущего сборщика ещё существует; требуется сверка' 120;return}
  if($mode -eq 'Inspect'){Reply 'Inspected' $(if($run){[string]$run.Error}else{'Сборщик не запускался этой версией'}) 0 $snapshot;return}
  if($mode -eq 'Stop'){Reply 'Stopped' 'Остановка служебного сборщика подтверждена' 120 $snapshot;return}
  if($mode -eq 'Recover'){
   if($run){$run.Failures=0;$run.NextUtc='';$run.Error='';$run.Accounted=$true;Save-Run}
   Remove-Item -LiteralPath (Join-Path $path 'blocked.flag'),(Join-Path $path 'result.json') -Force -ErrorAction SilentlyContinue
   Reply 'Recovered' 'Предыдущий исполнитель завершён; следующий опрос разрешён' 0;return
  }
  if($run -and !$run.Accounted -and (!$snapshot -or $snapshot.Id -ne $run.Id) -and ([DateTime]::UtcNow-[DateTime]::Parse($run.StartedUtc).ToUniversalTime()).TotalSeconds -lt 10){Reply 'Busy' 'Планировщик запускает сборщик' 10;return}
  if($run -and !$run.Accounted){
   if($snapshot -and $snapshot.Id -eq $run.Id -and $snapshot.Healthy){$run.Failures=0;$run.NextUtc='';$run.Error=''}
   else{
    $run.Failures=[int]$run.Failures+1;$reason=if($snapshot -and $snapshot.Id -eq $run.Id){[string]$snapshot.Error}else{'Сборщик завершился без результата (возможен предельный тайм-аут)'}
    $err=Join-Path $path 'error.txt';if(Test-Path -LiteralPath $err){$reason+='; '+[string](Get-Content -LiteralPath $err -Encoding UTF8 -TotalCount 8)}
    $run.Error=$reason;$delay=if($run.Failures -eq 1){120}elseif($run.Failures -eq 2){300}else{900};$run.NextUtc=[DateTime]::UtcNow.AddSeconds($delay).ToString('o')
   }
   $run.Accounted=$true;Save-Run
  }
  if($snapshot -and $run -and $snapshot.Id -eq $run.Id){$at=[DateTime]::Parse($snapshot.CollectedUtc).ToUniversalTime();if(([DateTime]::UtcNow-$at).TotalSeconds -ge 0 -and ([DateTime]::UtcNow-$at).TotalSeconds -lt 45){Reply 'Ready' '' 0 $snapshot;return}}
  if($run -and [int]$run.Failures -ge 3){Reply 'Blocked' ('Три сбоя сборщика подряд: '+$run.Error) 0;return}
  if($run -and $run.NextUtc){$left=([DateTime]::Parse($run.NextUtc).ToUniversalTime()-[DateTime]::UtcNow).TotalSeconds;if($left -gt 0){Reply 'Cooldown' $run.Error ([int][Math]::Ceiling($left));return}}
  if($mode -eq 'Read'){Reply 'Idle' 'Предыдущий запуск завершён; новый не создавался' 0;return}
  # No live instance, protected admission and execution gates held: safe migration.
  [IO.File]::WriteAllText($script,@@COLLECTOR@@,(New-Object Text.UTF8Encoding($true)))
  [IO.File]::WriteAllText((Join-Path $path 'Reboot.ps1'),@@REBOOT@@,(New-Object Text.UTF8Encoding($true)))
  [IO.File]::WriteAllText((Join-Path $path 'Activity.ps1'),@@ACTIVITY@@,(New-Object Text.UTF8Encoding($true)))
  foreach($file in @('result.json','state.json','cancel.flag','error.txt','Reboot.json','Activity.json','Reboot.error','Activity.error','Reboot.cancel','Activity.cancel','blocked.flag')){Remove-Item -LiteralPath (Join-Path $path $file) -Force -ErrorAction SilentlyContinue}
  if($run){$history=@(Read-Json 'history.json')|Where-Object {$_};$history=@($history|Select-Object -Last 15)+@($run);ConvertTo-Json -InputObject @($history) -Depth 8 -Compress|Set-Content -LiteralPath (Join-Path $path 'history.json') -Encoding UTF8}
  $failures=if($run){[int]$run.Failures}else{0}
  $run=@{Id=[Guid]::NewGuid().ToString();StartedUtc=[DateTime]::UtcNow.ToString('o');Failures=$failures;NextUtc='';Error='';Accounted=$false;Version=42};Save-Run
  if(!$task -or $seconds -ne 60){$action=New-ScheduledTaskAction -Execute $exe -Argument $arguments;$principal=New-ScheduledTaskPrincipal -UserId SYSTEM -LogonType ServiceAccount -RunLevel Highest;$settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromSeconds(60)) -MultipleInstances IgnoreNew -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries;Register-ScheduledTask -TaskName $name -TaskPath '\' -Action $action -Principal $principal -Settings $settings -Description 'DomainConsole bounded read-only supervisor 42; no update actions' -Force|Out-Null}
  Start-ScheduledTask -TaskName $name -TaskPath '\'
  Reply 'Busy' 'Сборщик запущен; ожидается результат' 10
 }finally{if($execution){$execution.Dispose()}}
}finally{if($lock){$lock.Dispose()}}
