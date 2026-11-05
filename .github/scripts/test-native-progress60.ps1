$ErrorActionPreference='Stop'
# Read-only scans on the disposable Windows runner, never on production clients.
foreach($item in @(@{Name='sfc';Code='sfc /verifyonly'},@{Name='dism';Code='dism /Online /Cleanup-Image /ScanHealth'})){
 $folder=Join-Path $env:RUNNER_TEMP ('DomainConsole-native60-'+$item.Name)
 New-Item $folder -ItemType Directory -Force|Out-Null
 @{Id=[guid]::NewGuid().ToString();Manual56=$true;OutputChannel='Commands';Steps=@(@{Name=$item.Name;Kind='PowerShell';Code=$item.Code})}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $folder 'job.json') -Encoding utf8
 $p=Start-Process ./Publish/Agent/DomainConsole.Agent.exe -ArgumentList @('run',('"'+$folder+'"')) -PassThru
 $until=(Get-Date).AddMinutes(10);$seen=$false;$seenPercent=$false
 try{
  while(!$p.HasExited -and (Get-Date) -lt $until){
   $out=Join-Path $folder 'step-1.out'
   if(Test-Path $out){try{$text=[IO.File]::ReadAllText($out);if($text.Length -gt 0){$seen=$true};if($text -match '\d+(?:\.\d+)?\s*%'){$seenPercent=$true}}catch [IO.IOException]{}}
   Start-Sleep -Milliseconds 100
  }
  if(!$p.HasExited){throw ($item.Name+': read-only scan timed out')}
  if(!$seen -or !$seenPercent){throw ($item.Name+': no live text/percentage before process exit')}
  $state=Get-Content (Join-Path $folder 'status.json') -Raw|ConvertFrom-Json
  if(!$state.Ended -or !(Test-Path (Join-Path $folder 'step-1.native.json'))){throw ($item.Name+': native execution/result not confirmed')}
  $final=[IO.File]::ReadAllText((Join-Path $folder 'step-1.out'))
  $percentLines=@($final -split '\r?\n'|Where-Object{$_ -match '^\s*(?:\[[=\-\s]*\d+(?:\.\d+)?\s*%[=\-\s]*\]|Verification\s+\d+%\s+complete\.?)\s*$'})
  if($percentLines.Count -gt 2){throw ($item.Name+': percentage flooded final output')}
  Write-Output ($item.Name+': live output and percentage observed before completion; compact progress; terminal status '+$state.Status+'; native exit '+$state.Results[0].NativeExitCode)
 }finally{if(!$p.HasExited){New-Item (Join-Path $folder 'force-stop.flag') -ItemType File -Force|Out-Null;$null=$p.WaitForExit(15000)}}
}
