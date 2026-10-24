$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$path=$PSScriptRoot
function Save-State($value){$tmp=Join-Path $path 'state.tmp';$value|ConvertTo-Json -Depth 8 -Compress|Set-Content -LiteralPath $tmp -Encoding UTF8;Move-Item -LiteralPath $tmp -Destination (Join-Path $path 'state.json') -Force}
# Windows closes this job on supervisor exit (including scheduler termination).
# Only our read-only child workers enter it; WUA/BITS services never do.
Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;
public static class DcReadJob42 {
 [StructLayout(LayoutKind.Sequential)] struct Basic {public long a,b;public uint flags;public UIntPtr min,max;public uint active;public UIntPtr affinity;public uint priority,scheduling;}
 [StructLayout(LayoutKind.Sequential)] struct Io {public ulong a,b,c,d,e,f;}
 [StructLayout(LayoutKind.Sequential)] struct Extended {public Basic basic;public Io io;public UIntPtr a,b,c,d;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr a,string n);
 [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr h,int c,ref Extended v,uint l);
 [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr h,IntPtr p);
 public static IntPtr Open(){var h=CreateJobObject(IntPtr.Zero,null);var e=new Extended();e.basic.flags=0x2000;if(h==IntPtr.Zero||!SetInformationJobObject(h,9,ref e,(uint)Marshal.SizeOf(e)))throw new Exception("Cannot establish read worker lifetime boundary");return h;}
 public static void Add(IntPtr h,IntPtr p){if(!AssignProcessToJobObject(h,p))throw new Exception("Cannot contain read worker");}
}
'@
$job=[DcReadJob42]::Open()
$run=Get-Content -LiteralPath (Join-Path $path 'run.json') -Raw -Encoding UTF8|ConvertFrom-Json
$gate=$null;foreach($attempt in 1..30){try{$gate=[IO.File]::Open((Join-Path $path 'execution.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);break}catch{Start-Sleep -Milliseconds 100}};if(!$gate){throw 'Не удалось получить execution.lock'}
$clock=[Diagnostics.Stopwatch]::StartNew();$errors=@();$parts=@{};$worker=$null
try{
 foreach($part in @('Reboot','Activity')){
  Save-State @{Id=$run.Id;Phase=$part;StartedUtc=$run.StartedUtc;UpdatedUtc=[DateTime]::UtcNow.ToString('o');Supervisor=$PID}
  if(Test-Path -LiteralPath (Join-Path $path 'cancel.flag')){$errors+='Запрошена остановка служебного сборщика';break}
  $start=New-Object Diagnostics.ProcessStartInfo
  $start.FileName=Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
  $start.Arguments='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+(Join-Path $path ($part+'.ps1'))+'"'
  $start.UseShellExecute=$false;$start.CreateNoWindow=$true
  $worker=[Diagnostics.Process]::Start($start)
  try{[DcReadJob42]::Add($job,$worker.Handle)}catch{try{$worker.Kill();$null=$worker.WaitForExit(3000)}catch{};throw}
  $partClock=[Diagnostics.Stopwatch]::StartNew();$budget=if($part -eq 'Reboot'){20}else{10}
  while(!$worker.WaitForExit(200)){
   if($clock.Elapsed.TotalSeconds -ge 45 -or (Test-Path -LiteralPath (Join-Path $path 'cancel.flag'))){[IO.File]::WriteAllText((Join-Path $path 'cancel.flag'),'cancel')}
   if($partClock.Elapsed.TotalSeconds -ge $budget){[IO.File]::WriteAllText((Join-Path $path ($part+'.cancel')),'cancel')};if($partClock.Elapsed.TotalSeconds -ge ($budget+5) -or $clock.Elapsed.TotalSeconds -ge 50){break}
  }
  if(!$worker.HasExited){$worker.Kill();if(!$worker.WaitForExit(3000)){throw 'Остановка служебного worker не подтверждена'};$errors+=($part+': превышено время чтения')}
  elseif($worker.ExitCode -ne 0){$errors+=($part+': ошибка чтения')}
  $file=Join-Path $path ($part+'.json')
  if(Test-Path -LiteralPath $file){try{$parts[$part]=Get-Content -LiteralPath $file -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop}catch{$errors+=($part+': некорректный результат')}}
  $errorFile=Join-Path $path ($part+'.error');if(Test-Path -LiteralPath $errorFile){$errors+=([IO.File]::ReadAllText($errorFile))}
  $worker.Dispose();$worker=$null
 }
 if($parts.Reboot -and $parts.Reboot.Error){$errors+=[string]$parts.Reboot.Error};if($parts.Activity -and $parts.Activity.Limitation){$errors+=[string]$parts.Activity.Limitation}
 if(!$parts.Reboot){$parts.Reboot=@{CollectedUtc=[DateTime]::UtcNow.ToString('o');Required=$null;Error=($errors -join '; ')}}
 if(!$parts.Activity){$parts.Activity=@{CollectedUtc=[DateTime]::UtcNow.ToString('o');Limitation=($errors -join '; ')}}
 $bundle=@{Id=$run.Id;CollectedUtc=[DateTime]::UtcNow.ToString('o');Reboot=$parts.Reboot;Activity=$parts.Activity;Complete=($errors.Count -eq 0);Healthy=([bool]$parts.Reboot.BootUtc -and !$parts.Reboot.Error);Error=($errors -join '; ')}
 $bundle|ConvertTo-Json -Depth 25 -Compress|Set-Content -LiteralPath (Join-Path $path 'result.tmp') -Encoding UTF8
 Move-Item -LiteralPath (Join-Path $path 'result.tmp') -Destination (Join-Path $path 'result.json') -Force
 Save-State @{Id=$run.Id;Phase='Completed';UpdatedUtc=[DateTime]::UtcNow.ToString('o');Error=$bundle.Error}
}catch{[IO.File]::WriteAllText((Join-Path $path 'error.txt'),$_.Exception.Message);Save-State @{Id=$run.Id;Phase='Failed';UpdatedUtc=[DateTime]::UtcNow.ToString('o');Error=$_.Exception.Message}}
finally{if($worker){try{if(!$worker.HasExited){$worker.Kill();$null=$worker.WaitForExit(3000)}}catch{};$worker.Dispose()};$gate.Dispose()}
