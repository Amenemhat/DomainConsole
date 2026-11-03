$ErrorActionPreference='Stop'
$fixture=Join-Path $env:RUNNER_TEMP ('executor54-'+[guid]::NewGuid().ToString('N'))
$env:ProgramData=$fixture
$id=[guid]::NewGuid().ToString();$mode='executor'
$p=Join-Path $fixture ('DomainConsoleCSharp/'+$id)
New-Item $p -ItemType Directory -Force|Out-Null
$exe=Join-Path $fixture 'DomainConsole.Agent.exe'
Add-Type -TypeDefinition 'public class Fixture54 {public static void Main(string[] args){System.Threading.Thread.Sleep(120000);}}' -OutputAssembly $exe -OutputType ConsoleApplication
$process=Start-Process $exe -ArgumentList ('run "'+$p+'"') -PassThru
$sentinel=Start-Process $exe -ArgumentList 'run "unrelated-fixture"' -PassThru
$script=[IO.File]::ReadAllText((Join-Path $env:RUNNER_TEMP 'DomainConsole-smoke/cancel-executor54.ps1'))
$task54=[pscustomobject]@{State='Running';Actions=@([pscustomobject]@{Execute=$exe;Arguments=('run "'+$p+'"')})}
function Get-ScheduledTask {param($TaskName) $script:task54}
function Disable-ScheduledTask {param($InputObject) $script:disabled54=$true}
try {
 @{Steps=@(@{Kind='UpdateScan'})}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $p 'job.json') -Encoding UTF8
 $state=@{Status='Running';Ended='';HasUpdateSteps=$true;Step=1;RestoreStatus='NotRequested';ExecutorPid37=$process.Id;ExecutorStarted37=$process.StartTime.ToUniversalTime().AddMinutes(-1).ToString('o');Error='';Results=@(@{Name='preserve';ExitCode=0})}
 $status=Join-Path $p 'status.json';$flag=Join-Path $p 'stop-now.flag';New-Item $flag -ItemType File|Out-Null
 $state|ConvertTo-Json -Depth 10|Set-Content $status -Encoding UTF8
 $rejected=$false;try{& ([scriptblock]::Create($script))}catch{$rejected=$_.Exception.Message -match '2 минуты'}
 if(!$rejected -or $process.HasExited){throw 'Premature executor termination not rejected'}
 (Get-Item $flag).CreationTimeUtc=[DateTime]::UtcNow.AddMinutes(-3)
 $rejected=$false;try{& ([scriptblock]::Create($script))}catch{$rejected=$_.Exception.Message -match 'PID уже'}
 if(!$rejected -or $process.HasExited){throw 'PID/start-time mismatch not rejected'}
 $state.ExecutorStarted37=$process.StartTime.ToUniversalTime().ToString('o');$state|ConvertTo-Json -Depth 10|Set-Content $status -Encoding UTF8
 $result=& ([scriptblock]::Create($script))|ConvertFrom-Json
 if(!$process.WaitForExit(5000) -or $result.Status -ne 'InterventionRequired' -or !$script:disabled54){throw 'Owned executor not confirmed stopped'}
 $saved=Get-Content $status -Raw|ConvertFrom-Json
 if($saved.Results.Count -ne 1 -or $sentinel.HasExited){throw 'Partial result or unrelated executor lost'}
 'PASS: early stop rejected; reused PID rejected; exact owned executor stopped; partial results and unrelated process preserved.'
}finally{
 foreach($owned in @($process,$sentinel)){if(!$owned.HasExited){$owned.Kill();$null=$owned.WaitForExit(5000)};$owned.Dispose()}
 Remove-Item $fixture -Recurse -Force
}
