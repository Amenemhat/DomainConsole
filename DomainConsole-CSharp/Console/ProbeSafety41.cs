using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using DomainConsole.Shared;
namespace DomainConsole;

// One fixed, bounded SYSTEM collector. The latch survives loss of the calling
// WinRM shell: an unknown outcome never admits another collection automatically.
public sealed partial class RemoteService {
 public static string SnapshotCollector41=>"$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';$dcRebootJobId='';try{$reboot=& {\n"+RebootStateScript+"\n}|ConvertFrom-Json -ErrorAction Stop;$activity=& {\n"+LiveUpdateScript+"\n}|ConvertFrom-Json -ErrorAction Stop;@{Reboot=$reboot;Activity=$activity;CollectedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 12 -Compress|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'result.tmp') -Encoding UTF8;Move-Item -LiteralPath (Join-Path $PSScriptRoot 'result.tmp') -Destination (Join-Path $PSScriptRoot 'result.json') -Force}catch{[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'error.txt'),$_.Exception.Message);exit 1}";
 public static string SnapshotWrapper41(bool reboot)=>SnapshotWrapperTemplate41.Replace("@@SCRIPT@@",PowerShellBridge.Literal(SnapshotCollector41)).Replace("@@KIND@@",reboot?"Reboot":"Activity");
 public const string SnapshotWrapperTemplate41="""
$root=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$path=Join-Path $root 'Snapshot41'
New-Item -Path $path -ItemType Directory -Force|Out-Null
$acl=New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
foreach($sid in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier($sid)),'FullControl','ContainerInherit,ObjectInherit','None','Allow')))}
Set-Acl -LiteralPath $path -AclObject $acl
$lock=$null
try{
 try{$lock=[IO.File]::Open((Join-Path $path 'admission.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{throw 'DC_PROBE_PAUSED: сборщик занят другой консолью; новый запуск не выполнен'}
 $name='DomainConsoleCSharp-Snapshot41'
 $task=Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction SilentlyContinue
 $out=Join-Path $path 'result.json';$latch=Join-Path $path 'blocked.flag'
 $cached=$null
 if(Test-Path -LiteralPath $out){try{$cached=Get-Content -LiteralPath $out -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop}catch{}}
 if($cached){$parsed=[DateTime]::MinValue;if([DateTime]::TryParse([string]$cached.CollectedUtc,[ref]$parsed) -and $cached.Reboot.CollectedUtc -and $cached.Activity.CollectedUtc){$age=([DateTime]::UtcNow-$parsed.ToUniversalTime()).TotalSeconds;if($age -ge 0 -and $age -lt 45){$cached.@@KIND@@|ConvertTo-Json -Depth 12 -Compress;return}}}
 if(Test-Path -LiteralPath $latch){throw 'DC_PROBE_PAUSED: предыдущая проверка не завершила уборку; автоматические проверки приостановлены. Откройте диагностику опроса'}
 if($task -and $task.State -in @('Running','Queued')){throw 'DC_PROBE_PAUSED: сборщик уже работает; новый запуск не выполнен'}
 $script=Join-Path $path 'collect.ps1';$exe=Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $argument='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$script+'"'
 if($task -and (@($task.Actions).Count -ne 1 -or $task.Actions[0].Execute -ne $exe -or $task.Actions[0].Arguments -ne $argument -or $task.Principal.UserId -notin @('SYSTEM','S-1-5-18') -or $task.Settings.MultipleInstances -ne 'IgnoreNew' -or [System.Xml.XmlConvert]::ToTimeSpan($task.Settings.ExecutionTimeLimit).TotalSeconds -ne 40)){throw 'DC_PROBE_PAUSED: параметры сборщика отличаются; автоматическая замена не выполнена'}
 [IO.File]::WriteAllText($latch,[DateTime]::UtcNow.ToString('o'))
 [IO.File]::WriteAllText($script,@@SCRIPT@@,(New-Object Text.UTF8Encoding($true)))
 Remove-Item -LiteralPath $out,(Join-Path $path 'result.tmp'),(Join-Path $path 'error.txt') -Force -ErrorAction SilentlyContinue
 if(!$task){$action=New-ScheduledTaskAction -Execute $exe -Argument $argument;$principal=New-ScheduledTaskPrincipal -UserId SYSTEM -LogonType ServiceAccount -RunLevel Highest;$settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromSeconds(40)) -MultipleInstances IgnoreNew -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries;Register-ScheduledTask -TaskName $name -TaskPath '\' -Action $action -Principal $principal -Settings $settings -Description 'DomainConsole 0.41 bounded read-only snapshot; no maintenance actions' -Force|Out-Null}
 Start-ScheduledTask -TaskName $name -TaskPath '\'
 $watch=[Diagnostics.Stopwatch]::StartNew()
 do{Start-Sleep -Milliseconds 250;$state=(Get-ScheduledTask -TaskName $name -TaskPath '\' -ErrorAction Stop).State;if((Test-Path -LiteralPath $out) -and $state -notin @('Running','Queued')){break}}while($watch.Elapsed.TotalSeconds -lt 45)
 if($state -in @('Running','Queued') -or !(Test-Path -LiteralPath $out)){throw 'DC_PROBE_PAUSED: сборщик не вернул полный снимок вовремя; повторные запуски остановлены'}
 $result=Get-Content -LiteralPath $out -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop
 if(!$result.Reboot.CollectedUtc -or !$result.Activity.CollectedUtc){throw 'DC_PROBE_PAUSED: снимок неполный; повторные запуски остановлены'}
 Remove-Item -LiteralPath $latch -Force -ErrorAction Stop
 $result.@@KIND@@|ConvertTo-Json -Depth 12 -Compress
}finally{if($lock){$lock.Dispose()}}
""";
 public async Task<string> ProbeInventory41(TargetRecord target,bool clean=false){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));return await PowerShellBridge.Execute("# DC-MANUAL41\n"+Invoke(target,LegacyProbeInventory41.Replace("@@CLEAN@@",clean?"$true":"$false")),timeout.Token);}
 public const string LegacyProbeInventory41="""
$clean=@@CLEAN@@
$root=Join-Path $env:ProgramData 'DomainConsoleCSharp';$base=Join-Path $root 'Probes'
$rows=@();$tasks=@(Get-ScheduledTask -TaskName 'DomainConsoleCSharp-Probe-*' -ErrorAction SilentlyContinue)
foreach($t in @($tasks|Select-Object -First 500)){
 $status='Пропущено';$reason='Принадлежность или возраст не подтверждены';$owned=$false;$when=$null
 try{
  $id=$t.TaskName.Substring('DomainConsoleCSharp-Probe-'.Length);$guid=[Guid]::Empty
  $principalSid=[string]$t.Principal.UserId;try{if($principalSid -ne 'S-1-5-18'){$principalSid=(New-Object Security.Principal.NTAccount($principalSid)).Translate([Security.Principal.SecurityIdentifier]).Value}}catch{$principalSid='unknown'};$action=@($t.Actions);$exe=Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
  $owned=$t.TaskPath -eq '\' -and [Guid]::TryParseExact($id,'D',[ref]$guid) -and $action.Count -eq 1 -and $action[0].Execute -eq $exe -and $action[0].Arguments -match '^-NoProfile -NonInteractive -EncodedCommand [A-Za-z0-9+/=]+$' -and $principalSid -eq 'S-1-5-18' -and [System.Xml.XmlConvert]::ToTimeSpan($t.Settings.ExecutionTimeLimit).TotalSeconds -eq 30
  if($owned){[xml]$xml=Export-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath -ErrorAction Stop;$date=[string]$xml.Task.RegistrationInfo.Date;if($date){$when=[DateTime]::Parse($date).ToUniversalTime()}}
  # No commands are decoded or executed. The exact legacy signature, registration
  # age and a second live state check are required; missing metadata is a skip.
  if($owned -and !$when){$last=(Get-ScheduledTaskInfo -TaskName $t.TaskName -TaskPath $t.TaskPath -ErrorAction Stop).LastRunTime;if($last.Year -ge 2000){$when=$last.ToUniversalTime()}}
  if($owned -and $when -and ([DateTime]::UtcNow-$when).TotalMinutes -ge 10 -and $t.State -in @('Ready','Disabled')){
   $status='Можно удалить';$reason='Завершённая старая Probe; сигнатура и возраст подтверждены'
   if($clean){$again=Get-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath -ErrorAction Stop;if($again.State -notin @('Ready','Disabled') -or $again.Actions[0].Arguments -ne $action[0].Arguments){$status='Пропущено';$reason='Состояние изменилось'}else{Unregister-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath -Confirm:$false -ErrorAction Stop;$status='Удалено';$dir=Join-Path $base $id;if(Test-Path -LiteralPath $dir){$item=Get-Item -LiteralPath $dir -Force;if(!($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){foreach($file in @(Get-ChildItem -LiteralPath $dir -Force)){if(!$file.PSIsContainer -and !($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -and $file.Name -in @('activity.json','activity.tmp','error.txt')){Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop}};if(!(Get-ChildItem -LiteralPath $dir -Force)){Remove-Item -LiteralPath $dir -Force -ErrorAction Stop}}}}}
  }elseif($t.State -in @('Running','Queued')){$reason='Проверка выполняется или ожидает запуска'}
 }catch{$status='Пропущено';$reason=$_.Exception.Message}
 $rows+=@{Task=$t.TaskName;State=[string]$t.State;Result=$status;Reason=$reason;RegisteredUtc=$(if($when){$when.ToString('o')}else{''})}
}
@{Total=$tasks.Count;Inspected=$rows.Count;Rows=@($rows);Note='Maintenance, задания обслуживания, процессы WinRM и каталоги не изменяются. Неизвестные задачи пропущены.'}|ConvertTo-Json -Depth 5 -Compress
""";
 public async Task ResumeProbe41(TargetRecord target){var result=await InspectSnapshot42(target,"Recover");if(!result.Contains("Recovered"))throw new IOException(result);}

}

