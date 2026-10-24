param([Parameter(Mandatory)]$Session,[Parameter(Mandatory)][pscredential]$Credential,[Parameter(Mandatory)][string]$Fixture)
$ErrorActionPreference='Stop'
$task='DomainConsoleCSharp-Snapshot41';$path=Join-Path $env:ProgramData 'DomainConsoleCSharp\Snapshot41'
function Code($mode){[IO.File]::ReadAllText((Join-Path $Fixture ('probe42-'+$mode+'.ps1')))}
function Invoke-Probe([string]$body){Invoke-Command -Session $Session -ScriptBlock {param($text);& ([ScriptBlock]::Create($text))} -ArgumentList $body -ErrorAction Stop}
function Query($mode){Invoke-Probe (Code $mode)|ConvertFrom-Json}
function Await-Result { $until=[DateTime]::UtcNow.AddSeconds(100);do{$r=Query 'Read';if($r.Status -notin @('Busy','Stopping')){return $r};Start-Sleep -Seconds 2}while([DateTime]::UtcNow -lt $until);throw 'Collector did not settle' }
function Expire-Cache {$f=Join-Path $path 'result.json';if(Test-Path $f){$v=Get-Content $f -Raw|ConvertFrom-Json;$v.CollectedUtc=[DateTime]::UtcNow.AddMinutes(-2).ToString('o');$v|ConvertTo-Json -Depth 28|Set-Content $f -Encoding UTF8}}
function Admit-Next {Expire-Cache;$f=Join-Path $path 'run.json';if(Test-Path $f){$v=Get-Content $f -Raw|ConvertFrom-Json;$v.NextUtc=[DateTime]::UtcNow.AddSeconds(-1).ToString('o');$v|ConvertTo-Json -Depth 8|Set-Content $f -Encoding UTF8}}
# Parse every generated script before any execution.
foreach($f in Get-ChildItem $Fixture -Filter 'probe42-*.ps1'){$tokens=$null;$errors=$null;$null=[Management.Automation.Language.Parser]::ParseFile($f.FullName,[ref]$tokens,[ref]$errors);if($errors){throw ($f.Name+': '+($errors|Out-String))}}
$first=Query 'Get';if($first.Status -ne 'Busy'){throw ('Start failed: '+($first|ConvertTo-Json -Depth 5))}
$result=Await-Result;if($result.Status -ne 'Ready' -or !$result.Snapshot.Reboot.CollectedUtc -or !$result.Snapshot.Activity.CollectedUtc){throw ('No SYSTEM snapshot: '+($result|ConvertTo-Json -Depth 6))}
if([xml.xmlconvert]::ToTimeSpan((Get-ScheduledTask $task).Settings.ExecutionTimeLimit).TotalSeconds -ne 60){throw 'Legacy task was not safely migrated'}
$registered=(Get-ScheduledTaskInfo $task).LastRunTime
foreach($n in 1..12){if((Query 'Get').Status -ne 'Ready'){throw 'Cache lost'}}
$jobs=@();try{foreach($n in 1..6){$jobs+=Invoke-Command -ComputerName localhost -Credential $Credential -Authentication Negotiate -AsJob -ScriptBlock {param($text);& ([ScriptBlock]::Create($text))} -ArgumentList (Code 'Get')};$jobs|Wait-Job -Timeout 40|Out-Null;foreach($j in $jobs){if($j.State -ne 'Completed'){throw 'Concurrent request failed'};$r=Receive-Job $j -ErrorAction Stop|ConvertFrom-Json;if($r.Status -notin @('Ready','Busy')){throw 'Busy became persistent block'}}}finally{$jobs|Remove-Job -Force -ErrorAction SilentlyContinue}
if((Get-ScheduledTaskInfo $task).LastRunTime -ne $registered){throw 'Cache/concurrency restarted collector'}
# Failure reason, bounded cooldown, three errors then explicit recovery.
foreach($n in 1..3){Admit-Next;$start=Invoke-Probe (Code 'error')|ConvertFrom-Json;if($start.Status -ne 'Busy'){throw 'Fixture start rejected'};$r=Await-Result;if($r.Status -ne 'Ready' -or $r.Snapshot.Complete -or $r.Snapshot.Error -notlike '*FIXTURE_ACTIVITY_ERROR42*' -or !$r.Snapshot.Reboot.CollectedUtc){throw 'Partial result/reason lost'};Expire-Cache;$state=Query 'Get';$expected=if($n -lt 3){'Cooldown'}else{'Blocked'};if($state.Status -ne $expected){throw "Expected $expected, got $($state.Status)"};$last=(Get-ScheduledTaskInfo $task).LastRunTime;foreach($repeat in 1..4){$null=Query 'Get'};if((Get-ScheduledTaskInfo $task).LastRunTime -ne $last){throw 'Cooldown restarted collector'}}
if((Query 'Recover').Status -ne 'Recovered'){throw 'Verified recovery failed'}
# Non-cooperating read worker is bounded, its sibling result retained.
Admit-Next;$null=Invoke-Probe (Code 'slow');$r=Await-Result;if($r.Status -ne 'Ready' -or $r.Snapshot.Complete -or !$r.Snapshot.Reboot.CollectedUtc){throw 'Hung optional source destroyed reboot result'}
$left=@(Get-CimInstance Win32_Process -Filter "Name='powershell.exe'"|Where-Object {$_.CommandLine -like ('*'+$path+'\Activity.ps1*')});if($left.Count){throw 'Read worker survived supervision'}
$null=Query 'Recover';Admit-Next
# Start without any waiting console. Task Scheduler must stop the supervisor.
$null=Invoke-Probe (Code 'autonomous');Start-Sleep -Seconds 64
if((Get-ScheduledTask $task).State -in @('Running','Queued')){throw 'Autonomous timeout failed'}
$r=Query 'Read';if($r.Status -ne 'Cooldown'){throw 'Unknown result blindly restarted'}
$null=Query 'Recover';Admit-Next;$null=Query 'Get';if((Await-Result).Status -ne 'Ready'){throw 'Recovery did not collect'}
# Existing legacy ownership/cleanup fixture remains exercised separately below.
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
 $date=$xml.Task.RegistrationInfo.SelectSingleNode('*[local-name()="Date"]');if(!$date){$date=$xml.CreateElement('Date',$xml.DocumentElement.NamespaceURI);$null=$xml.Task.RegistrationInfo.AppendChild($date)};$date.InnerText=[DateTime]::UtcNow.AddMinutes($(if($Old){-20}else{0})).ToString('o')
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
Write-Output 'Probe42: real SYSTEM single-flight/cache, six WinRM readers, partial failures, cooldown/circuit breaker, non-cooperating worker termination, autonomous scheduler deadline, verified recovery and legacy cleanup passed.'
