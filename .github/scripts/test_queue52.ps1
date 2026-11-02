param([Parameter(Mandatory=$true)][string]$Fixture)
$ErrorActionPreference='Stop'
$path=Join-Path $Fixture 'report-queue-verify52.ps1'
$tokens=$null;$errors=$null
$null=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$errors)
if($errors.Count){throw ($errors|Out-String)}
$text=[IO.File]::ReadAllText($path)
$offset=$text.IndexOf('$q=$raw|ConvertFrom-Json',[StringComparison]::Ordinal)
if($offset -lt 0){throw 'Queue verification boundary missing'}
$verify=[ScriptBlock]::Create($text.Substring($offset))
foreach($mode in @('complete','empty','partial','duplicate','negative','error')){
 $queues=@(@{Name='EventCache';Files=2;Bytes=30},@{Name='EventCache.v2';Files=1;Bytes=20})
 $errorText=''
 switch($mode){
  'empty' {$queues|ForEach-Object {$_.Files=0;$_.Bytes=0}}
  'partial' {$queues=@($queues[0])}
  'duplicate' {$queues[1].Name='EventCache'}
  'negative' {$queues[1].Bytes=-1}
  'error' {$errorText='Access denied fixture'}
 }
 $raw=@{Queues=$queues;QueueError=$errorText}|ConvertTo-Json -Depth 5 -Compress
 $failed=$false
 try{$output=& $verify}catch{$failed=$true}
 $expected=$mode -notin @('complete','empty')
 if($failed -ne $expected){throw "Incorrect queue verification outcome: $mode"}
 if(!$failed -and ($output -join ' ') -notmatch 'EventCache.v2'){throw 'Queue measurements absent'}
 Write-Output "Queue52 $mode PASS"
}

# Validate the actual MpCmdRun result handler without invoking Defender or changing services.
$defender=[IO.File]::ReadAllText((Join-Path $Fixture 'defender-recover.ps1'))
$start=$defender.IndexOf("`$hex='0x'",[StringComparison]::Ordinal)
$end=$defender.IndexOf('Get-Service WinDefend',[StringComparison]::Ordinal)
if($start -lt 0 -or $end -le $start){throw 'Defender result handler missing'}
$handler=[ScriptBlock]::Create($defender.Substring($start,$end-$start))
function Test-Path {param($Path) return $false}
try {
 foreach($code in @(0,-2147416390,-2147023174)) {
  $failure=$null
  try {$lines=@(& $handler)} catch {$failure=$_.Exception.Message}
  if($code -eq 0 -and $failure){throw 'Successful MpCmdRun classified as failure'}
  if($code -ne 0 -and (!$failure -or !$failure.Contains([string]$code))){throw 'Original Defender error lost'}
  if($code -eq -2147416390 -and (!$failure.Contains('0x800106BA') -or !$failure.Contains('WinDefend'))){throw 'Defender service error lost hex/context'}
  if($code -eq -2147023174 -and (!$failure.Contains('0x800706BA') -or $failure.Contains('WinDefend'))){throw 'RPC error confused with Defender error'}
  Write-Output "Defender52 $code PASS"
 }
} finally {Remove-Item Function:\Test-Path}