// Admission happens before PowerShell/New-PSSession. Shared file locks also
// prevent separate console processes on this workstation from overlapping.
public static class TransportBudget41 {
 static readonly SemaphoreSlim slots=new(3);
 public static bool Paused {get;set;}=Store.Read("poll-paused41.json",false);
 public static string Key(TargetRecord t)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(t.Host.ToUpperInvariant()+"|"+ConnectionStore.Address(t)+"|"+ConnectionStore.Identity(t))));
 public static void Reset(TargetRecord t){string file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DomainConsole","Transport41",Key(t)+".blocked");if(File.Exists(file))File.Delete(file);}
 public static async Task<string> Run(string code,CancellationToken token,Func<CancellationToken,Task<string>> execute){
  var match=System.Text.RegularExpressions.Regex.Match(code,@"# DC-ENDPOINT41:([A-F0-9]{64})");
  if(!match.Success)return await execute(token);
  bool manual=code.Contains("# DC-MANUAL41\n",StringComparison.Ordinal);
  if(Paused&&!manual&&(PollingControl43.IsRead||code.Contains("# DC-READ42",StringComparison.Ordinal)))throw new IOException("DC_PROBE_PAUSED: служебные подключения приостановлены в этой консоли");
  string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DomainConsole","Transport41");Directory.CreateDirectory(dir);string path=Path.Combine(dir,match.Groups[1].Value);FileStream gate;
  try{gate=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){throw new PollWait42("Busy","Подключение к клиенту уже выполняется; повтор не создан",10);}
  using(gate){await slots.WaitAsync(token);try{token.ThrowIfCancellationRequested();if(Paused&&!manual&&PollingControl43.IsRead)throw new OperationCanceledException("Опрос выключен");string label=code.Contains("$dcJobId34=")?"Чтение состояния задания":code.Contains("# DC-READ42")?"Проверка состояния клиента":code.Contains("whoami;hostname")?"Проверка связи":"Получение данных WSUS";if(PollingControl43.IsRead)PollingControl43.Show(match.Groups[1].Value,label);return await execute(token);}finally{if(PollingControl43.IsRead)PollingControl43.Show(match.Groups[1].Value,"");slots.Release();}}
 }
}
