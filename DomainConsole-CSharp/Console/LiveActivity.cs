using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class LiveUpdateActivity {public string PolicySource {get;set;}="";public string CollectedUtc {get;set;}="";public bool InstallerBusy {get;set;}public bool Busy {get;set;}public List<string> Jobs {get;set;}=new();public List<BackgroundDownload> Downloads {get;set;}=new();public string Limitation {get;set;}="";public string InstallationEvidence {get;set;}="";}
public sealed class BackgroundDownload {public string Label=>System.IO.Path.GetFileName(LocalName)+" · "+State+" · "+Priority+" · "+Id;public string Id {get;set;}="";public string State {get;set;}="";public long Received {get;set;}public long? Total {get;set;}public string Priority {get;set;}="";public string CreatedUtc {get;set;}="";public string ModifiedUtc {get;set;}="";public string RemoteName {get;set;}="";public string LocalName {get;set;}="";public bool Accelerated {get;set;}public string Error {get;set;}="";public string Code {get;set;}="";}
public sealed partial class RemoteService {
 public const string LiveUpdateScript="""
$root=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$jobs=@();$scanWarnings=@()
foreach($proc in @(Get-CimInstance Win32_Process -Filter "Name='DomainConsole.Agent.exe'")){
 if(!$proc.CommandLine -or $proc.CommandLine -notmatch '\brun\s+"([^\"]+)"'){continue}
 $folder=$matches[1];$id=Split-Path $folder -Leaf
 $guid=[Guid]::Empty;if(![Guid]::TryParse($id,[ref]$guid) -or !(Test-Path (Join-Path $folder 'job.json'))){continue}
 try{$job=Get-Content (Join-Path $folder 'job.json') -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop}catch{$scanWarnings+=('Некорректный снимок задания '+$id);continue}
 $state=$null;if(Test-Path (Join-Path $folder 'status.json')){try{$state=Get-Content (Join-Path $folder 'status.json') -Raw -Encoding UTF8|ConvertFrom-Json}catch{$state=$null}}
 if($job.OutputChannel -eq 'WSUS' -and (!$state -or !$state.Ended -or $state.DiagnosticStatus -in @('Pending','Running','LegacyRunning'))){$jobs+= $id}
}
$busy=$false; $installerBusy=$false # This script is executed under local SYSTEM.
$downloads=@();$limitation=$scanWarnings -join '; '
function Dc-Time($v){if($v -is [DateTime]){$v.ToUniversalTime().ToString('o')}else{''}}
try {Import-Module BitsTransfer -ErrorAction Stop;foreach($b in @(Get-BitsTransfer -AllUsers -ErrorAction Stop|Where-Object {$_.DisplayName -eq 'WU Client Download'})){try{$total=$null;if($null -ne $b.BytesTotal -and [decimal]$b.BytesTotal -lt [decimal][long]::MaxValue){$total=[long]$b.BytesTotal};$file=@($b.FileList)|Select-Object -First 1;$downloads+=@{Id=[string]$b.JobId;State=[string]$b.JobState;Received=[long]$b.BytesTransferred;Total=$total;Priority=[string]$b.Priority;CreatedUtc=(Dc-Time $b.CreationTime);ModifiedUtc=(Dc-Time $b.ModificationTime);RemoteName=[string]$file.RemoteName;LocalName=[string]$file.LocalName;Accelerated=(Test-Path (Join-Path (Join-Path $root 'Controls') ('priority-'+$b.JobId+'.json')));Error=[string]$b.ErrorDescription;Code='0x'+([int]$b.Error.ErrorCode).ToString('X8')}}catch{$limitation+='BITS '+$b.JobId+': '+$_.Exception.Message+'; '}}}catch{$limitation+='; BITS: '+$_.Exception.Message}
$busy=@($downloads|Where-Object {$_.State -in @('Connecting','Transferring','Queued','TransientError')}).Count -gt 0
$install=@(Get-CimInstance Win32_Process -Filter "Name='TiWorker.exe' OR Name='TrustedInstaller.exe' OR Name='MRT.exe'")
try{$installerBusy=[bool](New-Object -ComObject Microsoft.Update.Installer).IsBusy;$busy=$busy -or $installerBusy}catch{$limitation+='; WUA: '+$_.Exception.Message}
$evidence=if($install.Count){'Работают процессы обслуживания: '+(($install|Select-Object -ExpandProperty Name) -join ', ')+'. Установка обновления не подтверждена; процент недоступен.'}else{''}
$policySource=[string](Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate' -ErrorAction SilentlyContinue).WUServer
@{PolicySource=$policySource;CollectedUtc=[DateTime]::UtcNow.ToString('o');InstallerBusy=$installerBusy;Busy=$busy;Jobs=@($jobs);Downloads=@($downloads);Limitation=$limitation;InstallationEvidence=$evidence}|ConvertTo-Json -Depth 5 -Compress
""";
 public async Task<LiveUpdateActivity> LiveUpdates(TargetRecord target){if(Local(target))RequireLocalAdmin();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(95));
  return ParseData34<LiveUpdateActivity>(await SnapshotPart42(target,"Activity",timeout.Token),"фоновая активность");}
 public static string LiveProbeWrapper(string? collector=null)=>SnapshotWrapper41(collector!=null);


}
public partial class MainWindow {
 readonly Dictionary<string,string> preflightErrors=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,LiveUpdateActivity> backgroundUpdates=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,(long Bytes,DateTimeOffset At,DateTimeOffset Changed,double Speed)> backgroundSamples=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,DateTimeOffset> backgroundReads=new(StringComparer.OrdinalIgnoreCase);
 bool backgroundReading; public static bool FreshActivity(LiveUpdateActivity live)=>DateTimeOffset.TryParse(live.CollectedUtc,out var at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromSeconds(90);
 async Task RefreshBackground(){if(backgroundReading||ComputerGrid.SelectedItem is not ComputerRow row||closing)return;if(!RemoteService.SnapshotDue42(ConnectionStore.Capture(row.Target))||!ReadDue34(row.Host+"|фоновая активность"))return;if(backgroundReads.TryGetValue(row.Host,out var last)&&DateTimeOffset.UtcNow-last<TimeSpan.FromSeconds(60))return;backgroundReads[row.Host]=DateTimeOffset.UtcNow;backgroundReading=true;var started38=DateTimeOffset.UtcNow;try{var live=await remote.LiveUpdates(row.Target);backgroundUpdates[row.Host]=live;ReadSucceeded34(row.Host,"фоновая активность");if(FreshActivity(live))ConnectionSucceeded(row.Host,started38);foreach(var d in live.Downloads){var key=row.Host+"|"+d.Id;var now=DateTimeOffset.UtcNow;var speed=0.0;var changed=now;if(backgroundSamples.TryGetValue(key,out var old)){speed=d.Received>=old.Bytes?(d.Received-old.Bytes)/Math.Max(1,(now-old.At).TotalSeconds):0;changed=d.Received==old.Bytes?old.Changed:now;}backgroundSamples[key]=(d.Received,now,changed,speed);ObserveBits(row.Host,d,now);}Store.Write("bits-observations.json",bitsObservations);if(IsCurrent(row))UpdateWsusStage();RefreshActivity();await MaybeRecoverDownload(row,live);}catch(Exception ex){ReadFailed34(row.Host,"фоновая активность",ex);if(backgroundUpdates.TryGetValue(row.Host,out var previous))previous.Limitation="Снимок не обновлён: "+ShortConnectionError(ex.Message);else backgroundUpdates[row.Host]=new(){Limitation="Снимок не получен: "+ShortConnectionError(ex.Message)};if(IsCurrent(row))UpdateWsusStage();RefreshActivity();}finally{backgroundReading=false;}}
 string BackgroundText(string host,LiveUpdateActivity live){var lines=new List<string>();foreach(var d in live.Downloads.Take(8)){var key=host+"|"+d.Id;var progress=d.Total>0&&d.Received<=d.Total?((double)d.Received*100/d.Total.Value).ToString("F1")+"%":"размер неизвестен";var label=d.State switch{"Queued"=>"В очереди · передача не подтверждена","Transferring"=>"Передача данных","Connecting"=>"Подключение","TransientError"=>"Ошибка · ожидается повтор","Suspended"=>"Приостановлено","Error"=>"Ошибка",_=>d.State};var text="BITS · "+label+" · "+(d.Received/1048576.0).ToString("F1")+" / "+(d.Total.HasValue?(d.Total.Value/1048576.0).ToString("F1"):"?")+" МиБ · "+progress+" · "+d.Priority+(d.Accelerated?" · ускоренный режим":"");if(DateTimeOffset.TryParse(d.CreatedUtc,out var created))text+=" · возраст задания "+(DateTimeOffset.UtcNow-created).TotalHours.ToString("F1")+" ч";if(bitsObservations.TryGetValue(key,out var sample))text+=" · снимок "+sample.Seen.ToLocalTime().ToString("HH:mm:ss")+" · "+(sample.Speed/1024).ToString("F1")+" КиБ/с · без изменения при наблюдении "+(int)(sample.Seen-sample.Changed).TotalMinutes+" мин";if(d.Error.Length>0)text+=" · "+d.Error;lines.Add(text);}if(live.Downloads.GroupBy(d=>d.RemoteName+"|"+d.LocalName).Any(g=>g.Count()>1&&g.Key!="|"))lines.Add("Есть задания с одинаковыми адресами и путями. Автоматическое удаление не выполняется.");if(live.InstallerBusy)lines.Add("Установщик Windows Update занят · процент внешней установки недоступен.");if(live.InstallationEvidence.Length>0)lines.Add(live.InstallationEvidence);if(live.Limitation.Length>0)lines.Add(live.Limitation);return string.Join("\n",lines);}
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

