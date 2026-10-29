param([Parameter(Mandatory)]$Session,[Parameter(Mandatory)][string]$Fixture)
$ErrorActionPreference='Stop'
function Run([string]$file){$code=[IO.File]::ReadAllText((Join-Path $Fixture $file));Invoke-Command -Session $Session -ScriptBlock {param($s);& ([ScriptBlock]::Create($s))} -ArgumentList $code -ErrorAction Stop|ConvertFrom-Json}
foreach($file in @('collector43-remove.ps1','collector43-enable.ps1')){$t=$null;$e=$null;$null=[Management.Automation.Language.Parser]::ParseFile((Join-Path $Fixture $file),[ref]$t,[ref]$e);if($e){throw ($e|Out-String)}}
$task='DomainConsoleCSharp-Snapshot41';$path=Join-Path $env:ProgramData 'DomainConsoleCSharp\Snapshot41'
# Existing real SYSTEM collector installed by previous suite. Native SID / translated name accepted.
$inspect=Run 'probe42-Inspect.ps1';if($inspect.Status -ne 'Inspected' -or $inspect.Differences.Count){throw 'Inspection failed for real SYSTEM principal'}
$beforeInspect=@(Get-ChildItem $path -File|ForEach-Object { $_.Name+':'+$_.LastWriteTimeUtc.Ticks+':'+$_.Length }) -join '|'
$aclBefore=(Get-Acl $path).Sddl
$null=Run 'probe42-Inspect.ps1'
$afterInspect=@(Get-ChildItem $path -File|ForEach-Object { $_.Name+':'+$_.LastWriteTimeUtc.Ticks+':'+$_.Length }) -join '|'
if($beforeInspect -ne $afterInspect -or $aclBefore -ne (Get-Acl $path).Sddl){throw 'Read-only inspection changed collector files or ACL'}
if($inspect.ExpectedTask.PrincipalSid -ne 'S-1-5-18' -or $inspect.ActualTask.PrincipalSid -ne 'S-1-5-18' -or !$inspect.TaskPresent){throw 'Expected/actual task configuration omitted'}
$original=Export-ScheduledTask $task
try{
 $wrong=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromSeconds(90)) -MultipleInstances IgnoreNew
 Set-ScheduledTask -TaskName $task -Settings $wrong|Out-Null
 $inspect=Run 'probe42-Inspect.ps1';if($inspect.Status -ne 'Inspected' -or !($inspect.Differences -like 'ExecutionTimeLimit:*')){throw 'Mismatch prevented diagnosis or lost exact field'}
 if((Run 'probe42-Get.ps1').Status -ne 'Blocked'){throw 'Foreign configuration overwritten'}
 $remove=Run 'collector43-remove.ps1';if($remove.Status -ne 'Partial' -or !(Get-ScheduledTask $task)){throw 'Unknown task deleted by cleanup'}
}finally{Register-ScheduledTask -TaskName $task -Xml $original -Force|Out-Null;$null=Run 'collector43-enable.ps1'}
# Future package must not be downgraded by this console.
$manifest=Join-Path $path 'package43.json';$old=[IO.File]::ReadAllText($manifest)
try{$p=$old|ConvertFrom-Json;$p.Version=99;$p|ConvertTo-Json|Set-Content $manifest -Encoding UTF8;if((Run 'probe42-Get.ps1').Status -ne 'Blocked'){throw 'Newer collector downgraded'}}finally{[IO.File]::WriteAllText($manifest,$old)}
# A 0.43 package request must not roll a 0.47 collector back.
if(($old|ConvertFrom-Json).Version -ne 47){throw 'Collector package revision was not advanced'}
$legacy=[IO.File]::ReadAllText((Join-Path $Fixture 'probe42-Get.ps1')).Replace('"Version":47','"Version":43')
$beforeLegacy=Get-FileHash (Join-Path $path 'collect.ps1')
$legacyResult=Invoke-Command -Session $Session -ScriptBlock {param($s);& ([ScriptBlock]::Create($s))} -ArgumentList $legacy -ErrorAction Stop|ConvertFrom-Json
if($legacyResult.Status -ne 'Blocked' -or (Get-FileHash (Join-Path $path 'collect.ps1')).Hash -ne $beforeLegacy.Hash){throw 'Older package request changed newer collector'}
# Stop and remove a real unresponsive read-only supervisor; preserve unrelated job data.
$sentinel=Join-Path (Split-Path $path) ('fixture-preserve43-'+[Guid]::NewGuid().ToString('N'));New-Item $sentinel -ItemType Directory|Out-Null;Set-Content (Join-Path $sentinel 'status.json') 'do not remove'
try{
 $null=Run 'probe42-Recover.ps1'
 $start=Run 'probe42-autonomous.ps1';if($start.Status -ne 'Busy'){throw 'Could not start hung cleanup fixture'};Start-Sleep -Seconds 2
 $r=Run 'collector43-remove.ps1';if($r.Status -ne 'Removed'){throw ('Cleanup failed: '+($r|ConvertTo-Json -Depth 6))}
 if(Get-ScheduledTask $task -ErrorAction SilentlyContinue){throw 'Collector task survived cleanup'}
 foreach($file in @('collect.ps1','Reboot.ps1','Activity.ps1','result.json','package43.json')){if(Test-Path (Join-Path $path $file)){throw 'Collector file survived cleanup'}}
 if(!(Test-Path (Join-Path $sentinel 'status.json')) -or !(Get-ScheduledTask 'DomainConsoleCSharp-Maintenance' -ErrorAction SilentlyContinue)){throw 'Cleanup touched executor/maintenance data'}
 if((Run 'probe42-Get.ps1').Status -ne 'Disabled'){throw 'Timer reinstalled removed collector'}
 if((Run 'collector43-remove.ps1').Status -ne 'Removed'){throw 'Repeated cleanup not idempotent'}
 if((Run 'collector43-enable.ps1').Status -ne 'Enabled'){throw 'Explicit re-enable failed'}
 if((Run 'probe42-Get.ps1').Status -ne 'Busy'){throw 'Re-enabled collector did not install'}
 $until=[DateTime]::UtcNow.AddSeconds(100);do{Start-Sleep -Seconds 2;$r=Run 'probe42-Read.ps1'}while($r.Status -in @('Busy','Stopping') -and [DateTime]::UtcNow -lt $until)
 if($r.Status -ne 'Ready'){throw 'Collector failed after reinstall'}
}finally{Remove-Item $sentinel -Recurse -Force -ErrorAction SilentlyContinue}
# New reset is read-only task control: confirmed stop, then remote cooldown.
$reset=Run 'probe44-Reset.ps1'
if($reset.Status -ne 'Cooldown' -or $reset.RetrySeconds -ne 120){throw 'Reset skipped confirmed stop/cooldown'}
if((Run 'probe42-Get.ps1').Status -ne 'Cooldown'){throw 'Reset immediately restarted collector'}
$null=Run 'probe42-Recover.ps1'
Write-Output 'Collector43: real WinRM inspection with mismatched task, SYSTEM identity, no downgrade, owned hung supervisor cleanup, foreign task protection, unrelated maintenance/data preservation, disabled reinstall gate, idempotency and re-enable passed.'


