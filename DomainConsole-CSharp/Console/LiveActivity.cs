using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class LiveUpdateActivity {public bool InstallerBusy {get;set;}public bool Busy {get;set;}public List<string> Jobs {get;set;}=new();public List<BackgroundDownload> Downloads {get;set;}=new();public string Limitation {get;set;}="";public string InstallationEvidence {get;set;}="";}
public sealed class BackgroundDownload {public string Id {get;set;}="";public string State {get;set;}="";public long Received {get;set;}public long Total {get;set;}public string Error {get;set;}="";public string Code {get;set;}="";}
public sealed partial class RemoteService {
 public const string LiveUpdateScript="""
$root=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$jobs=@()
foreach($proc in @(Get-CimInstance Win32_Process -Filter "Name='DomainConsole.Agent.exe'")){
 if(!$proc.CommandLine -or $proc.CommandLine -notmatch '\brun\s+"([^\"]+)"'){continue}
 $folder=$matches[1];$id=Split-Path $folder -Leaf
 $guid=[Guid]::Empty;if(![Guid]::TryParse($id,[ref]$guid) -or !(Test-Path (Join-Path $folder 'job.json'))){continue}
 $job=Get-Content (Join-Path $folder 'job.json') -Raw -Encoding UTF8|ConvertFrom-Json
 $state=$null;if(Test-Path (Join-Path $folder 'status.json')){$state=Get-Content (Join-Path $folder 'status.json') -Raw -Encoding UTF8|ConvertFrom-Json}
 if($job.OutputChannel -eq 'WSUS' -and (!$state -or !$state.Ended -or $state.DiagnosticStatus -in @('Pending','Running','LegacyRunning'))){$jobs+= $id}
}
$busy=$false; $installerBusy=$false # This script is executed under local SYSTEM.
$downloads=@();$limitation=''
try {Import-Module BitsTransfer -ErrorAction Stop;foreach($b in @(Get-BitsTransfer -AllUsers -ErrorAction Stop|Where-Object {$_.DisplayName -eq 'WU Client Download'})){$downloads+=@{Id=[string]$b.JobId;State=[string]$b.JobState;Received=[long]$b.BytesTransferred;Total=[long]$b.BytesTotal;Error=[string]$b.ErrorDescription;Code='0x'+([uint32]([long]$b.Error.ErrorCode -band 0xffffffff)).ToString('X8')}}}catch{$limitation='BITS: '+$_.Exception.Message}
$busy=@($downloads|Where-Object {$_.State -in @('Connecting','Transferring','Queued','TransientError')}).Count -gt 0
$install=@(Get-CimInstance Win32_Process -Filter "Name='TiWorker.exe' OR Name='TrustedInstaller.exe' OR Name='MRT.exe'")
try{$installerBusy=[bool](New-Object -ComObject Microsoft.Update.Installer).IsBusy;$busy=$busy -or $installerBusy}catch{$limitation+='; WUA: '+$_.Exception.Message}
$evidence=if($install.Count){'Работают процессы обслуживания: '+(($install|Select-Object -ExpandProperty Name) -join ', ')+'. Установка обновления не подтверждена; процент недоступен.'}else{''}
@{InstallerBusy=$installerBusy;Busy=$busy;Jobs=@($jobs);Downloads=@($downloads);Limitation=$limitation;InstallationEvidence=$evidence}|ConvertTo-Json -Depth 5 -Compress
""";
 public async Task<LiveUpdateActivity> LiveUpdates(TargetRecord target){if(IsLocal(target.Host))RequireLocalAdmin();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
  return JsonSerializer.Deserialize<LiveUpdateActivity>(await PowerShellBridge.Execute(Invoke(target.Host,LiveProbeWrapper()),timeout.Token),Store.Options)??throw new Exception("Не получено состояние WSUS.");}
 public static string LiveProbeWrapper(){
  // WUA/BITS access is local SYSTEM, not a remoting token. One bounded task; always remove completed probes.
  var resultScript="$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';try{$value=& {\n"+LiveUpdateScript+"\n};[IO.File]::WriteAllText(($out+'.tmp'),[string]$value,(New-Object Text.UTF8Encoding($false)))}catch{[IO.File]::WriteAllText(($out+'.tmp'),(@{Limitation=$_.Exception.Message;Busy=$false;Jobs=@();Downloads=@()}|ConvertTo-Json -Compress),(New-Object Text.UTF8Encoding($false)))};Move-Item ($out+'.tmp') $out -Force";
  var wrapper="$root=Join-Path $env:ProgramData 'DomainConsoleCSharp';$probes=Join-Path $root 'Probes';New-Item $probes -ItemType Directory -Force|Out-Null;foreach($old in @(Get-ScheduledTask -TaskName 'DomainConsoleCSharp-Probe-*' -ErrorAction SilentlyContinue)){if($old.State -ne 'Running' -and (Test-Path (Join-Path $probes $old.TaskName.Substring('DomainConsoleCSharp-Probe-'.Length))) -and (Get-Item (Join-Path $probes $old.TaskName.Substring('DomainConsoleCSharp-Probe-'.Length))).CreationTimeUtc -lt [DateTime]::UtcNow.AddMinutes(-2)){Unregister-ScheduledTask $old.TaskName -Confirm:$false;Remove-Item (Join-Path $probes $old.TaskName.Substring('DomainConsoleCSharp-Probe-'.Length)) -Recurse -Force -ErrorAction SilentlyContinue}};$id=[Guid]::NewGuid().ToString();$path=Join-Path $probes $id;New-Item $path -ItemType Directory -Force|Out-Null;$out=Join-Path $path 'activity.json';$code='$out='+[char]39+$out.Replace([char]39,([string][char]39+[char]39))+[char]39+';'+"+PowerShellBridge.Literal(resultScript)+";$encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($code));$name='DomainConsoleCSharp-Probe-'+$id;$action=New-ScheduledTaskAction -Execute (Join-Path $env:windir 'System32\\WindowsPowerShell\\v1.0\\powershell.exe') -Argument ('-NoProfile -NonInteractive -EncodedCommand '+$encoded);$principal=New-ScheduledTaskPrincipal -UserId SYSTEM -LogonType ServiceAccount -RunLevel Highest;$settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::FromSeconds(30));try{Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Settings $settings -Force|Out-Null;Start-ScheduledTask $name;$clock=[Diagnostics.Stopwatch]::StartNew();while(!(Test-Path $out) -and $clock.Elapsed.TotalSeconds -lt 35){Start-Sleep -Milliseconds 200};if(!(Test-Path $out)){throw 'Снимок фоновой активности не получен за 35 секунд'};Get-Content $out -Raw -Encoding UTF8}finally{for($i=0;$i -lt 10 -and (Get-ScheduledTask $name -ErrorAction SilentlyContinue).State -eq 'Running';$i++){Start-Sleep -Milliseconds 100};if((Get-ScheduledTask $name -ErrorAction SilentlyContinue).State -ne 'Running'){Unregister-ScheduledTask $name -Confirm:$false -ErrorAction SilentlyContinue;Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue}}";
  return wrapper;}

}
public partial class MainWindow {
 readonly Dictionary<string,string> preflightErrors=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,LiveUpdateActivity> backgroundUpdates=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,(long Bytes,DateTimeOffset At,DateTimeOffset Changed,double Speed)> backgroundSamples=new(StringComparer.OrdinalIgnoreCase);
 bool backgroundReading;
 async Task RefreshBackground(){if(backgroundReading||ComputerGrid.SelectedItem is not ComputerRow row||closing)return;backgroundReading=true;try{var live=await remote.LiveUpdates(row.Target);backgroundUpdates[row.Host]=live;foreach(var d in live.Downloads){var key=row.Host+"|"+d.Id;var now=DateTimeOffset.UtcNow;var speed=0.0;var changed=now;if(backgroundSamples.TryGetValue(key,out var old)){speed=d.Received>=old.Bytes?(d.Received-old.Bytes)/Math.Max(1,(now-old.At).TotalSeconds):0;changed=d.Received==old.Bytes?old.Changed:now;}backgroundSamples[key]=(d.Received,now,changed,speed);}if(IsCurrent(row))UpdateWsusStage();}catch(Exception ex){backgroundUpdates[row.Host]=new(){Limitation="Не удалось проверить фоновую активность: "+ex.Message};if(IsCurrent(row))UpdateWsusStage();}finally{backgroundReading=false;}}
 string BackgroundText(string host,LiveUpdateActivity live){var lines=new List<string>();foreach(var d in live.Downloads){var key=host+"|"+d.Id;var progress=d.Total>0&&d.Received<=d.Total?((double)d.Received*100/d.Total).ToString("F1")+"%":"процент уточняется";var text="Windows Update · BITS "+d.State+" · "+(d.Received/1048576.0).ToString("F1")+" / "+(d.Total/1048576.0).ToString("F1")+" МиБ · "+progress;if(backgroundSamples.TryGetValue(key,out var sample))text+=" · снимок "+sample.At.ToLocalTime().ToString("HH:mm:ss")+" · "+(sample.Speed/1024).ToString("F1")+" КиБ/с · без изменения "+(int)(DateTimeOffset.UtcNow-sample.Changed).TotalMinutes+" мин";if(d.Error.Length>0){if(d.Error.Contains("стоимости",StringComparison.OrdinalIgnoreCase)||d.Error.Contains("cost",StringComparison.OrdinalIgnoreCase))text+=" · Проверьте лимитное подключение и политику BITS; очистка кэша не устраняет этот запрет.";text+=" · "+d.Error;if(uint.TryParse(d.Code.Replace("0x",""),System.Globalization.NumberStyles.HexNumber,null,out var errorCode))text+=" · "+ExecutionDiagnosis.Recommend(unchecked((int)errorCode));}lines.Add(text);}if(live.InstallerBusy)lines.Add("Установщик Windows Update занят · процент внешней установки недоступен.");if(live.InstallationEvidence.Length>0)lines.Add(live.InstallationEvidence);if(live.Limitation.Length>0)lines.Add(live.Limitation);return string.Join("\n",lines);}
 public static async Task<(ComputerRow Row,LiveUpdateActivity? Live,string Error)[]> ProbeBatch(IEnumerable<ComputerRow> rows,Func<TargetRecord,Task<LiveUpdateActivity>> probe){using var gate=new SemaphoreSlim(8);return await Task.WhenAll(rows.Select(async row=>{await gate.WaitAsync();try{return (Row:row,Live:(LiveUpdateActivity?)await probe(row.Target),Error:"");}catch(Exception ex){return (Row:row,Live:(LiveUpdateActivity?)null,Error:ex.Message);}finally{gate.Release();}}));}
 async Task<bool> HasLiveWsus(List<ComputerRow> selected){bool active=false;preflightErrors.Clear();
  var probes=await ProbeBatch(selected,remote.LiveUpdates);
  foreach(var probe in probes){var row=probe.Row;if(probe.Live==null){preflightErrors[row.Host]=probe.Error;continue;}var live=probe.Live;backgroundUpdates[row.Host]=live;active|=live.Busy||live.Jobs.Count>0||queue.Any(q=>q.Target.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)&&IsWsusJob(q.Job));
   foreach(var job in History.Where(IsWsusJob)){var target=job.Targets.FirstOrDefault(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));if(target==null||target.Status is not ("Queued" or "Submitted" or "Running")||live.Jobs.Contains(job.Id)||queue.Any(q=>q.Job.Id==job.Id&&q.Target.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)))continue;
    try{var data=await remote.Poll(target,job.Id);if(data.AgentAlive||data.TaskState=="Running"){active=true;continue;}if(data.State!=null&&!string.IsNullOrEmpty(data.State.Ended)){target.State=data.State;target.Status=data.State.Status;}else{target.Status="InterventionRequired";target.State??=new();target.State.Status="InterventionRequired";target.State.Error="Исполнитель отсутствует; исход задания неизвестен. Требуется диагностика.";}Sync(job);}catch(Exception ex){preflightErrors[row.Host]=ex.Message;}
   }
  }
  Save();return active;
 }
}
