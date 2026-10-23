param([Parameter(Mandatory)]$Session,[Parameter(Mandatory)][pscredential]$Credential,[Parameter(Mandatory)][string]$Fixture)
$ErrorActionPreference='Stop'
$code=[IO.File]::ReadAllText((Join-Path $Fixture 'probe-wrapper41.ps1'))
$slow=[IO.File]::ReadAllText((Join-Path $Fixture 'probe-slow41.ps1'))
$task='DomainConsoleCSharp-Snapshot41'
$path=Join-Path $env:ProgramData 'DomainConsoleCSharp\Snapshot41'
function Invoke-Probe([string]$Body){Invoke-Command -Session $Session -ScriptBlock {param($text);& ([ScriptBlock]::Create($text))} -ArgumentList $Body -ErrorAction Stop}
$baseline=@(Get-Process wsmprovhost -ErrorAction SilentlyContinue).Count
$first=Invoke-Probe $code|ConvertFrom-Json
if(!$first.CollectedUtc){throw 'No real SYSTEM snapshot'}
$registered=(Get-ScheduledTaskInfo -TaskName $task).LastRunTime
foreach($n in 1..12){$reply=Invoke-Probe $code|ConvertFrom-Json;if(!$reply.CollectedUtc){throw 'Repeated probe lost cache'}}
if((Get-ScheduledTaskInfo -TaskName $task).LastRunTime -ne $registered){throw 'Cached reads started extra SYSTEM collections'}
$jobs=@()
try{
 foreach($n in 1..6){$jobs+=Invoke-Command -ComputerName localhost -Credential $Credential -Authentication Negotiate -AsJob -ScriptBlock {param($text);try{& ([ScriptBlock]::Create($text))}catch{if($_.Exception.Message -notlike '*DC_PROBE_PAUSED*'){throw};'BUSY'}} -ArgumentList $code}
 $jobs|Wait-Job -Timeout 60|Out-Null
 foreach($job in $jobs){if($job.State -ne 'Completed'){throw 'Concurrent collector did not finish'};$value=Receive-Job $job -ErrorAction Stop;if($value -ne 'BUSY' -and !($value|ConvertFrom-Json).CollectedUtc){throw 'Concurrent cache result missing'}}
 if(@(Get-ScheduledTask -TaskName $task).Count -ne 1){throw 'Collectors accumulated'}
 if((Get-ScheduledTaskInfo -TaskName $task).LastRunTime -ne $registered){throw 'Concurrent readers restarted a fresh collector'}
}finally{$jobs|Remove-Job -Force -ErrorAction SilentlyContinue}

# Actual Task Scheduler timeout: no endless restarts after missing output.
Remove-Item (Join-Path $path 'result.json'),(Join-Path $path 'blocked.flag') -Force -ErrorAction SilentlyContinue
$failed=$false;try{Invoke-Probe $slow|Out-Null}catch{$failed=$_.Exception.Message -like '*DC_PROBE_PAUSED*'}
if(!$failed -or !(Test-Path (Join-Path $path 'blocked.flag'))){throw 'Timed out collection did not latch'}
if((Get-ScheduledTask -TaskName $task).State -in @('Running','Queued')){throw 'SYSTEM task exceeded execution limit'}
$last=(Get-ScheduledTaskInfo -TaskName $task).LastRunTime
foreach($n in 1..4){$blocked=$false;try{Invoke-Probe $code|Out-Null}catch{$blocked=$_.Exception.Message -like '*DC_PROBE_PAUSED*'};if(!$blocked){throw 'Unknown outcome automatically retried'}}
if((Get-ScheduledTaskInfo -TaskName $task).LastRunTime -ne $last){throw 'Failure loop restarted collector'}
Remove-Item (Join-Path $path 'blocked.flag') -Force
if(!(Invoke-Probe $code|ConvertFrom-Json).CollectedUtc){throw 'Explicit resume did not recover'}

# Legacy cleanup: absent directory, foreign task, fresh task and running task.
$created=@();$preview=[IO.File]::ReadAllText((Join-Path $Fixture 'probe-clean-preview41.ps1'));$apply=[IO.File]::ReadAllText((Join-Path $Fixture 'probe-clean-apply41.ps1'))
$exe=Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
function New-Fixture([string]$Body,[bool]$Old,[bool]$Foreign=$false){
 $name='DomainConsoleCSharp-Probe-'+[Guid]::NewGuid().ToString();$script:created+=$name
 $arguments=if($Foreign){'-NoProfile -Command exit'}else{'-NoProfile -NonInteractive -EncodedCommand '+[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Body))}
 $action=New-ScheduledTaskAction -Execute $exe -Argument $arguments
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromSeconds(30))
 $principal=New-ScheduledTaskPrincipal -UserId SYSTEM -LogonType ServiceAccount -RunLevel Highest
 Register-ScheduledTask -TaskName $name -Action $action -Settings $settings -Principal $principal|Out-Null
 [xml]$xml=Export-ScheduledTask $name
 $xml.Task.RegistrationInfo.Date=[DateTime]::UtcNow.AddMinutes($(if($Old){-20}else{0})).ToString('o')
 Unregister-ScheduledTask $name -Confirm:$false
 Register-ScheduledTask -TaskName $name -Xml $xml.OuterXml|Out-Null
 return $name
}
try{
 $old=New-Fixture 'exit 0' $true;$fresh=New-Fixture 'exit 0' $false;$foreign=New-Fixture 'exit 0' $true $true;$running=New-Fixture 'Start-Sleep 25' $true
 Start-ScheduledTask $running;Start-Sleep -Seconds 1
 $before=Invoke-Probe $preview|ConvertFrom-Json
 if(!($before.Rows|Where-Object {$_.Task -eq $old -and $_.Result -eq 'Можно удалить'})){throw 'Orphan without directory not identified'}
 if(!(Get-ScheduledTask $old -ErrorAction SilentlyContinue)){throw 'Preview changed tasks'}
 $result=Invoke-Probe $apply|ConvertFrom-Json
 if(Get-ScheduledTask $old -ErrorAction SilentlyContinue){throw 'Confirmed orphan was not removed'}
 foreach($keep in @($fresh,$foreign,$running)){if(!(Get-ScheduledTask $keep -ErrorAction SilentlyContinue)){throw 'Fresh, foreign or running task removed'}}
 if(!(Get-ScheduledTask 'DomainConsoleCSharp-Maintenance' -ErrorAction SilentlyContinue)){throw 'Maintenance removed'}
}finally{foreach($name in $created){Stop-ScheduledTask $name -ErrorAction SilentlyContinue;Unregister-ScheduledTask $name -Confirm:$false -ErrorAction SilentlyContinue}}
$until=[DateTime]::UtcNow.AddSeconds(65)
while(@(Get-Process wsmprovhost -ErrorAction SilentlyContinue).Count -gt $baseline -and [DateTime]::UtcNow -lt $until){Start-Sleep -Seconds 1}
if(@(Get-Process wsmprovhost -ErrorAction SilentlyContinue).Count -gt $baseline){throw 'WinRM host processes did not return to baseline'}
Write-Output 'Probe41: real SYSTEM collection, concurrent WinRM readers, caching, scheduler timeout, latched retry prevention, explicit resume and selective legacy cleanup passed.'
