$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or $env:GITHUB_REF -ne 'refs/heads/lab/0.58-manual-priority'){throw 'Disposable test runner required'}
$source=[IO.File]::ReadAllText((Resolve-Path 'DomainConsole-CSharp/Console/UpdateConflict.cs'))
$match=[regex]::Match($source,'(?s)public const string RestartBody="""\r?\n(.*?)\r?\n""";')
if(!$match.Success){throw 'Production restart body missing'}
$code='$ErrorActionPreference=''Stop'';Add-Type -AssemblyName System.ServiceProcess;$names=@(''bits'');$withDependents=$false;' + $match.Groups[1].Value
$service=Get-Service BITS
if(@($service.DependentServices | Where-Object Status -eq 'Running').Count){throw 'Fixture must not interrupt another service'}
Import-Module BitsTransfer
if(@(Get-BitsTransfer -AllUsers).Count){throw 'Fixture must not interrupt another transfer'}
$initial=$service.Status
$directory=Join-Path $env:RUNNER_TEMP ('manual058-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($directory)|Out-Null
$marker=Join-Path $directory 'read-started'
$reg='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
$policy=(Get-ItemProperty -Path $reg).LocalAccountTokenFilterPolicy
$reader=$null;$manual=$null;$background=$null
$user='dcmanual'+([guid]::NewGuid().ToString('N').Substring(0,7))
$password=ConvertTo-SecureString (([guid]::NewGuid().ToString('N'))+'A!9') -AsPlainText -Force
$credential=[pscredential]::new(($env:COMPUTERNAME+'\'+$user),$password)
try {
 Enable-PSRemoting -Force -SkipNetworkProfileCheck
 New-LocalUser $user -Password $password -AccountNeverExpires|Out-Null
 Add-LocalGroupMember -Group (Get-LocalGroup -SID 'S-1-5-32-544').Name -Member $user
 New-ItemProperty $reg -Name LocalAccountTokenFilterPolicy -Value 1 -PropertyType DWord -Force|Out-Null
 $reader=New-PSSession -ComputerName localhost -Credential $credential -Authentication Negotiate
 $manual=New-PSSession -ComputerName localhost -Credential $credential -Authentication Negotiate
 Start-Service BITS
 $background=Invoke-Command -Session $reader -AsJob -ScriptBlock {param($p);[IO.File]::WriteAllText($p,'waiting');Start-Sleep -Seconds 80} -ArgumentList $marker
 $wait=[datetime]::UtcNow.AddSeconds(15)
 while(!(Test-Path $marker) -and [datetime]::UtcNow -lt $wait){Start-Sleep -Milliseconds 100}
 if(!(Test-Path $marker)){throw 'The simulated collector wait did not start'}
 $finished=Invoke-Command -Session $manual -ScriptBlock {param($body);& ([scriptblock]::Create($body))} -ArgumentList $code
 $lines=($finished|Out-String)
 $stop='Остановлена: bits'
 $start='bits: работает после восстановления'
 if(!$lines.Contains($stop) -or !$lines.Contains($start) -or (Get-Service BITS).Status -ne 'Running'){throw 'Production script did not stop and bring BITS back'}
 if($background.State -ne 'Running'){throw 'Manual restart did not finish before the outstanding read'}
 'PASS: production script actually stopped and restarted BITS via WinRM while a separate simulated collector read was still waiting.'
} finally {
 if($background){Stop-Job $background -ErrorAction SilentlyContinue;Remove-Job $background -Force -ErrorAction SilentlyContinue}
 if($reader){Remove-PSSession $reader}
 if($manual){Remove-PSSession $manual}
 if($initial -eq 'Stopped' -and (Get-Service BITS).Status -eq 'Running'){Stop-Service BITS -ErrorAction SilentlyContinue}
 Remove-LocalUser $user -ErrorAction SilentlyContinue
 if($null -eq $policy){Remove-ItemProperty $reg -Name LocalAccountTokenFilterPolicy -ErrorAction SilentlyContinue}else{Set-ItemProperty $reg -Name LocalAccountTokenFilterPolicy -Value $policy}
 Remove-Item $directory -Recurse -Force -ErrorAction SilentlyContinue
}
