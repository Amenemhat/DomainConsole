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
$before=Read-Identity
if($before.Error -or $before.Server -ne $url -or ![guid]::Parse($before.SusClientId)){throw 'Incomplete initial client observation'}
$dcExpectedId58=$before.SusClientId
$dcExpectedMachine58=$before.Machine
$dcExpectedName58=$before.Name
$dcExpectedServer58=$before.Server
$began=[datetime]::UtcNow
& ([ScriptBlock]::Create((Body 'IdentityRepair58.cs' 'IdentityRepairBody58')))
$after=Read-Identity
if($after.Error -or [guid]$after.SusClientId -eq [guid]$before.SusClientId -or (Get-Service wuauserv).Status -ne 'Running'){throw 'Client registration did not change cleanly'}
$read=Body 'IdentityConfirmation58.cs' 'IdentityServerScript58'
$identityId58=$after.SusClientId;$ssl=$false;$port=8530
$end=[datetime]::UtcNow.AddMinutes(10)
do{
 try{
  $record=& ([ScriptBlock]::Create($read))|ConvertFrom-Json
  if([guid]$record.Id -eq [guid]$after.SusClientId -and $record.Name.TrimEnd('.') -eq $after.Name.TrimEnd('.') -and [datetime]$record.ReportedUtc -ge $began -and $record.Groups -contains $group.Name){
   Write-Output ("PASS independent WUA registration, latest report and expected group: "+($record|ConvertTo-Json -Compress))
   return
  }
 }catch{Write-Output ('Waiting for exact server record: '+$_.Exception.Message)}
 Start-Sleep -Seconds 20
}while([datetime]::UtcNow -lt $end)
throw ('WSUS has not confirmed name, new identifier, group and report by the deadline: '+($record|ConvertTo-Json -Compress))
