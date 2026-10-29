$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue'
$root=Join-Path $env:ProgramData 'DomainConsoleCSharp'
foreach($p in @($root,(Join-Path $root 'repair48.lock'),(Join-Path $root 'repair48.json'))){if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Ссылка вместо данных восстановления'}}
New-Item -ItemType Directory -Path $root -Force|Out-Null
$gate=$null
try{
 try{$gate=[IO.File]::Open((Join-Path $root 'repair48.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{@{Status='Busy';Reason='Другая консоль восстанавливает сборщик; повтор не создан'}|ConvertTo-Json -Compress;return}
 $record=Join-Path $root 'repair48.json';$previous=$null
 if(Test-Path -LiteralPath $record){if((Get-Item -LiteralPath $record).Length -gt 1048576){throw 'Слишком большой журнал восстановления'};try{$previous=Get-Content -LiteralPath $record -Raw -Encoding UTF8|ConvertFrom-Json}catch{}}
 # A competing console reuses the verified outcome, not another reinstall.
 $age=if($previous -and $previous.EndedUtc){([DateTime]::UtcNow-[DateTime]::Parse($previous.EndedUtc).ToUniversalTime()).TotalSeconds}else{999}
 if($previous -and $previous.Status -eq 'Recovered' -and $age -ge 0 -and $age -lt 45){
  $read=& { @@READ@@ }|ConvertFrom-Json
  if($read.Status -eq 'Ready' -and $read.Snapshot.Id -eq $previous.SnapshotId -and $read.Snapshot.Healthy -and $read.Snapshot.Complete){@{Status='Recovered';Snapshot=$read.Snapshot;Joined=$true}|ConvertTo-Json -Depth 28 -Compress;return}
 }
 $id=[Guid]::NewGuid().ToString();$started=[DateTime]::UtcNow.ToString('o')
 @{Id=$id;Status='Running';StartedUtc=$started}|ConvertTo-Json -Compress|Set-Content -LiteralPath $record -Encoding UTF8
 try{
  $removed=& { @@REMOVE@@ }|ConvertFrom-Json
  if($removed.Status -ne 'Removed'){throw ('Удаление не подтверждено: '+($removed|ConvertTo-Json -Depth 6 -Compress))}
  $enabled=& { @@ENABLE@@ }|ConvertFrom-Json
  if($enabled.Status -ne 'Enabled'){throw ('Включение не подтверждено: '+($enabled|ConvertTo-Json -Depth 6 -Compress))}
  $clock=[Diagnostics.Stopwatch]::StartNew();$first=$true
  do{
   $reply=if($first){& { @@GET@@ }|ConvertFrom-Json}else{& { @@READ@@ }|ConvertFrom-Json};$first=$false
   if($reply.Status -eq 'Ready'){break}
   if($reply.Status -notin @('Busy','Stopping')){throw ($reply.Reason+' ['+$reply.Status+']')}
   Start-Sleep -Seconds 2
  }while($clock.Elapsed.TotalSeconds -lt 90)
  if($reply.Status -ne 'Ready' -or !$reply.Snapshot.Healthy -or !$reply.Snapshot.Complete){throw 'Полный свежий снимок после восстановления не получен'}
  $at=[DateTime]::Parse($reply.Snapshot.CollectedUtc).ToUniversalTime()
  if($at -lt [DateTime]::Parse($started).ToUniversalTime() -or $at -gt [DateTime]::UtcNow.AddSeconds(5)){throw 'Время снимка не подтверждает новый сбор'}
  @{Id=$id;Status='Recovered';StartedUtc=$started;EndedUtc=[DateTime]::UtcNow.ToString('o');SnapshotId=$reply.Snapshot.Id}|ConvertTo-Json -Compress|Set-Content -LiteralPath $record -Encoding UTF8
  @{Status='Recovered';Snapshot=$reply.Snapshot;Joined=$false}|ConvertTo-Json -Depth 28 -Compress
 }catch{
  @{Id=$id;Status='Failed';StartedUtc=$started;EndedUtc=[DateTime]::UtcNow.ToString('o');Reason=$_.Exception.Message.Substring(0,[Math]::Min(4096,$_.Exception.Message.Length))}|ConvertTo-Json -Compress|Set-Content -LiteralPath $record -Encoding UTF8
  throw
 }
}finally{if($gate){$gate.Dispose()}}
