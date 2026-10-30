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
