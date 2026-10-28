param([Parameter(Mandatory)][string]$Template,[Parameter(Mandatory)][string]$Root)
$ErrorActionPreference='Stop'
New-Item $Root -ItemType Directory -Force|Out-Null
$root=[IO.Path]::GetFullPath($Root)
$hash47='a'*64
$oldExe47=Join-Path (Join-Path (Join-Path $root 'Agent') $hash47) 'DomainConsole.Agent.exe'
$exe=Join-Path (Join-Path (Join-Path $root 'Agent') ('b'*64)) 'DomainConsole.Agent.exe'
$maintenance='incoming action';$principal='incoming principal';$settings='incoming settings'
$script:installed47=@();$script:previous47='0.46.0.0';$script:incoming47='0.47.0.0'
function Get-ScheduledTask {param($TaskName,$ErrorAction);$script:task47}
function Get-FileHash {param($LiteralPath,$Algorithm);[pscustomobject]@{Hash=$script:checksum47}}
function Get-Item {param($LiteralPath);[pscustomobject]@{VersionInfo=[pscustomobject]@{FileVersion=$(if($LiteralPath -eq $exe){$script:incoming47}else{$script:previous47})}}}
function New-ScheduledTaskTrigger {param([switch]$Daily,$At);'daily trigger'}
function Register-ScheduledTask {param($TaskName,$Action,$Trigger,$Principal,$Settings,[switch]$Force);$script:installed47+=@{Name=$TaskName;Action=$Action;Principal=$Principal;Settings=$Settings}}
$code=[IO.File]::ReadAllText($Template)
foreach($case47 in @('Missing','Upgrade','Equal','Newer','Running','Queued','ForeignPath','ForeignArgs','ForeignPrincipal','BadHash','LockBusy')){
 $script:installed47=@();$script:checksum47=$hash47;$script:previous47='0.46.0.0';$guard47=$null;$error47=$null
 $script:task47=[pscustomobject]@{State='Ready';Actions=@([pscustomobject]@{Execute=$oldExe47;Arguments=('cleanup "'+$root+'"')});Principal=[pscustomobject]@{UserId='S-1-5-18'}}
 switch($case47){
  'Missing'{$script:task47=$null}
  'Equal'{$script:previous47='0.47.0.0'}
  'Newer'{$script:previous47='0.48.0.0'}
  'Running'{$script:task47.State='Running'}
  'Queued'{$script:task47.State='Queued'}
  'ForeignPath'{$script:task47.Actions[0].Execute=Join-Path $root 'other.exe'}
  'ForeignArgs'{$script:task47.Actions[0].Arguments='run other'}
  'ForeignPrincipal'{$script:task47.Principal.UserId='S-1-5-19'}
  'BadHash'{$script:checksum47='c'*64}
  'LockBusy'{$guard47=[IO.File]::Open((Join-Path $root 'maintenance47.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
 }
 try{& ([ScriptBlock]::Create($code))}catch{$error47=$_}finally{if($guard47){$guard47.Dispose()}}
 $expected47=if($case47 -in @('Missing','Upgrade')){1}else{0}
 if($script:installed47.Count -ne $expected47){throw "Incorrect registration count: $case47"}
 if($case47 -in @('ForeignPath','ForeignArgs','ForeignPrincipal','BadHash','LockBusy')){if(!$error47){throw "Unconfirmed ownership/lock was accepted: $case47"}}elseif($error47){throw $error47}
 if($expected47 -eq 1 -and ($script:installed47[0].Action -ne $maintenance -or $script:installed47[0].Name -ne 'DomainConsoleCSharp-Maintenance')){throw 'Unexpected maintenance target'}
 # Every branch must close the actual filesystem gate.
 $released47=[IO.File]::Open((Join-Path $root 'maintenance47.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$released47.Dispose()
 Write-Output "Maintenance47 $case47 passed"
}
