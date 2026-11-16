using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;

namespace DomainConsole;

public sealed class IdentitySurvey {
 public string Owner {get;set;}="";
 public List<string> ServerNames {get;set;}=new();
 public string ServerName {get;set;}="";
 public string ServerResult {get;set;}="";
 public DateTimeOffset Started {get;set;}
 public DateTimeOffset Updated {get;set;}
 public string ScopeMessage {get;set;}="";
 public bool VerifyRegistration {get;set;}
 public bool Domain {get;set;}
 public List<string> Hosts {get;set;}=new();
 public Dictionary<string,string> Results {get;set;}=new(StringComparer.OrdinalIgnoreCase);
}

public partial class MainWindow {
 IdentitySurvey survey=Store.Read("identity-survey.json",new IdentitySurvey());
 CancellationTokenSource? surveyRun;
 Window? surveyWindow;
 Action? refreshSurvey;

 string DescribeSurveyResult(ComputerRow selected,IReadOnlyList<ClientIdentity58> confirmed,int failed,bool running){
  if(confirmed.Count>=2)return "Одинаковый SusClientId у "+string.Join(", ",confirmed.Take(5).Select(x=>x.Name))+(confirmed.Count>5?$" и ещё {confirmed.Count-5}":"");
  if(running)return "Идёт чтение участников";
  if(!survey.Results.ContainsKey(selected.Host)||!identities58.TryGetValue(selected.Host,out var anchor)||!anchor.Fresh(DateTimeOffset.UtcNow))return "Проверка неполная · требуется свежий ответ выбранного клиента";
  if(!Guid.TryParse(anchor.SusClientId,out var id)||id==Guid.Empty)return "Выбранный клиент вернул пустой или некорректный SusClientId";
  if(!IdentityPolicy58.Usable(anchor))return "Проверка неполная · не подтверждены устройство или источник WSUS";
  bool ambiguous=survey.Hosts.Select(h=>identities58.GetValueOrDefault(h)).Any(peer=>peer!=null&&peer.Fresh(DateTimeOffset.UtcNow)&&peer.Key==anchor.Key&&peer.Machine.Equals(anchor.Machine,StringComparison.OrdinalIgnoreCase)&&!peer.Name.TrimEnd('.').Equals(anchor.Name.TrimEnd('.'),StringComparison.OrdinalIgnoreCase));
  if(ambiguous)return "Один аппаратный идентификатор у разных имён · возможны переименование или клонирование. Исправление неоднозначных устройств недоступно";
  if(survey.ServerName.Length>0&&!survey.ServerName.TrimEnd('.').Equals(anchor.Name.TrimEnd('.'),StringComparison.OrdinalIgnoreCase))return "Имя записи WSUS отличается от имени клиента · возможны переименование или устаревшая запись";
  if(failed>0)return "Проверка неполная · общий ID у доступных участников не подтверждён";
  if(survey.Hosts.Any(h=>identities58.TryGetValue(h,out var peer)&&peer.Fresh(DateTimeOffset.UtcNow)&&IdentityPolicy58.Usable(peer)&&peer.Source.Equals(anchor.Source,StringComparison.OrdinalIgnoreCase)&&!peer.SusClientId.Equals(anchor.SusClientId,StringComparison.OrdinalIgnoreCase)))return "Идентификаторы проверенных устройств различаются · общий ID не подтверждён";
  return "На проверенных устройствах общий ID не подтверждён";
 }

