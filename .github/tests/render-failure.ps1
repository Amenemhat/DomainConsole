$ErrorActionPreference='Stop'
$env:DOMAINCONSOLE_RENDER_TEST='1'
try {
 foreach($case in @('hardware','software','denied','unanswered')){
  $writable=$case -ne 'denied'
  $software=$case -ne 'hardware'
  if($case -eq 'unanswered'){$env:DOMAINCONSOLE_TEST_UNANSWERED_DIALOG='1'}else{Remove-Item Env:DOMAINCONSOLE_TEST_UNANSWERED_DIALOG -ErrorAction SilentlyContinue}
  $folder=Join-Path $env:RUNNER_TEMP ("DomainConsole-render-test-"+$case)
  New-Item $folder -ItemType Directory -Force | Out-Null
  Copy-Item Publish/* $folder -Recurse
  $data=Join-Path $folder 'Data'
  New-Item $data -ItemType Directory -Force | Out-Null
  $diag=Join-Path $data 'Diagnostics'
  if(!$writable){ Set-Content $diag 'This fixture blocks directory creation.' }
  $history=Join-Path $data 'history.json'
  if(!(Test-Path $history)){Set-Content $history '[]'}
  $before=(Get-FileHash $history).Hash
  $argsForTest='--test-render-failure';if($software){$argsForTest+=' --software-rendering'}
  $info=[Diagnostics.ProcessStartInfo]::new((Join-Path $folder 'DomainConsole.exe'),$argsForTest)
  $info.WorkingDirectory=$folder
  $info.UseShellExecute=$false
  $p=[Diagnostics.Process]::Start($info)
  try {
   $exited=$p.WaitForExit(65000)
   $trace=@(Get-Content (Join-Path $folder 'render-test.txt') -ErrorAction SilentlyContinue)
   if(!$exited -or $p.ExitCode -ne 1 -or @($trace|Where-Object {$_ -eq 'activated Domain Console'}).Count -ne 1){throw "Render notice or shutdown failed. Exit=$exited, code=$($p.ExitCode)"}
   if(($software -and $trace -notcontains 'startup SoftwareOnly') -or (!$software -and $trace -contains 'startup SoftwareOnly') -or (Get-FileHash $history).Hash -ne $before){throw 'Renderer recovery changed history or the requested rendering mode'}
   if($writable){
    $log=Get-Item (Join-Path $diag 'last-render-failure.txt')
    foreach($field in @('HRESULT 0x88980406','Session=','WorkingSet=','DomainConsole ','.NET')){if(!(Select-String -SimpleMatch -Path $log.FullName -Pattern $field -Quiet)){throw "Missing diagnostic field $field"}}
    if($log.Length -gt 131072 -or !(Select-String -Path $log.FullName -Pattern 'UCEERR_RENDERTHREADFAILURE' -Quiet)){throw 'Missing bounded renderer diagnostic'}
   }
   Write-Output "PASS one notification and controlled shutdown, case=$case, diagnostic directory writable=$writable"
  }finally{if(!$p.HasExited){$p.Kill()};$p.Dispose()}
 }
} finally{Remove-Item Env:DOMAINCONSOLE_RENDER_TEST;Remove-Item Env:DOMAINCONSOLE_TEST_UNANSWERED_DIALOG -ErrorAction SilentlyContinue}
