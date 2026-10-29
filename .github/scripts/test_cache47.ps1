param([Parameter(Mandatory=$true)][string]$Template,[Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop'
$text=[IO.File]::ReadAllText($Template).Replace('(New-Object -ComObject Microsoft.Update.Installer).IsBusy','$fixtureBusy').Replace('$backup=$false','$backup=$true')
$originalWindir=$env:windir;$originalProgramData=$env:ProgramData
function Get-Service($Name){
 $obj=[pscustomobject]@{Name=$Name;Status=$script:states[$Name]}
 $obj|Add-Member ScriptMethod WaitForStatus {param($status,$timeout)
  if($script:case -eq 'StopWaitTimeout' -and $this.Name -eq 'bits' -and $status -eq 'Stopped'){throw [TimeoutException]::new('fixture stop wait timeout')}
 }
 return $obj
}
function Stop-Service($Name){
 $script:stops.Add($Name);$script:states[$Name]='Stopped'
 if($script:case -in @('StopFailure','PrimaryAndRestore') -and $Name -eq 'bits'){throw 'fixture primary stop failure'}
}
function Start-Service($Name){
 $script:starts.Add($Name)
 if($script:case -in @('StartFailure','PrimaryAndRestore') -and $Name -eq 'bits'){throw 'fixture restore failure'}
 $script:states[$Name]='Running'
}
function Start-ScheduledTask($TaskName){$script:maintenance++;if($script:case -eq 'MaintenanceMissing'){throw 'fixture maintenance missing'}}
try {
 foreach($script:case in @('Busy','InvalidRoot','ParentLink','InvalidFile','JournalLink','StopFailure','StopWaitTimeout','StartFailure','PrimaryAndRestore','MoveFailure','Missing','Disappeared','PartialIsolation','MaintenanceMissing','PendingSuccess','Success')){
  $env:windir=Join-Path $Root $script:case;$env:ProgramData=Join-Path $env:windir 'ProgramData'
  $dcServiceRecovery47=Join-Path $env:windir 'service-recovery'
  $dcCacheResult50=Join-Path $env:windir 'cache-result50.json'
  $download=Join-Path $env:windir 'SoftwareDistribution\Download'
  New-Item $download -ItemType Directory -Force|Out-Null;Set-Content (Join-Path $download 'keep.bin') 'fixture'
  $script:states=@{UsoSvc='Running';wuauserv='Running';bits='Running';cryptsvc='Stopped'}
  $script:stops=New-Object 'System.Collections.Generic.List[string]';$script:starts=New-Object 'System.Collections.Generic.List[string]';$script:maintenance=0
  $fixtureBusy=$script:case -eq 'Busy';$dcAllowUpdateConflict=$false
  $code=$text
  if($script:case -eq 'InvalidRoot'){
   # An isolated junction exercises the real reparse-point guard before service mutation.
   Remove-Item $download -Recurse -Force
   $elsewhere=Join-Path $env:windir 'outside';New-Item $elsewhere -ItemType Directory|Out-Null;Set-Content (Join-Path $elsewhere 'keep.bin') 'fixture'
   New-Item $download -ItemType Junction -Value $elsewhere|Out-Null
  }
  if($script:case -eq 'ParentLink'){
   $parent=Split-Path $download;Remove-Item $parent -Recurse -Force
   $elsewhere=Join-Path $env:windir 'outside';New-Item (Join-Path $elsewhere 'Download') -ItemType Directory -Force|Out-Null
   Set-Content (Join-Path $elsewhere 'Download/keep.bin') 'fixture'
   New-Item $parent -ItemType Junction -Value $elsewhere|Out-Null
  }
  if($script:case -in @('MaintenanceMissing','PendingSuccess')){$code=$text.Replace('$backup=$true','$backup=$false')}
  if($script:case -eq 'InvalidFile'){Remove-Item $download -Recurse -Force;Set-Content $download 'not a directory'}
  if($script:case -eq 'JournalLink'){
   $elsewhere=Join-Path $env:windir 'journal-outside';New-Item $elsewhere -ItemType Directory|Out-Null
   New-Item $dcServiceRecovery47 -ItemType Junction -Value $elsewhere|Out-Null
  }
  if($script:case -eq 'MoveFailure'){$code=$text.Replace('[IO.Directory]::Move($p,$dest)',"throw 'fixture move failure'")}
  if($script:case -eq 'Missing'){Remove-Item $download -Recurse -Force}
  if($script:case -eq 'Disappeared'){$code=$text.Replace('[IO.Directory]::Move($p,$dest)',"Remove-Item -LiteralPath `$p -Recurse -Force;[IO.Directory]::Move(`$p,`$dest)")}
  if($script:case -eq 'PartialIsolation'){
   $first=Join-Path (Split-Path $download) 'EventCache';$second=Join-Path (Split-Path $download) 'EventCache.v2'
   New-Item $first,$second -ItemType Directory|Out-Null;Set-Content (Join-Path $first 'event.bin') 'fixture'
   $code=$text.Replace("$"+"mode='Download'","$"+"mode='Queue'").Replace('$backup=$true','$backup=$false').Replace('[IO.Directory]::Move($p,$dest)',"if(`$p.EndsWith('EventCache.v2')){throw 'fixture second isolation denied'};[IO.Directory]::Move(`$p,`$dest)")
  }
  $lines=New-Object 'System.Collections.Generic.List[string]' ;$failed=$false;$errorText=''
  try{& ([ScriptBlock]::Create($code))|ForEach-Object {$lines.Add([string]$_)}}catch{$failed=$true;$errorText=$_.ToString()}
  $output=$lines -join "`n"
  $success=$script:case -in @('Success','MaintenanceMissing','PendingSuccess','Missing','Disappeared')
  if($success){
   $summary50=Get-Content $dcCacheResult50 -Raw|ConvertFrom-Json
   if($script:case -in @('Missing','Disappeared') -and $summary50.Isolated -ne 0){throw 'Absent cache reported as isolated'}
   if($script:case -eq 'PendingSuccess' -and ($summary50.PendingDelete -ne 1 -or !$summary50.CleanupStarted -or $summary50.Backups -ne 0 -or $output.Contains('Резервная копия сохранена.'))){throw 'Successful delayed deletion summary incorrect'}
   if($script:case -eq 'Success' -and ($summary50.Backups -ne 1 -or $summary50.PendingDelete -ne 0 -or !$output.Contains('Резервная копия сохранена.'))){throw 'Backup summary incorrect'}
   if($script:case -eq 'MaintenanceMissing' -and ($summary50.PendingDelete -ne 1 -or $summary50.CleanupStarted -or $output.Contains('Резервная копия сохранена.'))){throw 'Pending deletion reported as backup/completed cleanup'}
  }elseif(Test-Path $dcCacheResult50){throw 'Failed cleanup wrote success summary'}
  if($failed -eq $success){throw "Unexpected result: $script:case; $errorText"}
  if(!$success -and ($output.Contains('Кэш сброшен') -or $script:maintenance)){throw "False cache success/cleanup launch: $script:case"}
  if($script:case -in @('Busy','InvalidRoot','ParentLink','InvalidFile','JournalLink') -and ($script:stops.Count -or $script:starts.Count)){throw "Validation failure changed services: $script:case"}
  if($script:case -in @('StopFailure','StopWaitTimeout','PrimaryAndRestore') -and ($script:starts.Contains('cryptsvc') -or $script:states.cryptsvc -ne 'Stopped')){throw 'Untouched service changed after early stop failure'}
  if($script:case -in @('StopFailure','StopWaitTimeout','MoveFailure') -and @($script:starts|Where-Object {$script:states[$_] -ne 'Running'}).Count){throw "Restoration skipped: $script:case"}
  if($script:case -eq 'PrimaryAndRestore' -and (!$errorText.Contains('fixture primary stop failure') -or !$output.Contains('fixture restore failure'))){throw 'Restoration replaced primary failure'}
  if($script:case -eq 'Success' -and (!(Get-ChildItem (Split-Path $download) -Filter 'Download.DomainConsoleBackup_*') -or $script:maintenance -ne 0)){throw 'Backup was lost or incorrectly scheduled for physical cleanup'}
  if($script:case -eq 'MaintenanceMissing' -and (!(Get-ChildItem (Split-Path $download) -Filter 'Download.DomainConsolePendingDelete_*') -or $script:maintenance -ne 1)){throw 'Actual isolated cache was not scheduled for cleanup'}
  if($script:case -eq 'ParentLink' -and !(Test-Path (Join-Path $elsewhere 'Download/keep.bin'))){throw 'Parent junction changed external cache'}
  if($script:case -eq 'MaintenanceMissing' -and !$output.Contains('запуск фоновой очистки не подтверждён')){throw 'Missing cleanup task was hidden'}
  if($script:case -in @('Missing','Disappeared') -and ($script:maintenance -ne 0 -or $output.Contains('Кэш сброшен'))){throw 'Absent cache reported as isolated or scheduled'}
  if($script:case -eq 'PartialIsolation'){
   $manifest=Join-Path $env:ProgramData 'DomainConsoleCSharp/pending-cache-cleanup.jsonl'
   $entries=@(Get-Content $manifest|ForEach-Object {$_|ConvertFrom-Json})
   if($entries.Count -ne 1 -or !(Test-Path $entries[0].Path) -or !(Test-Path $second) -or !(Test-Path (Join-Path $download 'keep.bin'))){throw 'Partial isolation lost registration or changed another cache'}
   if(!$errorText.Contains('fixture second isolation denied')){throw 'Partial isolation lost primary failure'}
  }
  $pending=@(Get-ChildItem $dcServiceRecovery47 -Filter '*.pending' -ErrorAction SilentlyContinue)
  if($script:case -in @('StartFailure','PrimaryAndRestore')){if($pending.Count -ne 1 -or $pending[0].BaseName -ne 'bits'){throw 'Failed restoration did not retain exactly its intent'}}
  elseif($pending.Count){throw 'Completed/untouched service retains pending intent'}
  "Verified cache failure boundary: $script:case"
 }
}finally{$env:windir=$originalWindir;$env:ProgramData=$originalProgramData}
