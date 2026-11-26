$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or (Get-CimInstance Win32_ComputerSystem).PartOfDomain){throw 'Dedicated non-domain test runner required'}
function Body($file,$name){
 $s=[IO.File]::ReadAllText((Join-Path $pwd ("_app/DomainConsole-CSharp/Console/"+$file)))
 $m=[regex]::Match($s,('public const string '+$name+'\s*=\s*"""\r?\n(?<body>[\s\S]*?)\r?\n""";'))
 if(!$m.Success){throw "Missing production script $name"}
 return $m.Groups['body'].Value
}
[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration')
$wsus=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer('localhost',$false,8530)
$group=$wsus.CreateComputerTargetGroup('DomainConsoleAcceptance')
try{
 Import-Module WebAdministration
 $site='IIS:\Sites\WSUS Administration'
 Set-ItemProperty $site logFile.enabled -Value $true
 Set-ItemProperty $site logFile.flushByEntryCountW3CLog -Value 1
}catch{Write-Warning ('IIS trace setup: '+$_.Exception.Message)}

$url="http://${env:COMPUTERNAME}:8530"
$p='HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
New-Item $p -Force|Out-Null;New-Item ($p+'\AU') -Force|Out-Null
foreach($setting in @('WUServer','WUStatusServer')){New-ItemProperty -Path $p -Name $setting -Value $url -PropertyType String -Force|Out-Null}
New-ItemProperty -Path $p -Name TargetGroupEnabled -Value 1 -PropertyType DWord -Force|Out-Null
New-ItemProperty -Path $p -Name TargetGroup -Value 'DomainConsoleAcceptance' -PropertyType String -Force|Out-Null
New-ItemProperty -Path ($p+'\AU') -Name UseWUServer -Value 1 -PropertyType DWord -Force|Out-Null
$initialService=Get-CimInstance Win32_Service -Filter "Name='wuauserv'"
Write-Output ("Disposable runner WUA startup: "+$initialService.StartMode)
if($initialService.StartMode -eq 'Disabled'){Set-Service -Name wuauserv -StartupType Manual}
try{Restart-Service wuauserv -Force}catch{& sc.exe qc wuauserv;& sc.exe queryex wuauserv;throw}
$session=New-Object -ComObject Microsoft.Update.Session
$search=$session.CreateUpdateSearcher();$search.ServerSelection=1;$search.Online=$true
$result=$search.Search('IsInstalled=0 and IsHidden=0')
if([int]$result.ResultCode -ne 2){throw "Initial managed scan: $($result.ResultCode)"}
$probe=Body 'Identity58.cs' 'IdentityProbe58'
function Read-Identity {& ([ScriptBlock]::Create($probe+[Environment]::NewLine+'[pscustomobject]$dcIdentity58'))}
$repairBody=Body 'IdentityRepair58.cs' 'IdentityRepairBody58'
$tokens=$null;$parseErrors=$null
$syntax=[System.Management.Automation.Language.Parser]::ParseInput($repairBody,[ref]$tokens,[ref]$parseErrors)
$guard=$syntax.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-IdentityRepairIdle58'},$true)
if($parseErrors -or !$guard){throw 'Could not find the product readiness guard'}
$checkReady=[scriptblock]::Create($guard.Extent.Text+[Environment]::NewLine+'Assert-IdentityRepairIdle58')
$readyDeadline=[datetime]::UtcNow.AddMinutes(12)
while($true){
 try{& $checkReady;break}catch{
  if($_.Exception.Message -notlike 'DC_IDENTITY_NOT_STARTED|Windows Update занят*' -or [datetime]::UtcNow -ge $readyDeadline){throw}
  Start-Sleep -Seconds 15
 }
}
$before=Read-Identity
if($before.Error -or $before.Server -ne $url -or ![guid]::Parse($before.SusClientId)){throw 'Incomplete initial client observation'}
$dcExpectedId58=$before.SusClientId
$dcExpectedMachine58=$before.Machine
$dcExpectedName58=$before.Name
$dcExpectedServer58=$before.Server
$began=[datetime]::UtcNow
$labPath=Join-Path $env:RUNNER_TEMP 'concurrent-wsus-acceptance'
New-Item $labPath -ItemType Directory -Force|Out-Null
$bodyPath=Join-Path $labPath 'requested-repair.ps1'
function Literal($text){"'"+$text.Replace("'","''")+"'"}
$prefix='$dcExpectedId58='+(Literal $before.SusClientId)+';$dcExpectedMachine58='+(Literal $before.Machine)+';$dcExpectedName58='+(Literal $before.Name)+';$dcExpectedServer58='+(Literal $before.Server)+';'
$utf8=New-Object Text.UTF8Encoding($true)
[IO.File]::WriteAllText($bodyPath,$prefix+$repairBody,$utf8)
$exe=Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
function Start-Attempt([string]$label) {
 $wrapper=Join-Path $labPath ($label+'.ps1')
 $rescue='try { & '+(Literal $bodyPath)+' | Out-File -Encoding utf8 -LiteralPath '+(Literal ($wrapper+'.out'))+' }catch{[IO.File]::WriteAllText('+(Literal ($wrapper+'.error'))+',$_.Exception.Message);exit 1}'
 [IO.File]::WriteAllText($wrapper,$rescue,$utf8)
 return Start-Process $exe -ArgumentList ('-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$wrapper+'"') -PassThru
}
function End-Attempt($process) {
 if(!$process.WaitForExit(180000)){$process.Kill();throw 'Isolated WUA test process exceeded its time limit'}
 $process.Refresh()
 return $process.ExitCode
}
function Backup-Count {@(Get-ChildItem "$env:ProgramData\DomainConsole-Backups\WindowsUpdate-*.reg" -ErrorAction SilentlyContinue).Count}
$beforeCopies=Backup-Count
$machineGuid=(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography').MachineGuid
$marker=Join-Path $env:windir 'SoftwareDistribution\Download\DomainConsole-test-marker'
New-Item (Split-Path $marker) -ItemType Directory -Force|Out-Null
[IO.File]::WriteAllText($marker,'Keep cached downloads')
# Independent process must refuse an occupied client operation before touching WUA.
$mutex=New-Object Threading.Mutex($false,'Global\DomainConsole.WsusIdentity58')
try{
 if(!$mutex.WaitOne(0)){throw 'Unexpected pre-existing repair'}
 $blocked=Start-Attempt 'held'
 if((End-Attempt $blocked) -eq 0 -or [IO.File]::ReadAllText((Join-Path $labPath 'held.ps1.error')) -notmatch 'уже выполняется'){throw 'Cross-process exclusion not observed'}
}finally{$mutex.ReleaseMutex();$mutex.Dispose()}
if((Backup-Count) -ne $beforeCopies -or (Read-Identity).SusClientId -ne $before.SusClientId){throw 'Rejected concurrent request changed the client'}
$one=Start-Attempt 'first'
$two=Start-Attempt 'second'
$codes=@((End-Attempt $one),(End-Attempt $two))
if(0 -notin $codes){
 foreach($label in 'first','second'){
  $errorPath=Join-Path $labPath ($label+'.ps1.error')
  if(Test-Path $errorPath){Write-Output ($label+': '+[IO.File]::ReadAllText($errorPath))}
 }
 $local=Read-Identity
 Write-Output ("Local ID present: "+[bool]$local.SusClientId+"; ID changed: "+($local.SusClientId -ne $before.SusClientId)+"; new backups: "+((Backup-Count)-$beforeCopies)+"; service: "+(Get-Service wuauserv).Status)
 throw ('Neither concurrent request completed. Exit codes: '+($codes -join ', '))
}
foreach($label in 'first','second'){
 $outputPath=Join-Path $labPath ($label+'.ps1.out')
 if(Test-Path $outputPath){Write-Output ($label+' stdout: '+[IO.File]::ReadAllText($outputPath))}
 $errorPath=Join-Path $labPath ($label+'.ps1.error')
 if((Test-Path $errorPath) -and [IO.File]::ReadAllText($errorPath) -notmatch 'уже выполняется'){throw ('Unexpected operation failure: '+[IO.File]::ReadAllText($errorPath))}
}
$after=Read-Identity
if($after.Error -or [guid]$after.SusClientId -eq [guid]$before.SusClientId -or (Get-Service wuauserv).Status -ne 'Running'){throw 'Client registration did not change cleanly'}
$replay=Start-Attempt 'replay'
if((End-Attempt $replay) -ne 0 -or (Read-Identity).SusClientId -ne $after.SusClientId -or (Backup-Count) -ne $beforeCopies+1){throw 'Replayed request changed the new ID or repeated a backup'}
if((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography').MachineGuid -ne $machineGuid -or !(Test-Path $marker) -or (Get-ItemProperty $p).WUServer -ne $url){throw 'Machine identity, policy or existing download cache was modified'}
Write-Output ('PASS client mutex, two independent repair processes and replay: exits '+($codes -join ',')+'; one backup; MachineGuid, policy and cached downloads preserved')
$read=Body 'IdentityConfirmation58.cs' 'IdentityServerScript58'
$identityId58=$after.SusClientId;$lookupName58=$after.Name;$ssl=$false;$port=8530
function Wait-ForRegistration([int]$seconds){
 $until=[datetime]::UtcNow.AddSeconds($seconds)
 do {
  $record=& ([ScriptBlock]::Create($read))|ConvertFrom-Json
  if($record.Id -eq $identityId58 -and $record.Name.TrimEnd('.') -eq $after.Name.TrimEnd('.') -and [datetime]$record.ReportedUtc -ge $began -and $record.Groups -contains $group.Name){return $record}
  Start-Sleep -Seconds 15
 }while([datetime]::UtcNow -lt $until)
 Write-Host ("Last exact WSUS lookup: "+($record|ConvertTo-Json -Compress))
}
$product=Wait-ForRegistration 120
if($product){
 Write-Output ("PASS independent WUA registration, latest report and expected group: "+($product|ConvertTo-Json -Compress))
 return
}
# Keep this control separate from product acceptance. The current local ID is already known.
Write-Output 'Requesting native authorization after the new local identifier is known'
& "$env:windir\System32\wuauclt.exe" /resetauthorization /detectnow
$result=Wait-ForRegistration 120
if(!$result){
 & "$env:windir\System32\wuauclt.exe" /reportnow
 $result=Wait-ForRegistration 180
}
if((Read-Identity).SusClientId -ne $identityId58){throw 'The diagnostic authorization changed the already-created ID'}
if($result){
 Write-Output ("DIAGNOSTIC CONFIRMED: "+($result|ConvertTo-Json -Compress))
 throw 'Server confirmation required a separate authorization request after the new ID was known'
}
Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-WindowsUpdateClient/Operational';StartTime=$began.ToLocalTime()} -MaxEvents 15 -ErrorAction SilentlyContinue|Select-Object Id,TimeCreated,Message|Format-List|Out-Host
Write-Output ("Client IDs: old="+$before.SusClientId+"; new="+$after.SusClientId)
$wsus.SearchComputerTargets($after.Name)|Select-Object Id,FullDomainName,LastSyncTime,LastReportedStatusTime|Format-List|Out-Host
$iisRoot=Join-Path $env:SystemDrive 'inetpub\logs\LogFiles'
Get-ChildItem -LiteralPath $iisRoot -Filter '*.log' -Recurse -ErrorAction SilentlyContinue|
 Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 2|ForEach-Object {
  Get-Content -LiteralPath $_.FullName -Tail 250|Where-Object {$_ -like '#Fields:*' -or $_ -match '/(?:ClientWebService|SimpleAuthWebService|ReportingWebService)/'}|Select-Object -Last 35
 }
try{
 $updateLog=Join-Path $labPath 'WindowsUpdate.log'
 Get-WindowsUpdateLog -LogPath $updateLog | Out-Null
 Get-Content -LiteralPath $updateLog -Tail 3500|Where-Object {
  $_ -match 'RegisterComputer|ClientWebService|SimpleAuthWebService|ReportingWebService|AuthorizationCookie|GetCookie|SyncUpdates|WSUS server|SUSClientId|DomainConsole\.RegistrationDiagnostic'
 } | Select-Object -Last 100
}catch{Write-Warning ('Could not decode Windows Update trace: '+$_.Exception.Message)}
throw 'The exact WSUS record or a current report remained absent after the diagnostic continuation'