 IEnumerable<string> IdentityCandidates(ComputerRow selected,bool domain){
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase){selected.Host,selected.Name};
  if(identities58.TryGetValue(selected.Host,out var identity)&&Guid.TryParse(identity.SusClientId,out _)){
   foreach(var peer in identities58.Values.Where(p=>p.Key==identity.Key)){names.Add(peer.Host);names.Add(peer.Name);}
   if(SnapshotMatches58)foreach(var record in serverSnapshot58.Records.Where(r=>r.Id.Equals(identity.SusClientId,StringComparison.OrdinalIgnoreCase)))names.Add(record.Name);
  }
  foreach(var past in RepairGroupFor(selected.Host))names.Add(past.Host);
  if(survey.Owner.Equals(selected.Host,StringComparison.OrdinalIgnoreCase)&&survey.ServerName.Length>0)names.Add(survey.ServerName);
  if(survey.Owner.Equals(selected.Host,StringComparison.OrdinalIgnoreCase))names.UnionWith(survey.ServerNames);
  var lookup=names.Select(n=>n.TrimEnd('.')).ToHashSet(StringComparer.OrdinalIgnoreCase);
  bool Known(ComputerRow r)=>lookup.Contains(r.Host.TrimEnd('.'))||lookup.Contains(r.Name.TrimEnd('.'))||lookup.Contains(ConnectionStore.WsusName(r.Host).TrimEnd('.'));
  return Computers.Where(r=>!r.Target.AdDisabled&&(domain||Known(r))).OrderByDescending(r=>r.Host.Equals(selected.Host,StringComparison.OrdinalIgnoreCase)).ThenByDescending(Known).Select(r=>r.Host).Distinct(StringComparer.OrdinalIgnoreCase);
 }
 async Task RunIdentitySurvey(ComputerRow selected,bool domain,bool resume=false,Func<TargetRecord,CancellationToken,Task<ClientIdentity58>>? readClient=null,Func<WsusSettings,string,CancellationToken,Task<IdentityServerReply58>>? readServer=null,bool verifyRegistration=false){
  if(surveyRun!=null)return;
  var checkedSources=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  using var stop=new CancellationTokenSource();surveyRun=stop;
  using var lane=PollingControl43.Manual56("identity");
  try{
   if(!resume||!survey.Owner.Equals(selected.Host,StringComparison.OrdinalIgnoreCase)||survey.Domain!=domain)
    survey=new(){Owner=selected.Host,Started=DateTimeOffset.UtcNow,Domain=domain,Hosts=IdentityCandidates(selected,domain).ToList()};
   if(verifyRegistration){
    survey.VerifyRegistration=true;survey.Hosts=RepairGroupFor(selected.Host).Select(a=>a.Host).Prepend(selected.Host).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
   }
   Store.Write("identity-survey.json",survey);refreshSurvey?.Invoke();
   for(int i=0;i<survey.Hosts.Count;i++){
    stop.Token.ThrowIfCancellationRequested();string host=survey.Hosts[i];if(survey.Results.ContainsKey(host))continue;
    var row=Computers.FirstOrDefault(r=>r.Host.Equals(host,StringComparison.OrdinalIgnoreCase));
    if(row==null||row.Target.AdDisabled){survey.Results[host]="Пропущен · отсутствует или отключён в AD";continue;}
    var target=ConnectionStore.Capture(row.Target);var began=DateTimeOffset.UtcNow;survey.ScopeMessage="Читаем "+host;refreshSurvey?.Invoke();
    try{
     if(!ConnectionStore.CanConnect(target))throw new InvalidOperationException("Проверьте адрес и учётные данные");
     var result=await (readClient??remote.ReadIdentity)(target,stop.Token);
     stop.Token.ThrowIfCancellationRequested();
     if(!CurrentEndpoint(target)||!CurrentIdentity(target))throw new InvalidOperationException("Цель изменилась во время чтения");
     if(maintenancePlans.TryGetValue(host,out var plan))result.Activity+=" · план консоли: "+plan.Phase;
     result.Address=string.IsNullOrWhiteSpace(target.ConnectionAddress)?target.Host:target.ConnectionAddress;
     if(readClient==null)await ResolveIdentitySource(result,ReadWsus(),checkedSources,stop.Token);
     stop.Token.ThrowIfCancellationRequested();
     if(!CurrentEndpoint(target)||!CurrentIdentity(target))throw new InvalidOperationException("Цель изменилась во время проверки источника WSUS");
     ConnectionSucceeded(host,began,true);
     ObserveIdentity58(host,result,false);
     if(survey.VerifyRegistration&&result.Error.Length==0)await VerifyIdentityRegistration58(host,true,stop.Token);
     if(!survey.VerifyRegistration&&host.Equals(selected.Host,StringComparison.OrdinalIgnoreCase)&&IdentityPolicy58.Usable(result)){
      try{
       var settings=ReadWsus();var server=readServer==null?await remote.FindIdentityRecords(settings,result.SusClientId,result.Name,stop.Token):await readServer(settings,result.SusClientId,stop.Token);stop.Token.ThrowIfCancellationRequested();
       survey.ServerName=server.Name;survey.ServerNames=server.Names;survey.ServerResult=server.Id.Length>0?"WSUS · "+server.Name+" · отчёт "+Display36.Date(server.ReportedUtc):"Запись по ID не подтверждена";if(server.LookupError.Length>0)survey.ServerResult+=" · "+ConnectionBrief31(server.LookupError);
      }catch(Exception ex) when(!stop.IsCancellationRequested){survey.ServerResult="Серверный ID не проверен · "+ConnectionBrief31(ex.Message);}
     }
     survey.Results[host]=result.Error.Length>0?"Не удалось проверить · "+result.Error:!Guid.TryParse(result.SusClientId,out var valid)||valid==Guid.Empty?"Неполные сведения · отсутствует или некорректен SusClientId":!IdentityPolicy58.Usable(result)?"Неполные сведения · проверьте имя, аппаратный идентификатор и источник WSUS":result.Name+" · "+result.SusClientId+" · "+result.Server+" · "+Display36.Date(result.At);
    }catch(OperationCanceledException) when(stop.IsCancellationRequested){throw;}
    catch(Exception ex){survey.Results[host]="Не удалось проверить · "+ConnectionBrief31(ex.Message);ObserveIdentity58(host,new(){At=DateTimeOffset.UtcNow.ToString("o"),Error=ex.Message},false);}
    if(i==0&&!domain&&!survey.VerifyRegistration)foreach(var peer in IdentityCandidates(selected,false).ToList())if(!survey.Hosts.Contains(peer,StringComparer.OrdinalIgnoreCase))survey.Hosts.Add(peer);
    Store.Write("identity-survey.json",survey);refreshSurvey?.Invoke();
   }
  }catch(OperationCanceledException) when(stop.IsCancellationRequested){}
  finally{survey.Updated=DateTimeOffset.UtcNow;survey.ScopeMessage=stop.IsCancellationRequested?"Поиск приостановлен":"Проход завершён";surveyRun=null;Store.Write("identity-survey.json",survey);refreshSurvey?.Invoke();}
 }
 async void OpenIdentitySurvey(ComputerRow selected){
  if(surveyWindow!=null){surveyWindow.Activate();return;}
  if(surveyRun!=null)selected=Computers.FirstOrDefault(r=>r.Host.Equals(survey.Owner,StringComparison.OrdinalIgnoreCase))??selected;
  var window=IdentityWindow58(selected);surveyWindow=window;
  window.Closed+=(s,e)=>{surveyWindow=null;refreshSurvey=null;};window.Show();
  if(surveyRun==null&&identityRepairBusy58.Count==0&&(!survey.Owner.Equals(selected.Host,StringComparison.OrdinalIgnoreCase)||!survey.Domain&&survey.Results.Count==survey.Hosts.Count))await Guard(()=>RunIdentitySurvey(selected,false));
 }
}

