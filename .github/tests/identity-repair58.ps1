param([Parameter(Mandatory=$true)][string]$ScriptFolder)
$ErrorActionPreference='Stop'
$template=[IO.File]::ReadAllText((Join-Path $ScriptFolder 'identity-repair58.ps1'))
$template=$template.Replace('$installer58=New-Object -ComObject Microsoft.Update.Installer','$installer58=$fixtureInstaller58')
$template=$template.Replace('$session58=New-Object -ComObject Microsoft.Update.Session','$session58=$fixtureSession58')
$template=$template.Replace("& reg.exe export 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate' $"+'backup58 /y | Out-Null','Export-Fixture58 $backup58')
$template=$template.Replace('& "$env:windir\System32\wuauclt.exe" /resetauthorization /detectnow',"Invoke-Fixture58 'Authorize'")
$template=$template.Replace('& "$env:windir\System32\wuauclt.exe" /reportnow',"Invoke-Fixture58 'Report'")
# Refuse to execute if any system-changing native command escaped the substitutions.
if($template -match '& reg.exe|& "\$env:windir|New-Object -ComObject'){throw 'Fixture isolation incomplete'}
$prefix=@'
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$env:COMPUTERNAME='fixture'
$env:ProgramData=$PSScriptRoot
$global:trace58=New-Object 'System.Collections.Generic.List[string]'
$global:id58='22222222-2222-2222-2222-222222222222'
if($env:CASE58 -eq 'already-changed'){$global:id58='33333333-3333-3333-3333-333333333333'}
$global:service58='Running'
function Invoke-Fixture58 {param($Name) $global:trace58.Add($Name)}
function Get-CimInstance {param($ClassName,$ErrorAction)
 if($ClassName -eq 'Win32_ComputerSystem'){return [pscustomobject]@{PartOfDomain=$true;Domain=$(if($env:CASE58 -eq 'wrong-name'){'other.invalid'}else{'invalid'})}}
 if($ClassName -eq 'Win32_ComputerSystemProduct'){return [pscustomobject]@{UUID=$(if($env:CASE58 -eq 'wrong-machine'){'44444444-4444-4444-4444-444444444444'}else{'11111111-1111-1111-1111-111111111111'})}}
 throw 'Unexpected CIM request'
}
function Get-ItemProperty {param($Path,$ErrorAction)
 if($Path -like '*Policies*'){return [pscustomobject]@{WUServer=$(if($env:CASE58 -eq 'wrong-server'){'http://other.invalid:8530'}else{'http://wsus.invalid:8530'})}}
 return [pscustomobject]@{SusClientId=$global:id58;SusClientIdValidation='fixture'}
}
function Export-Fixture58 {param($Path) Invoke-Fixture58 'Backup';$global:LASTEXITCODE=0;if($env:CASE58 -eq 'backup-fails'){$global:LASTEXITCODE=5}else{[IO.File]::WriteAllText($Path,'fixture registry backup')}}
function Stop-Service {param($Name,$ErrorAction) if($Name -ne 'wuauserv'){throw 'Unexpected service'};Invoke-Fixture58 'Stop';if($env:CASE58 -in @('stop-fails','stop-and-start-fail')){throw 'STOP_FIXTURE_FAILURE'};$global:service58='Stopped'}
function Start-Service {param($Name,$ErrorAction) if($Name -ne 'wuauserv'){throw 'Unexpected service'};Invoke-Fixture58 'Start';if($env:CASE58 -eq 'stop-and-start-fail'){throw 'START_FIXTURE_FAILURE'};$global:service58='Running';if(!$global:id58){$global:id58='33333333-3333-3333-3333-333333333333'}}
function Get-Service {param($Name) $s=[pscustomobject]@{};$s|Add-Member ScriptMethod WaitForStatus {param($status,$timeout)};return $s}
function Remove-ItemProperty {param($Path,$Name,$ErrorAction) Invoke-Fixture58 ('Remove-'+$Name);if($env:CASE58 -eq 'remove-fails'){throw 'REMOVE_FIXTURE_FAILURE'};if($Name -eq 'SusClientId'){$global:id58=''};if($env:CASE58 -eq 'after-delete' -and $Name -eq 'SusClientId'){throw 'INTERRUPTED_AFTER_DELETE'}}
$fixtureInstaller58=[pscustomobject]@{IsBusy=($env:CASE58 -eq 'busy')}
$fixtureSearch58=[pscustomobject]@{ServerSelection=0;Online=$false}
$fixtureSearch58|Add-Member ScriptMethod Search {param($criteria) Invoke-Fixture58 'Search';if($this.ServerSelection -ne 1 -or !$this.Online){throw 'Search source incorrect'};[pscustomobject]@{ResultCode=$(if($env:CASE58 -eq 'search-fails'){4}else{2})}}
$fixtureSession58=[pscustomobject]@{ClientApplicationID=''}
$fixtureSession58|Add-Member ScriptMethod CreateUpdateSearcher {$fixtureSearch58}
'@
$root=Join-Path $env:RUNNER_TEMP ('identity58-'+[guid]::NewGuid().ToString('N'))
New-Item $root -ItemType Directory|Out-Null
foreach($case in @('wrong-name','wrong-machine','wrong-server','busy','already-changed','backup-fails','stop-fails','stop-and-start-fail','remove-fails','after-delete','search-fails','success')){
 $folder=Join-Path $root $case;New-Item $folder -ItemType Directory|Out-Null
 $file=Join-Path $folder 'case.ps1';$state=Join-Path $folder 'state.json'
 $suffix=@'
}finally{
 [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'state.json'),(@{Trace=@($global:trace58);Id=$global:id58;Service=$global:service58}|ConvertTo-Json -Depth 4))
}
'@
 [IO.File]::WriteAllText($file,$prefix+"`ntry {`n"+$template+"`n"+$suffix,(New-Object Text.UTF8Encoding($true)))
 $p=New-Object Diagnostics.ProcessStartInfo
 $p.FileName="$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
 $p.Arguments='-NoProfile -NonInteractive -File "'+$file+'"'
 $p.UseShellExecute=$false;$p.RedirectStandardOutput=$true;$p.RedirectStandardError=$true
 $p.StandardOutputEncoding=[Text.Encoding]::UTF8;$p.StandardErrorEncoding=[Text.Encoding]::UTF8
 $p.EnvironmentVariables['CASE58']=$case
 $process=[Diagnostics.Process]::Start($p)
 $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 if(!$process.WaitForExit(20000)){$process.Kill();throw "Fixture timed out: $case"}
 $output=$stdout.Result+"`n"+$stderr.Result
 $data=Get-Content $state -Raw|ConvertFrom-Json
 $expectedSuccess=$case -in @('already-changed','success')
 if(($process.ExitCode -eq 0) -ne $expectedSuccess){throw "Unexpected result for ${case}: $output"}
 if($case -in @('wrong-name','wrong-machine','wrong-server','busy','already-changed') -and $data.Trace.Count -ne 0){throw "Guard changed state: $case"}
 if($case -eq 'backup-fails' -and ($data.Trace -contains 'Stop')){throw 'Backup failure reached service stop'}
 if($case -in @('stop-fails','remove-fails','after-delete') -and ($data.Service -ne 'Running' -or $data.Trace -notcontains 'Start')){throw "Service not restored: $case"}
 if($case -eq 'stop-and-start-fail' -and ($output -notmatch 'STOP_FIXTURE_FAILURE' -or $output -notmatch 'START_FIXTURE_FAILURE')){throw 'Primary or restoration failure was lost'}
 if($case -eq 'success' -and (($data.Trace -join ',') -ne 'Backup,Stop,Remove-SusClientId,Remove-SusClientIdValidation,Start,Authorize,Search,Report' -or $data.Id -ne '33333333-3333-3333-3333-333333333333')){throw 'Successful repair did not follow expected order'}
 if($case -eq 'search-fails' -and $data.Id -ne '33333333-3333-3333-3333-333333333333'){throw 'Partial repair lost new identity'}
 Write-Output "PASS identity repair: $case"
 $process.Dispose()
}
