$ErrorActionPreference='Stop'
$result=Install-WindowsFeature UpdateServices -IncludeManagementTools
if (!$result.Success){throw "WSUS feature installation failed: $($result.ExitCode)"}
$store=Join-Path $env:SystemDrive 'WsusLabContent'
New-Item -ItemType Directory -Path $store -Force | Out-Null
& "$env:ProgramFiles\Update Services\Tools\WsusUtil.exe" postinstall "CONTENT_DIR=$store"
if ($LASTEXITCODE){throw "WSUS postinstall failed with $LASTEXITCODE"}
Import-Module WebAdministration
$pool='IIS:\AppPools\WsusPool'
Set-ItemProperty $pool recycling.periodicRestart.privateMemory -Value 0
[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration')
$server=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer('localhost',$false,8530)
$config=$server.GetConfiguration()
$config.TargetingMode=[Microsoft.UpdateServices.Administration.TargetingMode]::Client
$config.Save()
Write-Output "WSUS ready, version $($server.Version)"
