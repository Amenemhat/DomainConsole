namespace DomainConsole;
public static class MaintenanceRegistration47 {
 public const string Script = """
$maintenanceLock47=$null
try {
 $maintenanceLock47=[IO.File]::Open((Join-Path $root 'maintenance47.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
 $oldMaintenance=Get-ScheduledTask -TaskName 'DomainConsoleCSharp-Maintenance' -ErrorAction SilentlyContinue
 $replace47=!$oldMaintenance
 if($oldMaintenance){
  $actions47=@($oldMaintenance.Actions)
  if($actions47.Count -ne 1){throw 'Задача Maintenance имеет неизвестные действия; не изменена'}
  $oldPath47=[IO.Path]::GetFullPath([string]$actions47[0].Execute)
  $agentRoot47=[IO.Path]::GetFullPath((Join-Path $root 'Agent'))+[IO.Path]::DirectorySeparatorChar
  $parent47=Split-Path (Split-Path $oldPath47) -Leaf
  if(!$oldPath47.StartsWith($agentRoot47,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $oldPath47 -Leaf) -ne 'DomainConsole.Agent.exe' -or $parent47 -notmatch '^[a-fA-F0-9]{64}$' -or [string]$actions47[0].Arguments -ne ('cleanup "'+$root+'"')){throw 'Принадлежность задачи Maintenance не подтверждена; не изменена'}
  $principal47=[string]$oldMaintenance.Principal.UserId
  if($principal47 -ne 'S-1-5-18'){$principal47=(New-Object Security.Principal.NTAccount($principal47)).Translate([Security.Principal.SecurityIdentifier]).Value}
  if($principal47 -ne 'S-1-5-18'){throw 'Учетная запись Maintenance не SYSTEM; задача не изменена'}
  if((Get-FileHash -LiteralPath $oldPath47 -Algorithm SHA256).Hash -ne $parent47){throw 'Контрольная сумма прежнего Maintenance не совпадает; задача не изменена'}
  $previousVersion47=[version](Get-Item -LiteralPath $oldPath47).VersionInfo.FileVersion
  $incomingVersion47=[version](Get-Item -LiteralPath $exe).VersionInfo.FileVersion
  $replace47=$oldMaintenance.State -notin @('Running','Queued') -and $previousVersion47 -lt $incomingVersion47
 }
 if($replace47){Register-ScheduledTask -TaskName 'DomainConsoleCSharp-Maintenance' -Action $maintenance -Trigger (New-ScheduledTaskTrigger -Daily -At '00:30') -Principal $principal -Settings $settings -Force|Out-Null}
} finally {if($maintenanceLock47){$maintenanceLock47.Dispose()}}
""";
}
