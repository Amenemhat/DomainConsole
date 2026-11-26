param([Parameter(Mandatory=$true)][string]$ScriptFolder)
$ErrorActionPreference='Stop'
$template=[IO.File]::ReadAllText((Join-Path $ScriptFolder 'identity-repair58.ps1'))
$installationFixture='55555555-5555-5555-5555-555555555555'
$template=('$dcExpectedInstallation58='''+$installationFixture+''';$dcExpectedStatusServer58=''http://wsus.invalid:8530'';')+$template.Replace('$dcExpectedInstallation58='''';','').Replace('$dcExpectedStatusServer58='''';','')
$template=$template.Replace('$installer58=New-Object -ComObject Microsoft.Update.Installer','$installer58=$fixtureInstaller58')
$template=$template.Replace('$session58=New-Object -ComObject Microsoft.Update.Session','$session58=$fixtureSession58')
$template=$template.Replace("& reg.exe export 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate' $"+'backup58 /y | Out-Null','Export-Fixture58 $backup58')
$template=$template.Replace('& "$env:windir\System32\wuauclt.exe" /reportnow',"Invoke-Fixture58 'Report'")
$template=$template.Replace('Start-Sleep -Seconds 1','Start-Sleep -Milliseconds 1')
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
if($env:CASE58 -eq 'invalid-id'){$global:id58='00000000-0000-0000-0000-000000000000'}
$global:service58='Running'
$global:validation58=[byte[]]@(1,2,3)
function Invoke-Fixture58 {param($Name) $global:trace58.Add($Name)}
function Start-Sleep {param($Milliseconds) if($env:CASE58 -eq 'id-after-search' -and $global:trace58 -contains 'Search' -and $global:trace58 -notcontains 'Report'){$global:id58='33333333-3333-3333-3333-333333333333'}}
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
 return [pscustomobject]@{SusClientId=$global:id58;SusClientIdValidation=$global:validation58}
}
function Export-Fixture58 {param($Path) Invoke-Fixture58 'Backup';$global:LASTEXITCODE=0;if($env:CASE58 -eq 'backup-fails'){$global:LASTEXITCODE=5}else{[IO.File]::WriteAllText($Path,'fixture registry backup')}}
function Stop-Service {param($Name,$ErrorAction) if($Name -ne 'wuauserv'){throw 'Unexpected service'};Invoke-Fixture58 'Stop';if($env:CASE58 -in @('stop-fails','stop-and-start-fail')){throw 'STOP_FIXTURE_FAILURE'};$global:service58='Stopped';if($env:CASE58 -eq 'late-native-registers' -and $global:trace58 -contains 'Search'){$global:id58='33333333-3333-3333-3333-333333333333';$global:validation58=[byte[]]@(4,5)}}
function Start-Service {param($Name,$ErrorAction) if($Name -ne 'wuauserv'){throw 'Unexpected service'};Invoke-Fixture58 'Start';if($env:CASE58 -eq 'stop-and-start-fail'){throw 'START_FIXTURE_FAILURE'};$global:service58='Running';if(!$global:id58 -and $env:CASE58 -in @('after-delete','native-registers')){$global:id58='33333333-3333-3333-3333-333333333333'}}
function Get-Service {param($Name) $s=[pscustomobject]@{};$s|Add-Member ScriptMethod WaitForStatus {param($status,$timeout)};return $s}
function Remove-ItemProperty {param($Path,$Name,$ErrorAction) Invoke-Fixture58 ('Remove-'+$Name);if($env:CASE58 -eq 'remove-fails'){throw 'REMOVE_FIXTURE_FAILURE'};if($Name -eq 'SusClientId'){$global:id58=''}else{$global:validation58=$null};if($env:CASE58 -eq 'after-delete' -and $Name -eq 'SusClientId'){throw 'INTERRUPTED_AFTER_DELETE'}}
function New-ItemProperty {param($Path,$Name,$PropertyType,$Value,[switch]$Force,$ErrorAction)
 if($Name -eq 'SusClientIdValidation' -and $PropertyType -eq 'Binary'){$global:validation58=$Value}
 elseif($Name -eq 'SusClientId' -and $PropertyType -eq 'String'){$global:id58=$Value}
 else{throw 'Unexpected fixture registry property'}
 Invoke-Fixture58 ('Restore-'+$Name)
}
$fixtureInstaller58=[pscustomobject]@{IsBusy=($env:CASE58 -eq 'busy')}
$fixtureSearch58=[pscustomobject]@{ServerSelection=0;Online=$false}
$fixtureSearch58|Add-Member ScriptMethod Search {param($criteria) Invoke-Fixture58 'Search';if($this.ServerSelection -ne 1 -or !$this.Online){throw 'Search source incorrect'};if($env:CASE58 -notin @('no-new-id','late-native-registers','id-after-search')){$global:id58='33333333-3333-3333-3333-333333333333'};[pscustomobject]@{ResultCode=$(if($env:CASE58 -eq 'search-fails'){4}else{2})}}
$fixtureSession58=[pscustomobject]@{ClientApplicationID=''}
$fixtureSession58|Add-Member ScriptMethod CreateUpdateSearcher {$fixtureSearch58}
'@
$root=Join-Path $env:RUNNER_TEMP ('identity58-'+[guid]::NewGuid().ToString('N'))
New-Item $root -ItemType Directory|Out-Null
foreach($case in @('wrong-name','wrong-machine','wrong-installation','wrong-server','wrong-report-server','wrong-policy','invalid-id','busy','bits-active','bits-error','installer-process','already-changed','backup-fails','stop-fails','stop-and-start-fail','remove-fails','after-delete','search-fails','no-new-id','native-registers','late-native-registers','id-after-search','success')){
 $folder=Join-Path $root $case;New-Item $folder -ItemType Directory|Out-Null
 $file=Join-Path $folder 'case.ps1';$state=Join-Path $folder 'state.json'
 $suffix=@'
}finally{
 [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'state.json'),(@{Trace=@($global:trace58);Id=$global:id58;Validation=$global:validation58;Service=$global:service58}|ConvertTo-Json -Depth 4))
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
 $expectedSuccess=$case -in @('already-changed','native-registers','late-native-registers','id-after-search','success')
 if(($process.ExitCode -eq 0) -ne $expectedSuccess){throw "Unexpected result for ${case}: $output"}
 if($case -in @('wrong-name','wrong-machine','wrong-installation','wrong-server','wrong-report-server','wrong-policy','invalid-id','busy','bits-active','bits-error','installer-process','already-changed') -and $data.Trace.Count -ne 0){throw "Guard changed state: $case"}
 if($case -eq 'backup-fails' -and ($data.Trace -contains 'Stop')){throw 'Backup failure reached service stop'}
 if($case -in @('stop-fails','remove-fails','after-delete') -and ($data.Service -ne 'Running' -or $data.Trace -notcontains 'Start')){throw "Service not restored: $case"}
 if($case -eq 'stop-and-start-fail' -and ($output -notmatch 'STOP_FIXTURE_FAILURE' -or $output -notmatch 'START_FIXTURE_FAILURE')){throw 'Primary or restoration failure was lost'}
 if($case -eq 'success' -and (($data.Trace -join ',') -ne 'Backup,Stop,Remove-SusClientId,Remove-SusClientIdValidation,Start,Search,Report' -or $data.Id -ne '33333333-3333-3333-3333-333333333333')){throw 'Successful repair did not follow expected order'}
 if($case -eq 'native-registers' -and (($data.Trace -join ',') -ne 'Backup,Stop,Remove-SusClientId,Remove-SusClientIdValidation,Start,Report' -or !$data.Id)){throw 'An unnecessary online search followed a successful native registration'}
 if($case -eq 'id-after-search' -and (($data.Trace -join ',') -ne 'Backup,Stop,Remove-SusClientId,Remove-SusClientIdValidation,Start,Search,Report' -or $data.Id -ne '33333333-3333-3333-3333-333333333333')){throw 'A delayed ID was not obtained before requesting its report'}
 if($case -eq 'no-new-id' -and ($output -notmatch 'WUA .*SusClientId' -or @($data.Trace|Where-Object {$_ -eq 'Search'}).Count -ne 1 -or $data.Id -ne '22222222-2222-2222-2222-222222222222' -or ($data.Validation -join ',') -ne '1,2,3' -or $data.Service -ne 'Running')){throw ('Missing identity did not restore the original registration values and service: '+($data|ConvertTo-Json -Compress)+'; '+$output)}
 if($case -eq 'late-native-registers' -and ($data.Id -ne '33333333-3333-3333-3333-333333333333' -or ($data.Validation -join ',') -ne '4,5' -or $data.Trace -like 'Restore-*')){throw 'Rollback overwrote a new ID created while stopping wuauserv'}
 if($case -in @('search-fails','after-delete') -and $data.Id -ne '33333333-3333-3333-3333-333333333333'){throw ('Partial repair lost new identity: '+($data|ConvertTo-Json -Compress)+'; '+$output)}
 if($case -in @('busy','bits-active','bits-error','installer-process') -and $output -notmatch 'DC_IDENTITY_NOT_STARTED'){throw "Missing readiness outcome for $case"}
 $expectedErrors=@{'stop-fails'='STOP_FIXTURE_FAILURE';'remove-fails'='REMOVE_FIXTURE_FAILURE';'after-delete'='INTERRUPTED_AFTER_DELETE'}
 if($expectedErrors.ContainsKey($case) -and $output -notmatch $expectedErrors[$case]){throw ('Failure point was not reached: '+$case+'; '+$output)}
 Write-Output "PASS identity repair: $case"
 $process.Dispose()
}