# Two independent remote shells must share one recovery, not reinstall twice.
$repairCode=[IO.File]::ReadAllText((Join-Path $Fixture 'collector48-repair.ps1'))
$tokens=$null;$errors=$null;$null=[Management.Automation.Language.Parser]::ParseInput($repairCode,[ref]$tokens,[ref]$errors);if($errors){throw ($errors|Out-String)}
$second=New-PSSession -Session $Session
$repairJobs=@()
try{
 foreach($connection in @($Session,$second)){$repairJobs+=Invoke-Command -Session $connection -AsJob -ScriptBlock {param($code);& ([ScriptBlock]::Create($code))} -ArgumentList $repairCode}
 $repairJobs|Wait-Job -Timeout 110|Out-Null
 $replies=@(foreach($job in $repairJobs){if($job.State -ne 'Completed'){throw 'Concurrent recovery did not finish'};Receive-Job $job -ErrorAction Stop|ConvertFrom-Json})
 if(!($replies|Where-Object {$_.Status -eq 'Recovered'})){throw 'No recovery confirmed'}
 if($replies|Where-Object {$_.Status -notin @('Recovered','Busy')}){throw 'Unexpected competing recovery result'}
 $registered=(Get-ScheduledTaskInfo $task).LastRunTime
 $record=Get-Content (Join-Path (Split-Path $path) 'repair48.json') -Raw|ConvertFrom-Json
 $joined=Invoke-Command -Session $second -ScriptBlock {param($code);& ([ScriptBlock]::Create($code))} -ArgumentList $repairCode|ConvertFrom-Json
 if($joined.Status -ne 'Recovered' -or !$joined.Joined -or !$joined.Snapshot.Complete -or $joined.Snapshot.Id -ne $record.SnapshotId){throw 'Second console did not join fresh recovery result'}
 if((Get-ScheduledTaskInfo $task).LastRunTime -ne $registered){throw 'Second recovery restarted the collector'}
 Write-Output 'Collector48: two independent WinRM shells share one recovery and one fresh snapshot; repeated recovery does not reinstall.'
}finally{$repairJobs|Remove-Job -Force -ErrorAction SilentlyContinue;Remove-PSSession $second}

# Read WUA history directly: no legacy Snapshot wrapper and no task mutation.
$historyBefore=(Get-ScheduledTaskInfo $task).LastRunTime
$packageBefore=(Get-FileHash (Join-Path $path 'collect.ps1')).Hash
$history=Run 'collector48-history.ps1'
if($null -eq $history.Limit -or $null -eq $history.Total -or $null -ne $history.Reboot -or $history.Limit -gt 500){throw 'History read returned collector data or exceeded its budget'}
if((Get-ScheduledTaskInfo $task).LastRunTime -ne $historyBefore -or (Get-FileHash (Join-Path $path 'collect.ps1')).Hash -ne $packageBefore){throw 'History query mutated the collector'}
Write-Output 'Collector48: bounded WUA history query returns history schema without restarting or replacing collector.'
