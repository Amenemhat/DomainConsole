using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class IdentityRepairAttempt58 {
 public string Key {get;set;}="";
 public string Host {get;set;}="";
 public string OldId {get;set;}="";
 public string NewId {get;set;}="";
 public string JobId {get;set;}="";
 public string StartedUtc {get;set;}="";
 public string DeadlineUtc {get;set;}="";
 public bool Manual {get;set;}
 public string LastClientReadUtc {get;set;}="";
 public string LastCheckUtc {get;set;}="";
 public List<string> Groups {get;set;}=new();
 public string GroupNote {get;set;}="";
 public string Phase {get;set;}="Ожидается проверка";
 public string At {get;set;}="";
}
public partial class MainWindow {
 readonly Dictionary<string,IdentityRepairAttempt58> identityRepairs58=Store.Read("identity-repairs58.json",new Dictionary<string,IdentityRepairAttempt58>());
 readonly Dictionary<string,List<string>> manualIdentityScopes=Store.Read("identity-scope-approvals.json",new Dictionary<string,List<string>>());
 static string MemberIdentity(ClientIdentity58 i){
  var installation=IdentityPolicy58.Installation(i);
  return i.Machine.ToLowerInvariant()+"|"+i.Name.TrimEnd('.').ToLowerInvariant()+
   (installation==Guid.Empty?"":"|"+installation.ToString("D"));
 }
 void RememberDiagnosticScope(ClientIdentity58 identity){
  if(!IdentityPolicy58.Usable(identity)||manualIdentityScopes.ContainsKey(identity.Key))return;
  var retained=identities58.Values.Select(x=>x.Key).Concat(identityRepairs58.Values.Where(a=>a.Key.LastIndexOf('|')>0).Select(a=>a.Key[..a.Key.LastIndexOf('|')])).ToHashSet(StringComparer.OrdinalIgnoreCase);
  foreach(var key in manualIdentityScopes.Keys.Where(k=>!retained.Contains(k)).ToList())manualIdentityScopes.Remove(key);
  manualIdentityScopes[identity.Key]=new();Store.Write("identity-scope-approvals.json",manualIdentityScopes);
 }
 readonly Dictionary<string,CancellationTokenSource> identityRepairStops=new(StringComparer.OrdinalIgnoreCase);
 void StopIdentityRepair58(string host){
  var anchor=RepairAnchor58(host);
  if(anchor==null||!identityRepairStops.TryGetValue(anchor.Key,out var stop))return;
  stop.Cancel();
  manualIdentityScopes[anchor.Key]=new();
  Store.Write("identity-scope-approvals.json",manualIdentityScopes);
  Event31(host,"identity-stop58",anchor.Key,"Остановлена передача новым участникам","Info","Уже принятые клиентами задания завершают восстановление служб.");
 }
 readonly HashSet<string> identityRepairBusy58=new(StringComparer.OrdinalIgnoreCase);
 // A solitary returning clone can extend only a previously proven, explicitly approved repair.
 List<ClientIdentity58> RepairCandidates58(ClientIdentity58 seen,bool manual){
  var now=DateTimeOffset.UtcNow;
  var members=IdentityPolicy58.Participants(identities58.Values,seen,now);
  manualIdentityScopes.TryGetValue(seen.Key,out var approved);
  bool confirmed=IdentityPolicy58.Confirmed(members,seen,now);
  bool established=approved?.Distinct().Count()>=2&&identityRepairs58.Values.Any(a=>a.Key.StartsWith(seen.Key+"|",StringComparison.OrdinalIgnoreCase)&&a.JobId.Length>0);
  if(manual&&(confirmed||established)||!manual&&approved==null&&confirmed)return members;
  return members.Where(i=>approved?.Contains(MemberIdentity(i))==true).ToList();
 }
 ClientIdentity58? RepairAnchor58(string owner){
  if(identities58.TryGetValue(owner,out var current)&&compromisedIds58.Contains(current.Key))return current;
  var past=RepairGroupFor(owner).FirstOrDefault();
  if(past==null||past.Key.LastIndexOf('|')<1)return null;
  var key=past.Key[..past.Key.LastIndexOf('|')];
  return identities58.Values.FirstOrDefault(i=>i.Key==key&&i.Fresh(DateTimeOffset.UtcNow));
 }
 async Task RepairIdentityGroup58(string host,bool manual){
  using var foreground=manual?PollingControl43.Manual56("identity"):null;
  var observed=manual?RepairAnchor58(host):identities58.GetValueOrDefault(host);
  if(closing||App.RecoveryPending57||observed==null||!compromisedIds58.Contains(observed.Key))return;
  if(!Guid.TryParse(observed.SusClientId,out var oldGuid)||oldGuid==Guid.Empty||!identityRepairBusy58.Add(observed.Key))return;
  using var stop=new CancellationTokenSource();identityRepairStops[observed.Key]=stop;refreshSurvey?.Invoke();
  try{
   var participants=RepairCandidates58(observed,manual);
   manualIdentityScopes.TryGetValue(observed.Key,out var approved);
   if(participants.Count==0)return;
   bool expanded=approved?.Count>0&&participants.Any(p=>!approved.Contains(MemberIdentity(p)));
   string question=expanded?"Добавить новых участников в ранее подтверждённое исправление?":"Перерегистрировать обнаруженных участников общего ID?";
   if(manual&&MessageBox.Show(this,question+"\n"+string.Join("\n",participants.Select(x=>x.Name))+"\n\nНедоступные и занятые клиенты остаются в ожидании. Записи сервера сохраняются.","Перерегистрация клиентов",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
   if(manual){
    manualIdentityScopes[observed.Key]=(approved??new List<string>()).Concat(participants.Select(MemberIdentity)).Distinct().ToList();
    Store.Write("identity-scope-approvals.json",manualIdentityScopes);
   }
   var verifiedSources=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   foreach(var identity in participants){
    if(stop.IsCancellationRequested)break;
    var row=Computers.FirstOrDefault(r=>r.Host.Equals(identity.Host,StringComparison.OrdinalIgnoreCase));if(row==null||row.Target.AdDisabled)continue;
    string legacyKey=identity.Key+"|"+identity.Machine.ToLowerInvariant();
    var installation=IdentityPolicy58.Installation(identity);
    string key=installation==Guid.Empty?legacyKey:legacyKey+":"+installation.ToString("D");
    // A persisted attempt survives a crash, even if delivery acknowledgement was lost.
    var legacy=identityRepairs58.GetValueOrDefault(legacyKey);
    if(legacy?.Host.Equals(identity.Host,StringComparison.OrdinalIgnoreCase)==true)key=legacyKey;
    var prior=identityRepairs58.GetValueOrDefault(key)??legacy;
    if(prior!=null&&prior.JobId.Length>0&&(!manual||!History.Any(j=>j.Id==prior.JobId&&j.Targets.All(t=>t.Status is "LaunchRejected" or "AccessDenied")))){if(manual)Notify(identity.Name+": попытка ещё активна; проверьте задание "+prior.JobId);continue;}
    void Defer(string reason){
     identityRepairs58[key]=new(){Key=key,Host=row.Host,OldId=identity.SusClientId,Phase=reason,At=DateTimeOffset.UtcNow.ToString("o")};
     Store.Write("identity-repairs58.json",identityRepairs58);refreshSurvey?.Invoke();
    }
    var frozen=new ComputerRow(ConnectionStore.Capture(row.Target));
    bool Available()=>!closing&&!stop.IsCancellationRequested&&!row.Target.AdDisabled&&CurrentEndpoint(frozen.Target)&&CurrentIdentity(frozen.Target);
    if(!Available()||!ConnectionStore.CanConnect(frozen.Target)){Defer("Подключение к согласованной цели недоступно");continue;}
    if(HostJobs39(row.Host).Any(x=>ActiveTarget(x.Target))){Defer("Ожидается завершение активного задания");continue;}
    if(manual||!identity.Fresh(DateTimeOffset.UtcNow)||!ConnectionFresh(row.Host)){
     if(!manual){Defer("Ожидается свежая проверка клиента");continue;}
     var began=DateTimeOffset.UtcNow;
     try{
      var current=await remote.ReadIdentity(frozen.Target,stop.Token);
      await ResolveIdentitySource(current,ReadWsus(),verifiedSources,stop.Token);
      if(!Available())continue;
      current.Address=string.IsNullOrWhiteSpace(frozen.Target.ConnectionAddress)?frozen.Host:frozen.Target.ConnectionAddress;
      ObserveIdentity58(row.Host,current,false);
      if(!IdentityPolicy58.SameApprovedDevice(identity,current,DateTimeOffset.UtcNow)){
       Defer("Участник, прежний ID или источник WSUS больше не подтверждены. Повторите диагностику");continue;
      }
      ConnectionSucceeded(row.Host,began,true);
     }catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
     catch(Exception ex){Defer("Не удалось повторно прочитать согласованного клиента: "+ConnectionBrief31(ex.Message));continue;}
    }
    if(identity.UseWUServer!=1){Defer("Политика UseWUServer=1 не подтверждена");continue;}
    try{
     var live=manual?await remote.ReadManualUpdateActivity(frozen.Target,stop.Token):await remote.LiveUpdates(frozen.Target);
     if(live.Busy||live.Jobs.Count>0||live.Limitation.Length>0||live.InstallationEvidence.Length>0){
      var reason=live.Limitation.Length>0?live.Limitation:live.InstallationEvidence.Length>0?live.InstallationEvidence:live.Jobs.Count>0?"активное задание клиента":"Windows Update занят";
      Defer("Ожидается завершение обслуживания или уточнение состояния · "+reason);continue;
     }
    }catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
    catch(Exception ex){Defer("Проверка активности не завершена: "+ConnectionBrief31(ex.Message));continue;}
    if(stop.IsCancellationRequested)break;
    if(!Available()||HostJobs39(row.Host).Any(x=>ActiveTarget(x.Target)))continue;
    var job=new RemoteJob{Manual56=manual,AutoInstall54=false,AutoReboot=false,MicrosoftSource=false,OutputChannel="WSUS",Name="Перерегистрация общего SusClientId",Steps=new(){new(){Name="Перерегистрация клиента WSUS",Kind="PowerShell",TouchesUpdates=true,Code=RemoteService.IdentityRepairScript58(identity)}}};
    identityRepairs58[key]=new(){Key=key,Host=row.Host,OldId=identity.SusClientId,Manual=manual,JobId=job.Id,StartedUtc=DateTimeOffset.UtcNow.ToString("o"),DeadlineUtc=DateTimeOffset.UtcNow.AddMinutes(10).ToString("o"),Phase="Подготовлено; повторный сброс запрещён",At=DateTimeOffset.UtcNow.ToString("o")};
    Store.Write("identity-repairs58.json",identityRepairs58);
    Event31(row.Host,"identity-repair58",job.Id,"Перерегистрация клиента подготовлена","Info","Прежний ID: "+identity.SusClientId,scope:job.Id);
    bool accepted=await CompletePreparedSubmission47(job,new(){row},new(){frozen},()=>Task.FromResult((Accepted:true,Force:false,Wait:false)),false);
    if(accepted){identityRepairs58[key].Phase="Задание создано; ожидается новый ID и регистрация на сервере";await SendQueue(manual?job.Id:"");}
    else identityRepairs58[key].Phase="Передача не подтверждена; требуется проверка задания";
    Store.Write("identity-repairs58.json",identityRepairs58);
   }
   Store.Write("identity-repairs58.json",identityRepairs58);
  }catch(Exception ex){Event31(host,"identity-repair58-error",ex.Message,"Перерегистрация требует проверки","Warning",ex.Message);}
  finally{identityRepairStops.Remove(observed.Key);identityRepairBusy58.Remove(observed.Key);refreshSurvey?.Invoke();if(manual)_=ObserveManualRepairs58();}
 }
 void ConfirmIdentityRepairs58(){
  if(!SnapshotMatches58)return;
  foreach(var attempt in identityRepairs58.Values.Where(a=>a.Phase!="Подтверждено")){
   if(!identities58.TryGetValue(attempt.Host,out var identity)||!identity.Fresh(DateTimeOffset.UtcNow)||identity.SusClientId==attempt.OldId)continue;
   var record=serverSnapshot58.Records.FirstOrDefault(r=>r.Id.Equals(identity.SusClientId,StringComparison.OrdinalIgnoreCase)&&r.Name.TrimEnd('.').Equals(identity.Name.TrimEnd('.'),StringComparison.OrdinalIgnoreCase));
   if(record!=null)CompleteIdentityRegistration58(attempt,identity,new(){Id=record.Id,Name=record.Name,ReportedUtc=record.Summary?.LastReportedUtc??""});
  }
  Store.Write("identity-repairs58.json",identityRepairs58);
 }
}
public sealed partial class RemoteService {
 public static string IdentityRepairScript58(ClientIdentity58 identity)=>
  "$dcExpectedId58="+PowerShellBridge.Literal(identity.SusClientId)+";$dcExpectedMachine58="+PowerShellBridge.Literal(identity.Machine)+";$dcExpectedInstallation58="+PowerShellBridge.Literal(IdentityPolicy58.Installation(identity)==Guid.Empty?"":identity.InstallationId)+";$dcExpectedName58="+PowerShellBridge.Literal(identity.Name)+";$dcExpectedServer58="+PowerShellBridge.Literal(identity.Server)+";"+IdentityRepairBody58;
 public const string IdentityRepairBody58="""
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.ServiceProcess
$mutex58=New-Object Threading.Mutex($false,'Global\DomainConsole.WsusIdentity58')
$owns58=$false
try {
 try{$owns58=$mutex58.WaitOne(0)}catch [Threading.AbandonedMutexException]{$owns58=$true}
 if(!$owns58){throw 'Перерегистрация уже выполняется'}
 $cs58=Get-CimInstance Win32_ComputerSystem -ErrorAction Stop
 $name58=$env:COMPUTERNAME+$(if($cs58.PartOfDomain){'.'+$cs58.Domain}else{''})
 $machine58=[string](Get-CimInstance Win32_ComputerSystemProduct -ErrorAction Stop).UUID
 $server58=[string](Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate' -ErrorAction Stop).WUServer
 if($name58 -ne $dcExpectedName58 -or $machine58 -ne $dcExpectedMachine58 -or $server58.TrimEnd('/') -ne $dcExpectedServer58.TrimEnd('/')){throw 'Идентичность или сервер клиента изменились; сброс не выполнен'}
 if($dcExpectedInstallation58){
  $installation58=[string](Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography' -Name MachineGuid -ErrorAction Stop).MachineGuid
  if($installation58 -ne $dcExpectedInstallation58){throw 'Установка Windows изменилась; сброс не выполнен'}
 }
 if((Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -ErrorAction Stop).UseWUServer -ne 1){throw 'UseWUServer=1 не подтверждён; сброс не выполнен'}
 $path58='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate'
 $id58=[string](Get-ItemProperty $path58 -ErrorAction Stop).SusClientId
 $parsedId58=[guid]::Empty
 if(![guid]::TryParse($id58,[ref]$parsedId58) -or $parsedId58 -eq [guid]::Empty){throw 'SusClientId пуст или невалиден; автоматический сброс не выполнен'}
 if($id58 -ne $dcExpectedId58){Write-Output ('ID уже изменён: '+$id58+'. Повторного сброса нет.');exit 0}
 $installer58=New-Object -ComObject Microsoft.Update.Installer
 if($installer58.IsBusy){throw 'Установщик Windows Update занят; перерегистрация отложена'}
 $backupFolder58=Join-Path $env:ProgramData 'DomainConsole-Backups'
 New-Item $backupFolder58 -ItemType Directory -Force|Out-Null
 $backup58=Join-Path $backupFolder58 ('WindowsUpdate-'+$env:COMPUTERNAME+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N')+'.reg')
 & reg.exe export 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate' $backup58 /y | Out-Null
 if($LASTEXITCODE -ne 0){throw 'Не удалось сохранить реестр; сброс отменён'}
 $resetError58='';$restoreError58=''
 try{
  Stop-Service wuauserv -ErrorAction Stop
  (Get-Service wuauserv).WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped,[TimeSpan]::FromSeconds(30))
  Remove-ItemProperty $path58 -Name SusClientId -ErrorAction Stop
  if((Get-ItemProperty $path58).PSObject.Properties.Name -contains 'SusClientIdValidation'){Remove-ItemProperty $path58 -Name SusClientIdValidation -ErrorAction Stop}
 }catch{$resetError58=$_.Exception.Message}finally{try{Start-Service wuauserv -ErrorAction Stop;(Get-Service wuauserv).WaitForStatus([ServiceProcess.ServiceControllerStatus]::Running,[TimeSpan]::FromSeconds(30))}catch{$restoreError58=$_.Exception.Message}}
 if($resetError58 -or $restoreError58){throw ('Перерегистрация: '+$resetError58+'; восстановление wuauserv: '+$restoreError58)}
 & "$env:windir\System32\wuauclt.exe" /resetauthorization /detectnow
 $session58=New-Object -ComObject Microsoft.Update.Session
 $session58.ClientApplicationID='DomainConsole.Identity58'
 $search58=$session58.CreateUpdateSearcher();$search58.ServerSelection=1;$search58.Online=$true
 $result58=$search58.Search('IsInstalled=0 and IsHidden=0')
 & "$env:windir\System32\wuauclt.exe" /reportnow
 $new58=[string](Get-ItemProperty $path58 -ErrorAction Stop).SusClientId
 if(!$new58 -or $new58 -eq $dcExpectedId58){throw 'Новый SusClientId не подтверждён'}
 [pscustomobject]@{Computer=$name58;Finished=[DateTime]::UtcNow.ToString('o');OldId=$dcExpectedId58;NewId=$new58;SearchResult=[int]$result58.ResultCode;Backup=$backup58}|Format-List
 if([int]$result58.ResultCode -ne 2){throw 'ID изменён, но поиск завершился не полностью; требуется диагностика'}
 'Новый ID создан; регистрация и отчёт подтверждаются отдельно на сервере. Установка этим шагом не выполнялась.'
}finally{if($owns58){$mutex58.ReleaseMutex()};$mutex58.Dispose()}
""";
}
