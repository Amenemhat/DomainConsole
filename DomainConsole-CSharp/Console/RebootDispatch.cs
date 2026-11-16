namespace DomainConsole;

public partial class MainWindow {
 // The lock is on the client. Independent consoles and repeated deliveries share it.
 internal const string EnterRebootDispatch="""
$ErrorActionPreference='Stop'
$dcOwnId='{{REBOOT_JOB}}'
$dcBoot=(Get-CimInstance Win32_OperatingSystem -ErrorAction Stop).LastBootUpTime.ToUniversalTime().ToString('o')
$dcRoot=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$dcOwnerFile=Join-Path $dcRoot 'header-reboot-owner.json'
$dcGate=New-Object Threading.Mutex($false,'Global\DomainConsole.HeaderReboot')
$dcHeld=$false
try{
 try{$dcHeld=$dcGate.WaitOne(0)}catch [Threading.AbandonedMutexException]{$dcHeld=$true}
 if(!$dcHeld){throw 'Назначение перезагрузки уже выполняется на клиенте. Новая команда не отправлена.'}
 if(Test-Path -LiteralPath $dcOwnerFile){
  $dcOwner=Get-Content -LiteralPath $dcOwnerFile -Raw | ConvertFrom-Json
  if($dcOwner.Boot -eq $dcBoot){
   $dcGuid=[guid]::Parse([string]$dcOwner.Id)
   $dcReceipt=Join-Path $dcRoot ($dcGuid.ToString()+'\reboot-plan.json')
   $dcOld=if(Test-Path -LiteralPath $dcReceipt){Get-Content -LiteralPath $dcReceipt -Raw|ConvertFrom-Json}
   if(!$dcOld -or ($dcOld.DispatchUtc -and !$dcOld.Cancelled -and !$dcOld.DispatchError -and (!$dcOld.DispatchExitCode -or $dcOld.DispatchExitCode -eq 0))){
    throw ('Перезагрузка уже запрошена для этой загрузки Windows. Проверьте исходное задание '+$dcGuid)
   }
  }
 }
 $dcTmp=$dcOwnerFile+'.tmp'
 [IO.File]::WriteAllText($dcTmp,(@{Id=$dcOwnId;Boot=$dcBoot}|ConvertTo-Json -Compress))
 if([IO.File]::Exists($dcOwnerFile)){[IO.File]::Replace($dcTmp,$dcOwnerFile,$dcOwnerFile+'.previous')}else{[IO.File]::Move($dcTmp,$dcOwnerFile)}
""";
 internal const string LeaveRebootDispatch="\n}finally{if($dcHeld){$dcGate.ReleaseMutex()};$dcGate.Dispose()}";
}
