$ErrorActionPreference='Stop'
$lab='C:\DomainConsoleLab'
$log=Join-Path $lab 'network.log'
function Step([string]$message) { Add-Content -LiteralPath $log -Value ("{0:o} {1}" -f [DateTime]::UtcNow,$message) }
try {
 $node=Get-Content (Join-Path $lab 'node.json') -Raw|ConvertFrom-Json
 Step "Startup for $($node.name)"
 if($node.temporaryAdmin){
  $admin=Get-LocalUser|Where-Object {$_.SID.Value -match '-500$'}|Select-Object -First 1
  if(!$admin){throw 'Built-in administrator unavailable'}
  Enable-LocalUser -Name $admin.Name
  Set-LocalUser -Name $admin.Name -Password (ConvertTo-SecureString $node.temporaryAdmin -AsPlainText -Force)
 }
 Step 'Local account ready'
 foreach($drive in [IO.DriveInfo]::GetDrives()){
  if($drive.DriveType -ne 'CDRom' -or !$drive.IsReady){continue}
  $inf=Join-Path $drive.RootDirectory.FullName 'NetKVM\2k22\amd64\netkvm.inf'
  if(Test-Path -LiteralPath $inf){
   $out=& pnputil.exe /add-driver $inf /install
   Step ("Signed network driver: "+($out -join ' '))
  }
 }
 $adapter=$null
 for($i=0;$i -lt 30 -and !$adapter;$i++){
  $adapter=Get-NetAdapter -ErrorAction SilentlyContinue|Where-Object Status -eq Up|Select-Object -First 1
  if(!$adapter){Start-Sleep -Seconds 2}
 }
 if(!$adapter){throw 'Private lab adapter unavailable'}
 Step "Adapter $($adapter.InterfaceDescription), index $($adapter.ifIndex)"
 $known=@(Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue)
 if($node.ip -notin $known.IPAddress){
  Set-NetIPInterface -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -Dhcp Disabled
  New-NetIPAddress -IPAddress $node.ip -AddressFamily IPv4 -PrefixLength 24 -InterfaceIndex $adapter.ifIndex | Out-Null
 }
 Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' -Name LocalAccountTokenFilterPolicy -Value 1
 Step "IP assigned $($node.ip)"
 Set-Service -Name WinRM -StartupType Automatic
 Start-Service WinRM
 Enable-PSRemoting -Force -SkipNetworkProfileCheck|Out-Null
 if(!(Get-NetFirewallRule -Name DCLabPrivateWinRM -ErrorAction SilentlyContinue)){
  New-NetFirewallRule -Name DCLabPrivateWinRM -DisplayName 'DC lab private WinRM' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5985 -RemoteAddress 10.77.57.0/24 | Out-Null
 }
 if($node.name -eq 'DCWSUS' -and !(Get-NetFirewallRule -Name DCLabPrivateWSUS -ErrorAction SilentlyContinue)){
  New-NetFirewallRule -Name DCLabPrivateWSUS -DisplayName 'DC lab private WSUS' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8530 -RemoteAddress 10.77.57.0/24|Out-Null
 }
 $listener=Get-NetTCPConnection -LocalPort 5985 -State Listen -ErrorAction Stop|Select-Object -First 1
 Step "WinRM listening on $($listener.LocalAddress)"
 $node.PSObject.Properties.Remove('temporaryAdmin')
 $node|ConvertTo-Json|Set-Content (Join-Path $lab 'node.json')
 Set-Content (Join-Path $lab 'network-ready.txt') ([DateTime]::UtcNow.ToString('o'))
}catch{
 Step ("Failed on line "+$_.InvocationInfo.ScriptLineNumber+": "+$_.Exception.Message)
 exit 1
}
