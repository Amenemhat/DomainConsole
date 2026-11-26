param([Parameter(Mandatory=$true)][string]$ScriptFolder)
$ErrorActionPreference='Stop'
$template=[IO.File]::ReadAllText((Join-Path $ScriptFolder 'identity-repair58.ps1'))
$installationFixture='55555555-5555-5555-5555-555555555555'
$template=('$dcExpectedInstallation58='''+$installationFixture+''';$dcExpectedStatusServer58=''http://wsus.invalid:8530'';')+$template.Replace('$dcExpectedInstallation58='''';','').Replace('$dcExpectedStatusServer58='''';','')
$template=$template.Replace('$installer58=New-Object -ComObject Microsoft.Update.Installer','$installer58=$fixtureInstaller58')
$template=$template.Replace("& reg.exe export 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate' $"+'backup58 /y | Out-Null','Export-Fixture58 $backup58')
$template=$template.Replace('& "$env:windir\System32\wuauclt.exe" /resetauthorization /detectnow','Request-FixtureRegistration58')
$template=$template.Replace('& "$env:windir\System32\wuauclt.exe" /reportnow','Report-FixtureRegistration58')
$template=$template.Replace('Start-Sleep -Seconds 1','Start-Sleep -Milliseconds 1')
# Refuse to execute if any system-changing native command escaped the substitutions.
if($template -match '& reg.exe|& "\$env:windir|New-Object -ComObject'){throw 'Fixture isolation incomplete'}
$prefix=@'
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$env:COMPUTERNAME='fixture'
$env:ProgramData=$PSScriptRoot
$global:trace58=New-Object 'System.Collections.Generic.List[string]'
$global:fixtureId58='22222222-2222-2222-2222-222222222222'
if($env:CASE58 -eq 'already-changed'){$global:fixtureId58='33333333-3333-3333-3333-333333333333'}
if($env:CASE58 -eq 'invalid-id'){$global:fixtureId58='00000000-0000-0000-0000-000000000000'}
$global:service58='Running'
$global:validation58=[byte[]]@(1,2,3)
$global:waitReads58=0
function Invoke-Fixture58 {param($Name) $global:trace58.Add($Name)}
function Start-Sleep {param($Milliseconds) $global:waitReads58++;if($env:CASE58 -eq 'delayed-registration' -and $global:waitReads58 -eq 60){$global:fixtureId58='33333333-3333-3333-3333-333333333333'}}
function Get-CimInstance {param($ClassName,$ErrorAction,$Filter)
 if($ClassName -eq 'Win32_ComputerSystem'){return [pscustomobject]@{PartOfDomain=$true;Domain=$(if($env:CASE58 -eq 'wrong-name'){'other.invalid'}else{'invalid'})}}
 if($ClassName -eq 'Win32_ComputerSystemProduct'){return [pscustomobject]@{UUID=$(if($env:CASE58 -eq 'wrong-machine'){'44444444-4444-4444-4444-444444444444'}else{'11111111-1111-1111-1111-111111111111'})}}
 if($ClassName -eq 'Win32_Process'){if($env:CASE58 -eq 'installer-process'){[pscustomobject]@{Name='TiWorker.exe'}};return}
 throw 'Unexpected CIM request'
}
function Import-Module {param($Name,$ErrorAction) if($Name -ne 'BitsTransfer'){throw 'Unexpected module'}}
function Get-BitsTransfer {param([switch]$AllUsers,$ErrorAction)
 if(!$AllUsers){throw 'Expected all-user BITS observation'}
 if($env:CASE58 -eq 'bits-error'){throw 'BITS_FIXTURE_FAILURE'}
 if($env:CASE58 -eq 'bits-active'){[pscustomobject]@{DisplayName='WU Client Download';JobState='Queued'}}
 if($env:CASE58 -eq 'success'){[pscustomobject]@{DisplayName='Other app';JobState='Transferring'}}
}
function Get-ItemProperty {param($Path,$ErrorAction,$Name)
 if($Name -eq 'MachineGuid'){return [pscustomobject]@{MachineGuid=$(if($env:CASE58 -eq 'wrong-installation'){'66666666-6666-6666-6666-666666666666'}else{'55555555-5555-5555-5555-555555555555'})}}
 if($Path -like '*Policies*'){return [pscustomobject]@{UseWUServer=$(if($env:CASE58 -eq 'wrong-policy'){0}else{1});WUServer=$(if($env:CASE58 -eq 'wrong-server'){'http://other.invalid:8530'}else{'http://wsus.invalid:8530'});WUStatusServer=$(if($env:CASE58 -eq 'wrong-report-server'){'http://other.invalid:8530'}else{'http://wsus.invalid:8530'})}}
 return [pscustomobject]@{SusClientId=$global:fixtureId58;SusClientIdValidation=$global:validation58}
}
function Export-Fixture58 {param($Path) Invoke-Fixture58 'Backup';$global:LASTEXITCODE=0;if($env:CASE58 -eq 'backup-fails'){$global:LASTEXITCODE=5}else{[IO.File]::WriteAllText($Path,'fixture registry backup')}}
function Stop-Service {param($Name,$ErrorAction) if($Name -ne 'wuauserv'){throw 'Unexpected service'};Invoke-Fixture58 'Stop';if($env:CASE58 -in @('stop-fails','stop-and-start-fail')){throw 'STOP_FIXTURE_FAILURE'};$global:service58='Stopped';if($env:CASE58 -eq 'id-changed-before-removal'){$global:fixtureId58='33333333-3333-3333-3333-333333333333';$global:validation58=[byte[]]@(4,5)}}
function Start-Service {param($Name,$ErrorAction) if($Name -ne 'wuauserv'){throw 'Unexpected service'};Invoke-Fixture58 'Start';if($env:CASE58 -eq 'stop-and-start-fail'){throw 'START_FIXTURE_FAILURE'};$global:service58='Running';if(!$global:fixtureId58 -and $env:CASE58 -in @('after-delete','native-registers')){$global:fixtureId58='33333333-3333-3333-3333-333333333333'}}
function Get-Service {param($Name) $s=[pscustomobject]@{};$s|Add-Member ScriptMethod WaitForStatus {param($status,$timeout)};return $s}
function Remove-ItemProperty {param($Path,$Name,$ErrorAction) Invoke-Fixture58 ('Remove-'+$Name);if($env:CASE58 -eq 'remove-fails'){throw 'REMOVE_FIXTURE_FAILURE'};if($Name -eq 'SusClientId'){$global:fixtureId58=''}else{$global:validation58=$null};if($env:CASE58 -eq 'after-delete' -and $Name -eq 'SusClientId'){throw 'INTERRUPTED_AFTER_DELETE'}}
function New-ItemProperty {throw 'Unexpected write of a WSUS registration value'}
function Request-FixtureRegistration58 {
 Invoke-Fixture58 'RequestRegistration'
 if($env:CASE58 -eq 'request-fails'){throw 'DETECT_FIXTURE_FAILURE'}
 if($env:CASE58 -eq 'success'){$global:fixtureId58='33333333-3333-3333-3333-333333333333'}
}
function Report-FixtureRegistration58 {
 if($global:fixtureId58 -ne '33333333-3333-3333-3333-333333333333'){throw 'A report was requested without a new ID'}
 Invoke-Fixture58 'Report'
}
$fixtureInstaller58=[pscustomobject]@{IsBusy=($env:CASE58 -eq 'busy')}
'@
$root=Join-Path $env:RUNNER_TEMP ('identity58-'+[guid]::NewGuid().ToString('N'))
New-Item $root -ItemType Directory|Out-Null
foreach($case in @('wrong-name','wrong-machine','wrong-installation','wrong-server','wrong-report-server','wrong-policy','invalid-id','busy','bits-active','bits-error','installer-process','already-changed','backup-fails','stop-fails','stop-and-start-fail','id-changed-before-removal','remove-fails','after-delete','request-fails','no-new-id','native-registers','delayed-registration','success')){
 $folder=Join-Path $root $case;New-Item $folder -ItemType Directory|Out-Null
 $file=Join-Path $folder 'case.ps1';$state=Join-Path $folder 'state.json'
 $suffix=@'
}finally{
 [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'state.json'),(@{Trace=@($global:trace58);Id=$global:fixtureId58;Validation=$global:validation58;Service=$global:service58;WaitReads=$global:waitReads58}|ConvertTo-Json -Depth 4))
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
 $expectedSuccess=$case -in @('already-changed','native-registers','delayed-registration','success')
 if(($process.ExitCode -eq 0) -ne $expectedSuccess){throw "Unexpected result for ${case}: $output"}
 if($case -in @('wrong-name','wrong-machine','wrong-installation','wrong-server','wrong-report-server','wrong-policy','invalid-id','busy','bits-active','bits-error','installer-process','already-changed') -and $data.Trace.Count -ne 0){throw "Guard changed state: $case"}
 if($case -eq 'backup-fails' -and ($data.Trace -contains 'Stop')){throw 'Backup failure reached service stop'}
 if($case -in @('stop-fails','remove-fails','after-delete') -and ($data.Service -ne 'Running' -or $data.Trace -notcontains 'Start')){throw "Service not restored: $case"}
 if($case -eq 'stop-and-start-fail' -and ($output -notmatch 'STOP_FIXTURE_FAILURE' -or $output -notmatch 'START_FIXTURE_FAILURE')){throw 'Primary or restoration failure was lost'}
 $resetTrace='Backup,Stop,Remove-SusClientId,Remove-SusClientIdValidation,Start'
 if($case -in @('success','delayed-registration') -and (($data.Trace -join ',') -ne "$resetTrace,RequestRegistration,Report" -or $data.Id -ne '33333333-3333-3333-3333-333333333333')){throw 'Repair did not request registration once and report the new ID'}
 if($case -eq 'native-registers' -and (($data.Trace -join ',') -ne "$resetTrace,Report" -or !$data.Id)){throw 'An unnecessary registration request followed creation of a new ID'}
 if($case -eq 'no-new-id' -and ($output -notmatch 'WUA .*SusClientId' -or $output -notmatch 'WindowsUpdate-fixture-.*\.reg' -or ($data.Trace -join ',') -ne "$resetTrace,RequestRegistration" -or $data.WaitReads -ne 60 -or $data.Id -or $data.Validation -or $data.Service -ne 'Running')){throw ('Missing identity exceeded the wait, repeated work or lost the backup path: '+($data|ConvertTo-Json -Compress)+'; '+$output)}
 if($case -eq 'id-changed-before-removal' -and ($data.Id -ne '33333333-3333-3333-3333-333333333333' -or ($data.Validation -join ',') -ne '4,5' -or ($data.Trace -join ',') -ne 'Backup,Stop,Start' -or $data.Service -ne 'Running')){throw 'An ID registered while stopping wuauserv was overwritten or the service was left stopped'}
 if($case -eq 'after-delete' -and $data.Id -ne '33333333-3333-3333-3333-333333333333'){throw ('Partial repair lost new identity: '+($data|ConvertTo-Json -Compress)+'; '+$output)}
 if($case -eq 'request-fails' -and (($data.Trace -join ',') -ne "$resetTrace,RequestRegistration" -or $data.WaitReads -ne 0 -or $data.Service -ne 'Running')){throw 'An unsuccessful authorization request was repeated or sent a report'}
 if($case -in @('busy','bits-active','bits-error','installer-process') -and $output -notmatch 'DC_IDENTITY_NOT_STARTED'){throw "Missing readiness outcome for $case"}
 $expectedErrors=@{'stop-fails'='STOP_FIXTURE_FAILURE';'remove-fails'='REMOVE_FIXTURE_FAILURE';'after-delete'='INTERRUPTED_AFTER_DELETE';'request-fails'='DETECT_FIXTURE_FAILURE'}
 if($expectedErrors.ContainsKey($case) -and $output -notmatch $expectedErrors[$case]){throw ('Failure point was not reached: '+$case+'; '+$output)}
 Write-Output "PASS identity repair: $case"
 $process.Dispose()
}
