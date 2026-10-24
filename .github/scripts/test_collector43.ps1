param([Parameter(Mandatory)]$Session,[Parameter(Mandatory)][string]$Fixture)
$ErrorActionPreference='Stop'
function Run([string]$file){$code=[IO.File]::ReadAllText((Join-Path $Fixture $file));Invoke-Command -Session $Session -ScriptBlock {param($s);& ([ScriptBlock]::Create($s))} -ArgumentList $code -ErrorAction Stop|ConvertFrom-Json}
foreach($file in @('collector43-remove.ps1','collector43-enable.ps1')){$t=$null;$e=$null;$null=[Management.Automation.Language.Parser]::ParseFile((Join-Path $Fixture $file),[ref]$t,[ref]$e);if($e){throw ($e|Out-String)}}
$task='DomainConsoleCSharp-Snapshot41';$path=Join-Path $env:ProgramData 'DomainConsoleCSharp\Snapshot41'
# Existing real SYSTEM collector installed by previous suite. Native SID / translated name accepted.
$inspect=Run 'probe42-Inspect.ps1';if($inspect.Status -ne 'Inspected' -or $inspect.Differences.Count){throw 'Inspection failed for real SYSTEM principal'}
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
Write-Output 'Collector43: real WinRM inspection with mismatched task, SYSTEM identity, no downgrade, owned hung supervisor cleanup, foreign task protection, unrelated maintenance/data preservation, disabled reinstall gate, idempotency and re-enable passed.'
