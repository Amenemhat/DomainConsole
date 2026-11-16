$ErrorActionPreference='Stop'
# Only present in the offline-personalized disposable guest image.
if (!(Test-Path C:\DomainConsoleLab\node.json)){throw 'Missing lab guest identity'}
if(Test-Path C:\DomainConsoleLab\network-ready.txt){return}
$node=Get-Content C:\DomainConsoleLab\node.json -Raw|ConvertFrom-Json
Start-Transcript -Path C:\DomainConsoleLab\setup.log -Append
$drivers=Get-Volume | Where-Object {$_.DriveLetter -and (Test-Path ($_.DriveLetter+':\NetKVM\2k22\amd64\netkvm.inf'))} | Select-Object -First 1
if($drivers){& pnputil.exe /add-driver ($drivers.DriveLetter+':\NetKVM\2k22\amd64\netkvm.inf') /install | Out-Null}
$adapter=$null
for($n=0;$n -lt 30 -and !$adapter;$n++){ $adapter=Get-NetAdapter |Where-Object Status -eq Up|Select-Object -First 1;if(!$adapter){Start-Sleep -Seconds 2}}
if(!$adapter){throw 'Private lab NIC unavailable'}
if(!(Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 |Where-Object IPAddress -eq $node.ip)){
 Set-NetIPInterface -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -Dhcp Disabled
 New-NetIPAddress -IPAddress $node.ip -AddressFamily IPv4 -PrefixLength 24 -InterfaceIndex $adapter.ifIndex | Out-Null
}
Set-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' LocalAccountTokenFilterPolicy 1
Enable-PSRemoting -Force -SkipNetworkProfileCheck
New-NetFirewallRule -DisplayName 'DC lab private WinRM' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5985 -RemoteAddress 10.77.57.0/24 | Out-Null
if($node.name -eq 'DCWSUS'){
 New-NetFirewallRule -DisplayName 'DC lab private WSUS' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8530 -RemoteAddress 10.77.57.0/24|Out-Null
}
Set-Content C:\DomainConsoleLab\network-ready.txt ([DateTime]::UtcNow.ToString('o'))
Stop-Transcript
