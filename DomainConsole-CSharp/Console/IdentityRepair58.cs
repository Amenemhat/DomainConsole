using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class IdentityRepairAttempt58 {
 public string Key {get;set;}="";
 public string Host {get;set;}="";
 public string OldId {get;set;}="";
 public string NewId {get;set;}="";
 public string JobId {get;set;}="";
 public string Phase {get;set;}="Ожидается проверка";
 public string At {get;set;}="";
}
public partial class MainWindow {
 readonly Dictionary<string,IdentityRepairAttempt58> identityRepairs58=Store.Read("identity-repairs58.json",new Dictionary<string,IdentityRepairAttempt58>());
 readonly HashSet<string> identityRepairBusy58=new(StringComparer.OrdinalIgnoreCase);
 async Task RepairIdentityGroup58(string host,bool manual){
  if(closing||App.RecoveryPending57||!identities58.TryGetValue(host,out var observed)||!compromisedIds58.Contains(observed.Key))return;
  if(!identityRepairBusy58.Add(observed.Key))return;
  try{
   var participants=identities58.Values.Where(x=>x.Key==observed.Key).DistinctBy(x=>x.Machine,StringComparer.OrdinalIgnoreCase).ToList();
   if(manual&&MessageBox.Show(this,"Перерегистрировать всех обнаруженных участников общего ID?\n"+string.Join("\n",participants.Select(x=>x.Name))+"\n\nНедоступные и занятые клиенты остаются в ожидании. Записи сервера не удаляются.","Перерегистрация клиентов",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
   foreach(var identity in participants){
    var row=Computers.FirstOrDefault(r=>r.Host.Equals(identity.Host,StringComparison.OrdinalIgnoreCase));if(row==null||row.Target.AdDisabled)continue;
    string key=identity.Key+"|"+identity.Machine.ToLowerInvariant();
    // A persisted attempt survives a crash, even if delivery acknowledgement was lost.
    if(identityRepairs58.TryGetValue(key,out var prior)&&prior.JobId.Length>0&&(!manual||History.Any(j=>j.Id==prior.JobId&&j.Targets.Any(ActiveTarget)))){if(manual)Notify(identity.Name+": попытка ещё активна; проверьте задание "+prior.JobId);continue;}
    if(!identity.Fresh(DateTimeOffset.UtcNow)||identity.Error.Length>0||!ConnectionFresh(row.Host)||HostJobs39(row.Host).Any(x=>ActiveTarget(x.Target))){
     identityRepairs58[key]=new(){Key=key,Host=row.Host,OldId=identity.SusClientId,Phase="Ожидается свежая проверка и завершение активных заданий",At=DateTimeOffset.UtcNow.ToString("o")};continue;
    }
    var frozen=new ComputerRow(ConnectionStore.Capture(row.Target));
    if(!ConnectionStore.CanConnect(frozen.Target))continue;
    try{
     var live=await remote.LiveUpdates(frozen.Target);
     if(live.Busy||live.Jobs.Count>0||live.Limitation.Length>0||live.InstallationEvidence.Length>0){
      identityRepairs58[key]=new(){Key=key,Host=row.Host,OldId=identity.SusClientId,Phase="Ожидается завершение обслуживания или уточнение состояния",At=DateTimeOffset.UtcNow.ToString("o")};continue;
     }
    }catch(Exception ex){identityRepairs58[key]=new(){Key=key,Host=row.Host,OldId=identity.SusClientId,Phase="Проверка активности не завершена: "+ConnectionBrief31(ex.Message),At=DateTimeOffset.UtcNow.ToString("o")};continue;}
    if(!CurrentEndpoint(frozen.Target)||HostJobs39(row.Host).Any(x=>ActiveTarget(x.Target)))continue;
    var job=new RemoteJob{Manual56=manual,AutoInstall54=false,AutoReboot=false,MicrosoftSource=false,OutputChannel="WSUS",Name="Перерегистрация общего SusClientId",Steps=new(){new(){Name="Перерегистрация клиента WSUS",Kind="PowerShell",TouchesUpdates=true,Code=RemoteService.IdentityRepairScript58(identity)}}};
    identityRepairs58[key]=new(){Key=key,Host=row.Host,OldId=identity.SusClientId,JobId=job.Id,Phase="Подготовлено; повторный сброс запрещён",At=DateTimeOffset.UtcNow.ToString("o")};
    Store.Write("identity-repairs58.json",identityRepairs58);
    Event31(row.Host,"identity-repair58",job.Id,"Перерегистрация клиента подготовлена","Info","Прежний ID: "+identity.SusClientId,scope:job.Id);
    bool accepted=await CompletePreparedSubmission47(job,new(){row},new(){frozen},()=>Task.FromResult((Accepted:true,Force:false,Wait:false)),false);
    if(accepted){identityRepairs58[key].Phase="Задание создано; ожидается новый ID и регистрация на сервере";await SendQueue(manual?job.Id:"");}
    else identityRepairs58[key].Phase="Передача не подтверждена; требуется проверка задания";
    Store.Write("identity-repairs58.json",identityRepairs58);
   }
   Store.Write("identity-repairs58.json",identityRepairs58);
  }catch(Exception ex){Event31(host,"identity-repair58-error",ex.Message,"Перерегистрация требует проверки","Warning",ex.Message);}
  finally{identityRepairBusy58.Remove(observed.Key);}
 }
 void ConfirmIdentityRepairs58(){
  if(!SnapshotMatches58)return;
  foreach(var attempt in identityRepairs58.Values.Where(a=>a.Phase!="Подтверждено")){
   if(!identities58.TryGetValue(attempt.Host,out var identity)||!identity.Fresh(DateTimeOffset.UtcNow)||identity.SusClientId==attempt.OldId)continue;
   var record=serverSnapshot58.Records.FirstOrDefault(r=>r.Id.Equals(identity.SusClientId,StringComparison.OrdinalIgnoreCase)&&r.Name.TrimEnd('.').Equals(identity.Name.TrimEnd('.'),StringComparison.OrdinalIgnoreCase));
   if(record==null)continue;attempt.NewId=identity.SusClientId;attempt.Phase="Подтверждено";attempt.At=DateTimeOffset.UtcNow.ToString("o");
   Event31(attempt.Host,"identity-confirmed58",attempt.NewId,"Новая регистрация WSUS подтверждена","Success","Прежний ID: "+attempt.OldId+"; новый ID: "+attempt.NewId+"; имя: "+identity.Name);
  }
  Store.Write("identity-repairs58.json",identityRepairs58);
 }
}
public sealed partial class RemoteService {
 public static string IdentityRepairScript58(ClientIdentity58 identity)=>
  "$dcExpectedId58="+PowerShellBridge.Literal(identity.SusClientId)+";$dcExpectedMachine58="+PowerShellBridge.Literal(identity.Machine)+";$dcExpectedName58="+PowerShellBridge.Literal(identity.Name)+";$dcExpectedServer58="+PowerShellBridge.Literal(identity.Server)+";"+IdentityRepairBody58;
 public const string IdentityRepairBody58="""
$ErrorActionPreference='Stop'
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
 $path58='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate'
 $id58=[string](Get-ItemProperty $path58 -ErrorAction Stop).SusClientId
 if($id58 -ne $dcExpectedId58){Write-Output ('ID уже изменён: '+$id58+'. Повторного сброса нет.');exit 0}
 $installer58=New-Object -ComObject Microsoft.Update.Installer
 if($installer58.IsBusy){throw 'Установщик Windows Update занят; перерегистрация отложена'}
 $backupFolder58=Join-Path $env:ProgramData 'DomainConsole-Backups'
 New-Item $backupFolder58 -ItemType Directory -Force|Out-Null
 $backup58=Join-Path $backupFolder58 ('WindowsUpdate-'+$env:COMPUTERNAME+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N')+'.reg')
 & reg.exe export 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate' $backup58 /y | Out-Null
 if($LASTEXITCODE -ne 0){throw 'Не удалось сохранить реестр; сброс отменён'}
 try{
  Stop-Service wuauserv -ErrorAction Stop
  (Get-Service wuauserv).WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped,[TimeSpan]::FromSeconds(30))
  Remove-ItemProperty $path58 -Name SusClientId -ErrorAction Stop
  if((Get-ItemProperty $path58).PSObject.Properties.Name -contains 'SusClientIdValidation'){Remove-ItemProperty $path58 -Name SusClientIdValidation -ErrorAction Stop}
 }finally{Start-Service wuauserv -ErrorAction Stop}
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
