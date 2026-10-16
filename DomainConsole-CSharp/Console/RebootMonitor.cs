using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class RebootWatch {public string JobId {get;set;}="";public string RequestedUtc {get;set;}="";public string LastError {get;set;}="";public DateTimeOffset Next {get;set;}public DateTimeOffset Until {get;set;}}
public sealed class RebootSnapshot {public List<string> Sources {get;set;}=new();public string BootUtc {get;set;}="";public string CollectedUtc {get;set;}="";public bool? Required {get;set;}public string Error {get;set;}="";}
public partial class MainWindow {
 readonly Dictionary<string,RebootWatch> rebootWatches=new(Store.Read("reboot-watches.json",new Dictionary<string,RebootWatch>()),StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,RebootSnapshot> rebootSnapshots=new(Store.Read("reboot-snapshots.json",new Dictionary<string,RebootSnapshot>()),StringComparer.OrdinalIgnoreCase);
 bool checkingReboots;
 static bool RebootReturned(RebootWatch watch,RebootSnapshot snapshot)=>snapshot.Required.HasValue&&DateTimeOffset.TryParse(watch.RequestedUtc,out var sent)&&DateTimeOffset.TryParse(snapshot.BootUtc,out var boot)&&boot>sent;
 static bool ObservedAfter(RebootSnapshot snapshot,string stamp)=>DateTimeOffset.TryParse(snapshot.CollectedUtc,out var observed)&&DateTimeOffset.TryParse(stamp,out var earlier)&&observed>=earlier;
 string RebootReason(ComputerRow row){
  var evidence=new List<(DateTimeOffset At,bool Needed,string Reason)>();
  void Add(string stamp,bool needed,string reason){DateTimeOffset.TryParse(stamp,out var at);evidence.Add((at,needed,reason));}
  reportCache.TryGetValue(row.Host,out var cache);
  var job=History.FirstOrDefault(j=>IsWsusJob(j)&&j.Targets.Any(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)));var target=job?.Targets.First(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));
  bool required=target?.State?.RebootRequired==true||target?.Status is "AwaitingReboot" or "RebootScheduled";
  if(target?.State!=null&&(required||(job!.Steps.Any(t=>t.Kind is "Updates" or "UpdateScan")&&target.Status is "Completed" or "VerifiedAfterReboot")))Add(target.State.Updated.Length>0?target.State.Updated:target.State.Ended,required,target.Status=="RebootScheduled"?"Перезагрузка назначена заданием WSUS":"Клиент сообщил: нужна перезагрузка");
  foreach(var recorded in History.SelectMany(j=>j.Targets).Where(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)&&t.State?.RebootEvidence?.Required.HasValue==true)){var proof=recorded.State!.RebootEvidence;Add(proof.CollectedUtc,proof.Required!.Value,"Клиент: нужна перезагрузка · "+string.Join("; ",proof.Sources)+" · "+LocalDateConverter.Format(proof.CollectedUtc));}
  if(rebootSnapshots.TryGetValue(row.Host,out var observed)&&observed.Required.HasValue)Add(observed.CollectedUtc,observed.Required.Value,"Клиент подтвердил необходимость перезагрузки · "+string.Join("; ",observed.Sources)+" · "+LocalDateConverter.Format(observed.CollectedUtc));
  var latest=evidence.OrderByDescending(e=>e.At).FirstOrDefault();if(latest.Needed)return latest.Reason;if(cache!=null&&CacheMatches(cache)&&cache.Found&&cache.Report.InstalledPendingRebootCount>0&&(!DateTimeOffset.TryParse(cache.Report.LastReportedUtc,out var reported)||latest.At<reported))return "По отчёту WSUS нужна перезагрузка · "+LocalDateConverter.Format(cache.Report.LastReportedUtc);return "";
 }
 void WatchReboot(RemoteJob job,string host,int delaySeconds){rebootWatches[host]=new(){JobId=job.Id,RequestedUtc=job.Created,Next=DateTimeOffset.UtcNow.AddSeconds(delaySeconds+15),Until=DateTimeOffset.UtcNow.AddDays(2)};Store.Write("reboot-watches.json",rebootWatches);}
 async Task MonitorReboots(){if(checkingReboots||closing||rebootWatches.Count==0)return;checkingReboots=true;try{foreach(var entry in rebootWatches.Where(x=>x.Value.Next<=DateTimeOffset.UtcNow).Take(2).ToList()){
   if(closing)return;var host=entry.Key;var watch=entry.Value;var job=History.FirstOrDefault(j=>j.Id==watch.JobId);var target=job?.Targets.FirstOrDefault(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase));
   if(target?.Status=="Cancelled"){rebootWatches.Remove(host);Store.Write("reboot-watches.json",rebootWatches);continue;}
   if(DateTimeOffset.UtcNow>watch.Until){rebootWatches.Remove(host);Store.Write("reboot-watches.json",rebootWatches);if(CurrentHost(host))Notify("Возврат "+host+" после перезагрузки за 48 часов не подтверждён. Проверьте подключение.");continue;}
   watch.Next=DateTimeOffset.UtcNow.AddMinutes(1);Store.Write("reboot-watches.json",rebootWatches);
   try{
    var snapshot=await remote.ReadRebootState(target??new TargetRecord{Host=host});if(closing)return;ConnectionSucceeded(host);
    if(!RebootReturned(watch,snapshot)){if(CurrentHost(host)&&snapshot.Error.Length>0&&snapshot.Error!=watch.LastError)Notify("Флаг перезагрузки "+host+" прочитать не удалось: "+snapshot.Error);watch.LastError=snapshot.Error;continue;}
    rebootSnapshots[host]=snapshot;Store.Write("reboot-snapshots.json",rebootSnapshots);rebootWatches.Remove(host);Store.Write("reboot-watches.json",rebootWatches);
    // Read server data separately; never start a client scan or repair from this monitor.
    try{var settings=ReadWsus();var reply=(await remote.WsusOverview(settings,new(){host},CancellationToken.None)).SingleOrDefault();if(reply?.Found==true&&reply.Report!=null&&reply.Error.Length==0){var started=reportCache.GetValueOrDefault(host)?.Started??"";SaveReportCache(host,reply.Report,settings.Host+":"+settings.Port,started,false);serverReports[host]=reply.Report.Format(settings.Host+":"+settings.Port,started);}}
    catch(Exception ex){overviewErrors[host]=ex.Message;}
    RefreshOutcomes();if(CurrentHost(host)){ShowCurrent();Notify(host+": возврат после перезагрузки подтверждён. "+(snapshot.Required==true?"Клиенту ещё требуется перезагрузка.":"Клиент больше не требует перезагрузки."));}
   }catch(Exception ex){if(!closing){ConnectionFailed(host,ex.Message);RefreshOutcomes();}}
  }}finally{checkingReboots=false;}}
 bool CurrentHost(string host)=>(ComputerGrid.SelectedItem as ComputerRow)?.Host.Equals(host,StringComparison.OrdinalIgnoreCase)==true;
}
public sealed partial class RemoteService {
 public Task<RebootSnapshot> ReadRebootState(string host)=>ReadRebootState(new TargetRecord{Host=host});
 public async Task<RebootSnapshot> ReadRebootState(TargetRecord target){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));var json=await PowerShellBridge.Execute(Invoke(target,RebootStateScript),timeout.Token);return JsonSerializer.Deserialize<RebootSnapshot>(json,Store.Options)??throw new Exception("Пустой ответ состояния перезагрузки");}
 public const string RebootStateScript="""
$os=Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
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
@{BootUtc=$os.LastBootUpTime.ToUniversalTime().ToString('o');CollectedUtc=[DateTime]::UtcNow.ToString('o');Required=$required;Sources=@($sources);Error=($errors -join '; ')}|ConvertTo-Json -Compress

""";
}
