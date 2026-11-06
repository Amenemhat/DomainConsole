param([Parameter(Mandatory=$true)][string]$Script)
$ErrorActionPreference='Stop'
# Production transmits the embedded Unicode script as a string, not an ANSI file.
$body=[scriptblock]::Create([IO.File]::ReadAllText($Script,[Text.Encoding]::UTF8))
$oldProgramData=$env:ProgramData
$fixture=Join-Path $env:TEMP ('control56-'+[guid]::NewGuid())
$env:ProgramData=$fixture
try {
 $root=Join-Path $fixture 'DomainConsoleCSharp'
 New-Item $root -ItemType Directory -Force|Out-Null
 $orphan=[guid]::NewGuid().ToString();$taskId=[guid]::NewGuid().ToString();$agentId=[guid]::NewGuid().ToString();$knownId=[guid]::NewGuid().ToString()
 $file=Join-Path $root 'control37.json'
 $control=@{Protocol=1;OwnerId='fixture-owner';OwnerLabel='Fixture';PlanId='fixture-plan';LastJobId=$orphan;UpdatedUtc=[datetime]::UtcNow.AddMinutes(-10).ToString('o');JobIds=@($orphan,$taskId,$agentId,$knownId)}
 $control|ConvertTo-Json -Depth 8|Set-Content $file -Encoding UTF8
 New-Item (Join-Path $root $knownId) -ItemType Directory|Out-Null
 '{}'|Set-Content (Join-Path (Join-Path $root $knownId) 'job.json') -Encoding UTF8
 function Get-ScheduledTask { [pscustomobject]@{TaskName="DomainConsoleCSharp-$taskId";State='Ready';Actions=@()} }
 function Get-CimInstance { param($ClassName,$Filter) if($ClassName -eq 'Win32_OperatingSystem'){return [pscustomobject]@{LastBootUpTime=(Get-Date).AddHours(-1)}}; [pscustomobject]@{ProcessId=1234;CommandLine="agent run $agentId";CreationDate=Get-Date} }
 $reply=(& $body)|ConvertFrom-Json
 if(@($reply.Removed).Count -ne 1 -or $reply.Removed[0] -ne $orphan){throw 'Repair removed wrong reservation'}
 $after=Get-Content $file -Raw|ConvertFrom-Json
 if(@($after.JobIds).Count -ne 3 -or $after.OwnerId -ne 'fixture-owner' -or $after.PlanId -ne 'fixture-plan'){throw 'Repair changed unrelated state'}
 if(!(Test-Path $reply.Backup)){throw 'Repair backup missing'}
 $again=(& $body)|ConvertFrom-Json
 if(@($again.Removed).Count){throw 'Repair is not idempotent'}

 $oldId=[guid]::NewGuid().ToString();$sameBootId=[guid]::NewGuid().ToString();$pendingId=[guid]::NewGuid().ToString()
 foreach($id in @($oldId,$sameBootId,$pendingId)){
  $folder=Join-Path $root $id;New-Item $folder -ItemType Directory|Out-Null
  '{}'|Set-Content (Join-Path $folder 'job.json') -Encoding UTF8
  $at=if($id -eq $sameBootId){[datetime]::UtcNow.AddMinutes(-10)}else{[datetime]::UtcNow.AddDays(-2)}
  @{Id=$id;Status='Running';Started=$at.ToString('o');Updated=$at.ToString('o');Ended='';Stage='Installation';RestoreStatus=$(if($id -eq $pendingId){'Pending'}else{'NotRequested'});DiagnosticStatus='NotRequested'}|ConvertTo-Json|Set-Content (Join-Path $folder 'status.json') -Encoding UTF8
 }
 $after.JobIds=@($oldId,$sameBootId,$pendingId);$after|ConvertTo-Json -Depth 8|Set-Content $file -Encoding UTF8
 function Get-ScheduledTask { [pscustomobject]@{TaskName="DomainConsoleCSharp-$oldId";State='Ready';Actions=@()} }
 $recovered=(& $body)|ConvertFrom-Json
 if(@($recovered.Removed).Count -ne 1 -or $recovered.Removed[0] -ne $oldId){throw 'Prior-boot reconciliation removed wrong jobs'}
 $old=Get-Content (Join-Path (Join-Path $root $oldId) 'status.json') -Raw|ConvertFrom-Json
 if($old.Status -ne 'InterventionRequired' -or !$old.Ended -or !$old.Error.StartsWith('DC_INTERRUPTED46:')){throw 'Interrupted outcome not preserved'}
 if(!@(Get-ChildItem (Join-Path $root $oldId) -Filter '*.bak').Count){throw 'Interrupted status backup missing'}
 'Control60 repair: orphan and prior-boot dead task released; same-boot, active and recovery-pending jobs retained; backups and idempotence verified.'
} finally {
 $env:ProgramData=$oldProgramData
 Remove-Item $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
