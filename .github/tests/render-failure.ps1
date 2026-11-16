$ErrorActionPreference = 'Stop'
$folder=Join-Path $env:RUNNER_TEMP 'DomainConsole-render-test'
New-Item $folder -ItemType Directory -Force | Out-Null
Copy-Item Publish/* $folder -Recurse
$env:DOMAINCONSOLE_RENDER_TEST='1'
$info=[Diagnostics.ProcessStartInfo]::new((Join-Path $folder 'DomainConsole.exe'),'--test-render-failure --software-rendering')
$info.WorkingDirectory=$folder
$info.UseShellExecute=$false
$p=[Diagnostics.Process]::Start($info)
try {
    $exited=$p.WaitForExit(45000)
    $trace=@(Get-Content (Join-Path $folder 'render-test.txt') -ErrorAction SilentlyContinue)
    Write-Output $trace
    $log=Get-Item (Join-Path $folder 'Data/Diagnostics/last-render-failure.txt')
    if (!$exited -or $p.ExitCode -ne 1 -or @($trace | Where-Object {$_ -eq 'activated Domain Console'}).Count -ne 1){throw "Render notice or shutdown failed. Exit=$exited; code=$($p.ExitCode)"}
    if ($trace -notcontains 'startup SoftwareOnly' -or $log.Length -gt 131072 -or !(Select-String -Path $log.FullName -Pattern 'UCEERR_RENDERTHREADFAILURE' -Quiet)){throw 'Missing bounded renderer diagnostic or software mode'}
} finally {
    if (!$p.HasExited){$p.Kill()}
    Remove-Item Env:DOMAINCONSOLE_RENDER_TEST
}
