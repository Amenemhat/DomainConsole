using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class IdentityServerReply58 {
 public string Id {get;set;}="";
 public string Name {get;set;}="";
 public string ReportedUtc {get;set;}="";
 public List<string> Groups {get;set;}=new();
 public string GroupError {get;set;}="";
}
public partial class MainWindow {
 readonly HashSet<string> identityChecks58=new(StringComparer.OrdinalIgnoreCase);
 void ExpireIdentityRepairs58(){
  bool changed=false;var now=DateTimeOffset.UtcNow;
  foreach(var attempt in identityRepairs58.Values.Where(a=>a.JobId.Length>0&&a.Phase!="Подтверждено")){
   if(attempt.StartedUtc.Length==0)attempt.StartedUtc=attempt.At;
   if(attempt.DeadlineUtc.Length==0&&DateTimeOffset.TryParse(attempt.StartedUtc,out var start)){attempt.DeadlineUtc=start.AddMinutes(10).ToString("o");changed=true;}
   if(DateTimeOffset.TryParse(attempt.DeadlineUtc,out var deadline)&&now>=deadline&&!attempt.Phase.StartsWith("Регистрация задерживается")){
    attempt.Phase="Регистрация задерживается: 10 минут истекли; обновите серверный обзор или проверьте доставку отчёта";attempt.At=now.ToString("o");changed=true;
    Event31(attempt.Host,"identity-delay58",attempt.JobId,"Подтверждение регистрации WSUS задерживается","Warning",attempt.Phase,scope:attempt.JobId);
   }
  }
  if(changed)Store.Write("identity-repairs58.json",identityRepairs58);
 }
 bool CompleteIdentityRegistration58(IdentityRepairAttempt58 attempt,ClientIdentity58 identity,IdentityServerReply58 reply){
  if(compromisedIds58.Contains(identity.Key)||!IdentityPolicy58.RegistrationConfirmed(identity,attempt.OldId,attempt.StartedUtc.Length>0?attempt.StartedUtc:attempt.At,reply.Id,reply.Name,reply.ReportedUtc,DateTimeOffset.UtcNow))return false;
  attempt.NewId=identity.SusClientId;attempt.Groups=reply.Groups;attempt.Phase="Подтверждено";attempt.At=DateTimeOffset.UtcNow.ToString("o");
  Event31(attempt.Host,"identity-confirmed58",attempt.NewId,"Новая регистрация и отчёт WSUS подтверждены","Success","Прежний ID: "+attempt.OldId+"; новый ID: "+attempt.NewId+"; имя: "+identity.Name+"; отчёт: "+Display36.Date(reply.ReportedUtc)+". Группы: "+(reply.Groups.Count>0?string.Join(", ",reply.Groups):"не прочитаны")+(reply.GroupError.Length>0?". "+reply.GroupError:""),scope:attempt.JobId);
  return true;
 }
 async Task VerifyIdentityRegistration58(string host){
  if(closing||App.RecoveryPending57||PollingControl43.BackgroundPaused56||!identities58.TryGetValue(host,out var current)||!current.Fresh(DateTimeOffset.UtcNow)||!Guid.TryParse(current.SusClientId,out var guid)||guid==Guid.Empty||!identityChecks58.Add(host))return;
  try{
   ExpireIdentityRepairs58();var now=DateTimeOffset.UtcNow;
   var attempts=identityRepairs58.Values.Where(a=>a.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&a.JobId.Length>0&&a.Phase!="Подтверждено"&&!a.OldId.Equals(current.SusClientId,StringComparison.OrdinalIgnoreCase)&&DateTimeOffset.TryParse(a.DeadlineUtc,out var until)&&until>now&&(!DateTimeOffset.TryParse(a.LastCheckUtc,out var last)||now-last>=TimeSpan.FromMinutes(1))).ToList();
   if(attempts.Count==0)return;
   var identity=Store.Clone(current);var settings=ReadWsus();var source=OverviewServer;var ssl=WsusSsl.IsChecked;var generation=PollingControl43.Token;var endpoint=ConnectionStore.Capture(new TargetRecord{Host=settings.Host});
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
 public async Task<IdentityServerReply58> ReadIdentityRegistration58(WsusSettings settings,string id,CancellationToken token){
  using var read=PollingControl43.Read();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(60));
  var script="$port="+settings.Port+";$ssl="+(settings.Ssl?"$true":"$false")+";$identityId58="+PowerShellBridge.Literal(id)+";"+IdentityServerScript58;
  return JsonSerializer.Deserialize<IdentityServerReply58>(await PowerShellBridge.Execute(Invoke(settings.Host,script),timeout.Token),Store.Options)??throw new Exception("Пустое подтверждение WSUS");
 }
 public const string IdentityServerScript58="""
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration')
$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,$ssl,$port)
$target58=$s.GetComputerTarget([string]$identityId58)
$groups58=@();$groupError58=''
try{$groups58=@($target58.GetComputerTargetGroups()|ForEach-Object {[string]$_.Name})}catch{$groupError58=$_.Exception.Message}
@{Id=[string]$target58.Id;Name=[string]$target58.FullDomainName;ReportedUtc=$target58.LastReportedStatusTime.ToUniversalTime().ToString('o');Groups=@($groups58);GroupError=$groupError58}|ConvertTo-Json -Depth 4 -Compress
""";
}
