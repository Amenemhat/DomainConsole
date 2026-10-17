using System.IO;
using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class RebootWatch {public bool Confirmed {get;set;}public bool Scan {get;set;}public bool Done {get;set;}public string Phase {get;set;}="";public string FollowupId {get;set;}="";public string BeforeBootUtc {get;set;}="";public DateTimeOffset ExpectedUtc {get;set;}public DateTimeOffset ReadyAfter {get;set;}public string JobId {get;set;}="";public string RequestedUtc {get;set;}="";public string LastError {get;set;}="";public DateTimeOffset Next {get;set;}public DateTimeOffset Until {get;set;}}
public sealed class RebootSnapshot {public string UptimeReceivedUtc {get;set;}="";public string UptimeCollectedUtc {get;set;}="";public double? UptimeSeconds {get;set;}public string ClientUtc {get;set;}="";public bool? PreservedRequired {get;set;}public string PreservedUtc {get;set;}="";public List<string> Sources {get;set;}=new();public string BootUtc {get;set;}="";public string CollectedUtc {get;set;}="";public bool? Required {get;set;}public string Error {get;set;}="";}
public partial class MainWindow {
 readonly Dictionary<string,RebootWatch> rebootWatches=new(Store.Read("reboot-watches.json",new Dictionary<string,RebootWatch>()),StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,RebootSnapshot> rebootSnapshots=new(Store.Read("reboot-snapshots.json",new Dictionary<string,RebootSnapshot>()),StringComparer.OrdinalIgnoreCase);
 bool checkingReboots;
 static bool RebootReturned(RebootWatch watch,RebootSnapshot snapshot)=>snapshot.Required.HasValue&&DateTimeOffset.TryParse(watch.RequestedUtc,out var sent)&&DateTimeOffset.TryParse(snapshot.BootUtc,out var boot)&&boot>sent&&(watch.BeforeBootUtc.Length==0||snapshot.BootUtc!=watch.BeforeBootUtc);
 static bool ObservedAfter(RebootSnapshot snapshot,string stamp)=>DateTimeOffset.TryParse(snapshot.CollectedUtc,out var observed)&&DateTimeOffset.TryParse(stamp,out var earlier)&&observed>=earlier;
 string RebootReason(ComputerRow row){
  var evidence=new List<(DateTimeOffset At,bool Needed,string Reason)>();
  void Add(string stamp,bool needed,string reason){DateTimeOffset.TryParse(stamp,out var at);evidence.Add((at,needed,reason));}
  reportCache.TryGetValue(row.Host,out var cache);
  var job=History.FirstOrDefault(j=>IsWsusJob(j)&&j.Targets.Any(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)));var target=job?.Targets.First(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));
  bool required=target?.State?.RebootRequired==true||target?.Status is "AwaitingReboot" or "RebootScheduled";
  if(target?.State!=null&&(required||(job!.Steps.Any(t=>t.Kind is "Updates" or "UpdateScan")&&target.Status is "Completed" or "VerifiedAfterReboot")))Add(target.State.Updated.Length>0?target.State.Updated:target.State.Ended,required,target.Status=="RebootScheduled"?"Перезагрузка назначена заданием WSUS":"Клиент сообщил: нужна перезагрузка");
  foreach(var recorded in History.SelectMany(j=>j.Targets).Where(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)&&CurrentIdentity(t)&&t.State?.RebootEvidence?.Required.HasValue==true)){var proof=recorded.State!.RebootEvidence;Add(proof.CollectedUtc,proof.Required!.Value,"Клиент: нужна перезагрузка · "+string.Join("; ",proof.Sources)+" · "+LocalDateConverter.Format(proof.CollectedUtc));}
  if(rebootSnapshots.TryGetValue(row.Host,out var observed)&&(observed.Required.HasValue||observed.PreservedRequired.HasValue))Add(observed.Required.HasValue?observed.CollectedUtc:observed.PreservedUtc,(observed.Required??observed.PreservedRequired)!.Value,"Клиент подтвердил необходимость перезагрузки · "+string.Join("; ",observed.Sources)+" · "+LocalDateConverter.Format(observed.Required.HasValue?observed.CollectedUtc:observed.PreservedUtc)+(observed.Required.HasValue?"":" · прежнее подтверждение; новый снимок неполный"));
  var latest=evidence.OrderByDescending(e=>e.At).FirstOrDefault();if(latest.Needed)return latest.Reason;if(cache!=null&&CacheMatches(cache)&&cache.Found&&cache.Report.InstalledPendingRebootCount>0&&(!DateTimeOffset.TryParse(cache.Report.LastReportedUtc,out var reported)||latest.At<reported))return "По отчёту WSUS нужна перезагрузка · "+LocalDateConverter.Format(cache.Report.LastReportedUtc);return "";
 }
 void WatchReboot(RemoteJob job,string host,int delaySeconds){rebootWatches[host]=new(){JobId=job.Id,RequestedUtc=job.Created,Next=DateTimeOffset.UtcNow.AddSeconds(delaySeconds+15),Until=DateTimeOffset.UtcNow.AddDays(2)};Store.Write("reboot-watches.json",rebootWatches);}

 bool CurrentHost(string host)=>(ComputerGrid.SelectedItem as ComputerRow)?.Host.Equals(host,StringComparison.OrdinalIgnoreCase)==true;
}
public sealed partial class RemoteService {
 public Task<RebootSnapshot> ReadRebootState(string host)=>ReadRebootState(new TargetRecord{Host=host});
 public async Task<RebootSnapshot> ReadRebootState(TargetRecord target,CancellationToken cancellation=default){using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(25));if(!Local(target)){var captured=ConnectionStore.Capture(target);var address=ConnectionStore.Address(captured);using var tcp=new System.Net.Sockets.TcpClient();using var quick=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);quick.CancelAfter(TimeSpan.FromSeconds(3));try{await tcp.ConnectAsync(address.Length>0?address:target.Host,ConnectionStore.Ssl(captured)?5986:5985,quick.Token);}catch(Exception ex){throw new IOException("TCP WinRM недоступен: "+ex.Message);}}var json=await PowerShellBridge.Execute(Invoke(target,RebootStateScript),timeout.Token);return JsonSerializer.Deserialize<RebootSnapshot>(json,Store.Options)??throw new Exception("Пустой ответ состояния перезагрузки");}
 public const string RebootStateScript="""
$os=Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
$clientNow=[DateTime]::UtcNow;$uptime=($clientNow-$os.LastBootUpTime.ToUniversalTime()).TotalSeconds;if($uptime -lt 0){$uptime=$null}
$sources=@();$errors=@()
try{if((New-Object -ComObject Microsoft.Update.SystemInfo).RebootRequired){$sources+='Windows Update API'}}catch{$errors+=('WUA: '+$_.Exception.Message)}
$root=$null
try{
 $view=if([Environment]::Is64BitOperatingSystem){[Microsoft.Win32.RegistryView]::Registry64}else{[Microsoft.Win32.RegistryView]::Registry32}
 $root=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,$view)
 foreach($path in @('SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired','SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending')){
  $key=$null;try{$key=$root.OpenSubKey($path);if($key){$sources+=$path}}catch{$errors+=($path+': '+$_.Exception.Message)}finally{if($key){$key.Dispose()}}
 }
 $key=$null;try{$key=$root.OpenSubKey('SYSTEM\CurrentControlSet\Control\Session Manager');if(!$key){throw 'Session Manager unavailable'};if(@($key.GetValue('PendingFileRenameOperations')|Where-Object {![string]::IsNullOrWhiteSpace($_)}).Count){$sources+='PendingFileRenameOperations'}}catch{$errors+=('Session Manager: '+$_.Exception.Message)}finally{if($key){$key.Dispose()}}
}catch{$errors+=('Registry: '+$_.Exception.Message)}finally{if($root){$root.Dispose()}}
$required=if($sources.Count){$true}elseif($errors.Count){$null}else{$false}
@{ClientUtc=$clientNow.ToString('o');UptimeSeconds=$uptime;BootUtc=$os.LastBootUpTime.ToUniversalTime().ToString('o');CollectedUtc=[DateTime]::UtcNow.ToString('o');Required=$required;Sources=@($sources);Error=($errors -join '; ')}|ConvertTo-Json -Compress

""";
}
