param([Parameter(Mandatory=$true)][string]$Script)
$ErrorActionPreference='Stop'
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
 function Get-CimInstance { param($Filter,$ClassName) [pscustomobject]@{ProcessId=1234;CommandLine="agent run $agentId";CreationDate=Get-Date} }
 $reply=(& $Script)|ConvertFrom-Json
 if(@($reply.Removed).Count -ne 1 -or $reply.Removed[0] -ne $orphan){throw 'Repair removed wrong reservation'}
 $after=Get-Content $file -Raw|ConvertFrom-Json
 if(@($after.JobIds).Count -ne 3 -or $after.OwnerId -ne 'fixture-owner' -or $after.PlanId -ne 'fixture-plan'){throw 'Repair changed unrelated state'}
 if(!(Test-Path $reply.Backup)){throw 'Repair backup missing'}
 $again=(& $Script)|ConvertFrom-Json
 if(@($again.Removed).Count){throw 'Repair is not idempotent'}
 'Control56 repair: orphan removed; scheduled, running and known jobs retained; backup and idempotence verified.'
} finally {
 $env:ProgramData=$oldProgramData
 Remove-Item $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
