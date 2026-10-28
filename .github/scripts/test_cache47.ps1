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
 foreach($script:case in @('Busy','InvalidRoot','InvalidFile','JournalLink','StopFailure','StopWaitTimeout','StartFailure','PrimaryAndRestore','MoveFailure','MaintenanceMissing','Success')){
  $env:windir=Join-Path $Root $script:case;$env:ProgramData=Join-Path $env:windir 'ProgramData'
  $dcServiceRecovery47=Join-Path $env:windir 'service-recovery'
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
  if($script:case -eq 'InvalidFile'){Remove-Item $download -Recurse -Force;Set-Content $download 'not a directory'}
  if($script:case -eq 'JournalLink'){
   $elsewhere=Join-Path $env:windir 'journal-outside';New-Item $elsewhere -ItemType Directory|Out-Null
   New-Item $dcServiceRecovery47 -ItemType Junction -Value $elsewhere|Out-Null
  }
  if($script:case -eq 'MoveFailure'){$code=$text.Replace('[IO.Directory]::Move($p,$dest)',"throw 'fixture move failure'")}
  $lines=New-Object 'System.Collections.Generic.List[string]' ;$failed=$false;$errorText=''
  try{& ([ScriptBlock]::Create($code))|ForEach-Object {$lines.Add([string]$_)}}catch{$failed=$true;$errorText=$_.ToString()}
  $output=$lines -join "`n"
  $success=$script:case -in @('Success','MaintenanceMissing')
  if($failed -eq $success){throw "Unexpected result: $script:case; $errorText"}
  if(!$success -and ($output.Contains('Кэш сброшен') -or $script:maintenance)){throw "False cache success/cleanup launch: $script:case"}
  if($script:case -in @('Busy','InvalidRoot','InvalidFile','JournalLink') -and ($script:stops.Count -or $script:starts.Count)){throw "Validation failure changed services: $script:case"}
  if($script:case -in @('StopFailure','StopWaitTimeout','PrimaryAndRestore') -and ($script:starts.Contains('cryptsvc') -or $script:states.cryptsvc -ne 'Stopped')){throw 'Untouched service changed after early stop failure'}
  if($script:case -in @('StopFailure','StopWaitTimeout','MoveFailure') -and @($script:starts|Where-Object {$script:states[$_] -ne 'Running'}).Count){throw "Restoration skipped: $script:case"}
  if($script:case -eq 'PrimaryAndRestore' -and (!$errorText.Contains('fixture primary stop failure') -or !$output.Contains('fixture restore failure'))){throw 'Restoration replaced primary failure'}
  if($success -and (!(Get-ChildItem (Split-Path $download) -Filter 'Download.DomainConsoleBackup_*') -or $script:maintenance -ne 1)){throw 'Successful reset lost backup or duplicated cleanup request'}
  if($script:case -eq 'MaintenanceMissing' -and !$output.Contains('запуск фоновой очистки не подтверждён')){throw 'Missing cleanup task was hidden'}
  $pending=@(Get-ChildItem $dcServiceRecovery47 -Filter '*.pending' -ErrorAction SilentlyContinue)
  if($script:case -in @('StartFailure','PrimaryAndRestore')){if($pending.Count -ne 1 -or $pending[0].BaseName -ne 'bits'){throw 'Failed restoration did not retain exactly its intent'}}
  elseif($pending.Count){throw 'Completed/untouched service retains pending intent'}
  "Verified cache failure boundary: $script:case"
 }
}finally{$env:windir=$originalWindir;$env:ProgramData=$originalProgramData}