public sealed partial class RemoteService {
 public async Task<ClientIdentity58> ReadIdentity(TargetRecord target,CancellationToken token){
  using var read=PollingControl43.Read();
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(30));
  string json=await PowerShellBridge.Execute(Invoke(target,IdentityProbe58+"\n"+ManualIdentityActivity+"\n$dcIdentity58 | ConvertTo-Json -Compress"),timeout.Token);
  return JsonSerializer.Deserialize<ClientIdentity58>(json,Store.Options)??throw new InvalidOperationException("Клиент вернул пустой ответ");
 }
 // Read local process metadata inside the existing manual WinRM call. No scheduled task or agent is started.
 const string ManualIdentityActivity="""
try {
 $dcWork=@(Get-CimInstance Win32_Process -Filter "Name='DomainConsole.Agent.exe' OR Name='TiWorker.exe' OR Name='TrustedInstaller.exe' OR Name='msiexec.exe'" -ErrorAction Stop)
 $dcExecutors=@($dcWork|Where-Object {$_.Name -eq 'DomainConsole.Agent.exe' -and $_.CommandLine -match '\brun\s+'})
 $dcServicing=@($dcWork|Where-Object {$_.Name -ne 'DomainConsole.Agent.exe'}|Select-Object -ExpandProperty Name -Unique)
 $dcIdentity58.Activity="Исполнителей заданий: "+$dcExecutors.Count
 if($dcServicing.Count){$dcIdentity58.Activity+=" · процессы обслуживания: "+($dcServicing -join ', ')}
} catch {$dcIdentity58.Activity="Активность не уточнена: "+$_.Exception.Message}
""";
}
