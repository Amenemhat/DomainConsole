using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class IdentityServerReply58 {
 public List<string> Names {get;set;}=new();
 public string LookupError {get;set;}="";
 public string Id {get;set;}="";
 public string Name {get;set;}="";
 public string ReportedUtc {get;set;}="";
 public List<string> Groups {get;set;}=new();
 public string GroupError {get;set;}="";
}
public partial class MainWindow {
 readonly HashSet<string> identityChecks58=new(StringComparer.OrdinalIgnoreCase);
 bool readingManualRepairs;
 async Task ObserveManualRepairs58(){
  if(readingManualRepairs||closing||App.RecoveryPending57)return;
  readingManualRepairs=true;
  using var manual=PollingControl43.Manual56("identity");
  try{
   while(!closing&&!App.RecoveryPending57){
    var checkedSources=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    var now=DateTimeOffset.UtcNow;
    var pending=identityRepairs58.Values.Where(a=>a.Manual&&a.JobId.Length>0&&a.Phase!="Подтверждено"&&DateTimeOffset.TryParse(a.DeadlineUtc,out var limit)&&limit>now&&!RecordIdentityReadinessFailure(a)).ToList();
    if(pending.Count==0)break;
    foreach(var attempt in pending){
     if(closing||App.RecoveryPending57)break;
     if(surveyRun!=null||identityChecks58.Contains(attempt.Host)||DateTimeOffset.TryParse(attempt.LastClientReadUtc,out var previous)&&DateTimeOffset.UtcNow-previous<TimeSpan.FromMinutes(1))continue;
     var row=Computers.FirstOrDefault(r=>r.Host.Equals(attempt.Host,StringComparison.OrdinalIgnoreCase));
     if(row==null||row.Target.AdDisabled||!ConnectionStore.CanConnect(row.Target))continue;
     var target=ConnectionStore.Capture(row.Target);
     attempt.LastClientReadUtc=DateTimeOffset.UtcNow.ToString("o");Store.Write("identity-repairs58.json",identityRepairs58);
     try{
      // This is observation of a manually accepted job, including when background polling is paused.
      var submitted=IdentityRepairTarget(attempt);
      if(submitted is {Status:"Submitted" or "SubmissionUnknown" or "Running"}&&CurrentEndpoint(submitted)&&CurrentIdentity(submitted))
       await Poll(new ComputerRow(submitted,attempt.JobId),afterLink:true);
      if(closing||!CurrentEndpoint(target)||!CurrentIdentity(target)||RecordIdentityReadinessFailure(attempt))continue;
      var observed=await remote.ReadIdentity(target,CancellationToken.None);
      await ResolveIdentitySource(observed,ReadWsus(),checkedSources,CancellationToken.None);
      if(closing||!CurrentEndpoint(target)||!CurrentIdentity(target))continue;
      ObserveIdentity58(row.Host,observed,false);
      await VerifyIdentityRegistration58(row.Host);
     }catch(Exception ex){if(!closing)Event31(row.Host,"identity-registration-read58",attempt.JobId,"Проверка регистрации ожидает связи","Warning",ConnectionBrief31(ex.Message),scope:attempt.JobId);}
     refreshSurvey?.Invoke();
    }
    await Task.Delay(TimeSpan.FromSeconds(30));
   }
  }finally{readingManualRepairs=false;if(!closing){ExpireIdentityRepairs58();refreshSurvey?.Invoke();}}
 }
 TargetRecord? IdentityRepairTarget(IdentityRepairAttempt58 attempt)=>History.FirstOrDefault(j=>j.Id==attempt.JobId)?.Targets.FirstOrDefault(t=>t.Host.Equals(attempt.Host,StringComparison.OrdinalIgnoreCase));
 bool RecordIdentityReadinessFailure(IdentityRepairAttempt58 attempt){
  if(attempt.Phase.StartsWith("Перерегистрация не начата",StringComparison.Ordinal))return true;
  var target=IdentityRepairTarget(attempt);
  if(target==null||!IdentityRepairNotStarted(target))return false;
  var error=target.State?.Error??"";
  if(error.StartsWith(IdentityNotStarted,StringComparison.Ordinal))error=error[IdentityNotStarted.Length..];
  attempt.Phase="Перерегистрация не начата · "+(error.Length>0?error:ComputerRow.StatusText(target.Status));
  attempt.At=DateTimeOffset.UtcNow.ToString("o");
  Store.Write("identity-repairs58.json",identityRepairs58);return true;
 }
 void ExpireIdentityRepairs58(){
  bool changed=false;var now=DateTimeOffset.UtcNow;
  foreach(var attempt in identityRepairs58.Values.Where(a=>a.JobId.Length>0&&a.Phase!="Подтверждено")){
   if(RecordIdentityReadinessFailure(attempt))continue;
   if(attempt.StartedUtc.Length==0)attempt.StartedUtc=attempt.At;
   if(attempt.DeadlineUtc.Length==0&&DateTimeOffset.TryParse(attempt.StartedUtc,out var start)){attempt.DeadlineUtc=start.AddMinutes(10).ToString("o");changed=true;}
   var target=IdentityRepairTarget(attempt);
   var localFailure=IdentityRepairFailure(target);
   if(localFailure.Length==0&&(!DateTimeOffset.TryParse(attempt.DeadlineUtc,out var deadline)||now<deadline))continue;
   var expired=localFailure.Length>0?localFailure:ExpiredIdentityRepairPhase(attempt,target);
   if(attempt.Phase!=expired){
    attempt.Phase=expired;attempt.At=now.ToString("o");changed=true;
    Event31(attempt.Host,"identity-delay58",attempt.JobId,"Проверка перерегистрации WSUS","Warning",attempt.Phase,scope:attempt.JobId);
   }
  }
  if(changed)Store.Write("identity-repairs58.json",identityRepairs58);
 }
 static string IdentityRepairFailure(TargetRecord? target)=>target?.State is {Status:"Failed" or "InterventionRequired",Ended.Length:>0} failure
  ?"Перерегистрация завершилась с ошибкой · "+(failure.Error.Length>0?failure.Error:ComputerRow.StatusText(target.Status)):"";
 static string ExpiredIdentityRepairPhase(IdentityRepairAttempt58 attempt,TargetRecord? target){
  if(attempt.NewId.Length>0)return "Регистрация задерживается: новый ID прочитан, свежая запись или отчёт WSUS не подтверждены за 10 минут";
  if(target==null)return "Регистрация задерживается: новый ID не подтверждён за 10 минут";
  var localFailure=IdentityRepairFailure(target);
  if(localFailure.Length>0)return localFailure;
  if(target.State is {Status:"Running"})return "Клиент выполняет перерегистрацию · новый ID не подтверждён за 10 минут";
  if(target.State is {Status:"Completed" or "AwaitingReboot"})return "Клиент завершил команду · новый ID не подтверждён за 10 минут";
  return "Результат задания не подтверждён за 10 минут · новый ID не прочитан";
 }
 static bool SameServer(string policy,WsusSettings settings){
  if(!Uri.TryCreate(policy,UriKind.Absolute,out var client)||string.IsNullOrWhiteSpace(settings.Host))return false;
  return client.Scheme==(settings.Ssl?"https":"http")&&client.Port==settings.Port&&client.Host.TrimEnd('.').Equals(settings.Host.Trim().TrimEnd('.'),StringComparison.OrdinalIgnoreCase);
 }
 bool CompleteIdentityRegistration58(IdentityRepairAttempt58 attempt,ClientIdentity58 identity,IdentityServerReply58 reply){
  if(attempt.JobId.Length==0||!SameServer(identity.Source,ReadWsus())||!attempt.Key.StartsWith(identity.Key[..(identity.Key.LastIndexOf('|')+1)]+attempt.OldId+"|",StringComparison.OrdinalIgnoreCase)||!IdentityPolicy58.IndependentNewId(identities58.Values,identity,DateTimeOffset.UtcNow)||compromisedIds58.Contains(identity.Key)||!IdentityPolicy58.RegistrationConfirmed(identity,attempt.OldId,attempt.StartedUtc.Length>0?attempt.StartedUtc:attempt.At,reply.Id,reply.Name,reply.ReportedUtc,DateTimeOffset.UtcNow))return false;
  // A bulk overview contains no group data. Read the exact target before reporting registration.
  if(reply.Groups.Count==0&&reply.GroupError.Length==0)return false;
  var expected=identity.TargetGroup.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
  attempt.GroupNote=reply.GroupError.Length>0?"Не удалось прочитать группы WSUS: "+reply.GroupError:
   identity.TargetGroupEnabled!=1?"Проверьте серверное назначение групп у администратора WSUS.":
   expected.Any(g=>!reply.Groups.Contains(g,StringComparer.OrdinalIgnoreCase))?"Новая запись зарегистрирована. Требуется проверить назначение группы "+identity.TargetGroup:"";
  attempt.NewId=identity.SusClientId;attempt.Groups=reply.Groups;attempt.Phase="Подтверждено";attempt.At=DateTimeOffset.UtcNow.ToString("o");
  Event31(attempt.Host,"identity-confirmed58",attempt.NewId,"Новая регистрация и отчёт WSUS подтверждены","Success","Прежний ID: "+attempt.OldId+"; новый ID: "+attempt.NewId+"; имя: "+identity.Name+"; отчёт: "+Display36.Date(reply.ReportedUtc)+". Группы: "+(reply.Groups.Count>0?string.Join(", ",reply.Groups):"не прочитаны")+(reply.GroupError.Length>0?". "+reply.GroupError:""),scope:attempt.JobId);
  if(attempt.GroupNote.Length>0)Event31(attempt.Host,"identity-group58",attempt.NewId,"Проверить группы новой записи WSUS","Warning",attempt.GroupNote,scope:attempt.JobId);
  return true;
 }
 List<IdentityRepairAttempt58> RepairGroupFor(string host){
  var latest=identityRepairs58.Values.Where(a=>a.Host.Equals(host,StringComparison.OrdinalIgnoreCase)).OrderByDescending(a=>a.At).FirstOrDefault();
  if(latest==null)return new();
  int split=latest.Key.LastIndexOf('|');if(split<0)return new(){latest};
  string prefix=latest.Key[..(split+1)];
  return identityRepairs58.Values.Where(a=>a.Key.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)).ToList();
 }
 async Task VerifyIdentityRegistration58(string host,bool requested=false,CancellationToken cancellation=default){
  if(closing||App.RecoveryPending57||PollingControl43.BackgroundPaused56||!identities58.TryGetValue(host,out var current)||!current.Fresh(DateTimeOffset.UtcNow)||!Guid.TryParse(current.SusClientId,out var guid)||guid==Guid.Empty||!identityChecks58.Add(host))return;
  try{
   ExpireIdentityRepairs58();var now=DateTimeOffset.UtcNow;
   var attempts=identityRepairs58.Values.Where(a=>a.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&a.JobId.Length>0&&a.Phase!="Подтверждено"&&!a.OldId.Equals(current.SusClientId,StringComparison.OrdinalIgnoreCase)&&(requested||DateTimeOffset.TryParse(a.DeadlineUtc,out var until)&&until>now&&(!DateTimeOffset.TryParse(a.LastCheckUtc,out var last)||now-last>=TimeSpan.FromMinutes(1)))).ToList();
   if(attempts.Count==0)return;
   var identity=Store.Clone(current);var settings=ReadWsus();var source=OverviewServer;var ssl=WsusSsl.IsChecked;using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,PollingControl43.Token);var generation=linked.Token;var endpoint=ConnectionStore.Capture(new TargetRecord{Host=settings.Host});
   bool Current()=>!closing&&!generation.IsCancellationRequested&&!PollingControl43.BackgroundPaused56&&source==OverviewServer&&ssl==WsusSsl.IsChecked&&CurrentEndpoint(endpoint)&&identities58.TryGetValue(host,out var observed)&&observed.Fresh(DateTimeOffset.UtcNow)&&observed.SusClientId==identity.SusClientId&&observed.Machine==identity.Machine&&observed.Name==identity.Name;
   foreach(var attempt in attempts){attempt.NewId=identity.SusClientId;attempt.LastCheckUtc=now.ToString("o");attempt.Phase="Новый ID прочитан; проверяется запись и отчёт WSUS (до 10 минут)";}
   Store.Write("identity-repairs58.json",identityRepairs58);
   try{
    var reply=await remote.ReadIdentityRegistration58(settings,identity.SusClientId,generation);
    if(!Current())return;
    foreach(var attempt in attempts){if(!identityRepairs58.Values.Contains(attempt)||attempt.Phase=="Подтверждено")continue;attempt.Groups=reply.Groups;if(!CompleteIdentityRegistration58(attempt,identity,reply)){attempt.Phase="Новый ID прочитан; имя записи или свежий отчёт пока не подтверждены";attempt.At=DateTimeOffset.UtcNow.ToString("o");}}
   }catch(OperationCanceledException){}catch(Exception ex){if(Current())foreach(var attempt in attempts.Where(a=>identityRepairs58.Values.Contains(a)&&a.Phase!="Подтверждено")){attempt.Phase="Подтверждение WSUS не получено: "+ConnectionBrief31(ex.Message);attempt.At=DateTimeOffset.UtcNow.ToString("o");}}
   Store.Write("identity-repairs58.json",identityRepairs58);ExpireIdentityRepairs58();
  }catch(Exception ex){Event31(host,"identity-check58",ConnectionBrief31(ex.Message),"Проверка регистрации WSUS отложена","Warning",ConnectionBrief31(ex.Message));}
  finally{identityChecks58.Remove(host);}
 }
}
public sealed partial class RemoteService {
 public Task<IdentityServerReply58> ReadIdentityRegistration58(WsusSettings settings,string id,CancellationToken token)=>FindIdentityRecords(settings,id,"",token);
 public async Task<IdentityServerReply58> FindIdentityRecords(WsusSettings settings,string id,string name,CancellationToken token){
  using var read=PollingControl43.Read();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(60));
  var script="$port="+settings.Port+";$ssl="+(settings.Ssl?"$true":"$false")+";$lookupName58="+PowerShellBridge.Literal(name)+";$identityId58="+PowerShellBridge.Literal(id)+";"+IdentityServerScript58;
  return JsonSerializer.Deserialize<IdentityServerReply58>(await PowerShellBridge.Execute(Invoke(settings.Host,script),timeout.Token),Store.Options)??throw new Exception("Пустое подтверждение WSUS");
 }
 public const string IdentityServerScript58="""
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration')
$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,$ssl,$port)
$names58=@();$lookupError58=''
if($lookupName58){
 try{$names58=@($s.SearchComputerTargets($lookupName58)|Where-Object {$_.FullDomainName.TrimEnd('.') -eq $lookupName58.TrimEnd('.')}|ForEach-Object {$_.FullDomainName})}
 catch{$lookupError58=$_.Exception.Message}
}
$reply58=@{Id='';Name='';ReportedUtc='';Groups=@();GroupError='';Names=@($names58);LookupError=$lookupError58}
try{$target58=$s.GetComputerTarget([string]$identityId58)}catch{if(!$lookupName58){throw};$reply58.LookupError=$_.Exception.Message;$target58=$null}
if($target58){
 $reply58.Id=[string]$target58.Id;$reply58.Name=[string]$target58.FullDomainName
 $reply58.ReportedUtc=$target58.LastReportedStatusTime.ToUniversalTime().ToString('o')
 try{$reply58.Groups=@($target58.GetComputerTargetGroups()|ForEach-Object {[string]$_.Name})}catch{$reply58.GroupError=$_.Exception.Message}
}
$reply58|ConvertTo-Json -Depth 4 -Compress
""";
}
