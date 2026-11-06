$root=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$file=Join-Path $root 'control37.json'
$repair=@@REPAIR@@
$mutex=[Threading.Mutex]::new($false,'Global\DomainConsoleCSharp.Coordination37')
$held=$false
try {
 try{$held=$mutex.WaitOne(10000)}catch [Threading.AbandonedMutexException]{$held=$true}
 if(!$held){throw 'Координатор занят более 10 секунд. Изменений нет.'}
 foreach($protected in @($root,$file)){if((Test-Path -LiteralPath $protected) -and ((Get-Item -LiteralPath $protected).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Каталог или файл координации является ссылкой. Изменений нет.'}}
 if(!(Test-Path -LiteralPath $file)){@{Rows=@();Removed=@();Message='Сохранённых резерваций нет.'}|ConvertTo-Json -Depth 8 -Compress;return}
 $c=Get-Content -LiteralPath $file -Raw -Encoding UTF8|ConvertFrom-Json
 if($c.Protocol -ne 1){throw 'Неизвестный протокол координации.'}
 $tasks=@(Get-ScheduledTask -ErrorAction Stop)
 $processes=@(Get-CimInstance Win32_Process -Filter "Name='DomainConsole.Agent.exe'" -ErrorAction Stop)
 $boot=(Get-CimInstance Win32_OperatingSystem -ErrorAction Stop).LastBootUpTime.ToUniversalTime()
 $rows=@();$removed=@()
 foreach($id in @($c.JobIds)){
  $guid=[guid]::Empty
  if(![guid]::TryParse($id,[ref]$guid)){throw 'Некорректный ID резервации.'}
  $p=Join-Path $root $id
  $state=$null
  $stateFile=Join-Path $p 'status.json'
  if(Test-Path -LiteralPath $stateFile){$state=Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8|ConvertFrom-Json}
  $related=@($tasks|Where-Object{$_.TaskName -like "*$id*" -or (($_.Actions|ForEach-Object{$_.Arguments}) -join ' ') -like "*$id*"})
  $agents=@($processes|Where-Object{[string]::IsNullOrWhiteSpace($_.CommandLine) -or $_.CommandLine -like "*$id*"})
  $missing=!(Test-Path (Join-Path $p 'job.json')) -and !(Test-Path $stateFile) -and !(Test-Path (Join-Path $p 'submitted.flag'))
  $age=[datetimeoffset]::UtcNow-[datetimeoffset]::Parse($c.UpdatedUtc)
  $linked=(Test-Path $p) -and ((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint)
  $eligible=$missing -and !$linked -and !$related.Count -and !$agents.Count -and $age.TotalMinutes -ge 5
  $oldBoot=$false
  if($state -and !$linked -and !$agents.Count -and !@($related|Where-Object {$_.State -in @('Running','Queued','Unknown')}).Count -and $state.Status -in @('Running','Queued','Submitted') -and !$state.Ended -and $state.RestoreStatus -notin @('Pending','InterventionRequired') -and $state.DiagnosticStatus -notin @('Pending','Running')){
   $started=[datetimeoffset]::MinValue;$updated=[datetimeoffset]::MinValue
   $oldBoot=[datetimeoffset]::TryParse([string]$state.Started,[ref]$started) -and [datetimeoffset]::TryParse([string]$state.Updated,[ref]$updated) -and $started.UtcDateTime -lt $boot -and $updated.UtcDateTime -lt $boot -and !((Get-Item -LiteralPath $stateFile).Attributes -band [IO.FileAttributes]::ReparsePoint)
  }
  if($repair -and $oldBoot){
   # Only reconcile a prior-boot executor. Never resume installation or stop Windows services.
   $backupState=$stateFile+'.before-reconcile60-'+[guid]::NewGuid()+'.bak'
   $now=[datetime]::UtcNow.ToString('o')
   $state|Add-Member NoteProperty Status 'InterventionRequired' -Force
   $state|Add-Member NoteProperty Ended $now -Force
   $state|Add-Member NoteProperty Updated $now -Force
   $state|Add-Member NoteProperty Error 'DC_INTERRUPTED46: Исполнитель отсутствует после новой загрузки Windows. Исход установки неизвестен; необходим свежий поиск.' -Force
   $tempState=$stateFile+'.'+[guid]::NewGuid()+'.tmp'
   try{[IO.File]::WriteAllText($tempState,($state|ConvertTo-Json -Depth 80),[Text.UTF8Encoding]::new($false));[IO.File]::Replace($tempState,$stateFile,$backupState)}finally{if(Test-Path $tempState){Remove-Item -LiteralPath $tempState -Force}}
  }
  $eligible=$eligible -or $oldBoot
  $rows+=@{Id=$id;Status=$(if($state){$state.Status}else{'Результат отсутствует'});Stage=$state.Stage;Updated=$state.Updated;Ended=$state.Ended;Restore=$state.RestoreStatus;Diagnostics=$state.DiagnosticStatus;Tasks=@($related|Select-Object TaskName,State);Processes=@($agents|Select-Object ProcessId,CreationDate);CanRelease=$eligible}
  if($repair -and $eligible){$removed+=@($id)}
 }
 $backup=''
 if($removed.Count){
  $backup=$file+'.before-orphan56-'+[guid]::NewGuid()+'.bak'
  $c.JobIds=@($c.JobIds|Where-Object{$_ -notin $removed})
  if($c.LastJobId -in $removed){$c.LastJobId=''}
  $temp=$file+'.'+[guid]::NewGuid()+'.tmp'
  try{[IO.File]::WriteAllText($temp,($c|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));[IO.File]::Replace($temp,$file,$backup)}finally{if(Test-Path $temp){Remove-Item -LiteralPath $temp -Force}}
 }
 @{Rows=@($rows);Removed=@($removed);Backup=$backup;Owner=$c.OwnerLabel;Plan=$c.PlanId;Updated=$c.UpdatedUtc;Message=$(if($repair){'Снято оставшихся резерваций: '+$removed.Count}else{'Прочитано резерваций: '+$rows.Count})}|ConvertTo-Json -Depth 8 -Compress
}finally{if($held){$mutex.ReleaseMutex()};$mutex.Dispose()}
