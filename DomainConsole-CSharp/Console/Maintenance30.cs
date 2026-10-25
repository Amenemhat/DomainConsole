using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class AttentionRow30 {public string Host {get;set;}="";public string Text {get;set;}="";public Action Stop {get;set;}=()=>{};}
public sealed class ReportWait30 {public int Attempts42 {get;set;}public string Id {get;set;}="";public DateTimeOffset Until {get;set;}public DateTimeOffset Next {get;set;}}
public sealed class MaintenancePlan {public string DeadlineUtc44 {get;set;}="";public bool Released37 {get;set;}public string PlanId37 {get;set;}="";
 public string Host {get;set;}="";public string Identity {get;set;}="";public string JobId {get;set;}="";public string Phase {get;set;}="";public string BootUtc {get;set;}="";public string StartedUtc {get;set;}=DateTime.UtcNow.ToString("o");public string NextJobId {get;set;}="";public bool Install {get;set;}public bool Done {get;set;}public int Cycles {get;set;}public DateTimeOffset Next {get;set;}public DateTimeOffset ReadyAfter {get;set;}public string Detail {get;set;}="";
}
public partial class MainWindow {
 readonly Dictionary<string,MaintenancePlan> maintenancePlans=new(Store.Read("maintenance-plans.json",new Dictionary<string,MaintenancePlan>()),StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,string> observedReturns=new(Store.Read("observed-return-scans.json",new Dictionary<string,string>()),StringComparer.OrdinalIgnoreCase);
 bool advancingMaintenance;
 static bool ActiveTarget(TargetRecord t)=>t.Status is "Queued" or "Sending" or "Submitted" or "Running" or "SubmissionUnknown";
 static bool TerminalSuccess(TargetRecord t)=>t.Status is "Completed" or "VerifiedAfterReboot" or "AwaitingReboot" or "RebootScheduled" or "RebootObserved";
 void SaveMaintenance()=>Store.Write("maintenance-plans.json",maintenancePlans);
 void BeginMaintenance(RemoteJob job){if(job.PlanId37.Length==0)job.PlanId37=job.Id;foreach(var t in job.Targets){if(maintenancePlans.TryGetValue(t.Host,out var existing44)&&(existing44.PlanId37==job.PlanId37||existing44.JobId==job.Id))continue;maintenancePlans[t.Host]=new(){StartedUtc=job.Created,DeadlineUtc44=DateTimeOffset.Parse(job.Created).AddDays(2).ToString("o"),PlanId37=job.PlanId37,Host=t.Host,Identity=t.ConnectionIdentity,JobId=job.Id,Install=true,Phase="Выполняется полное обслуживание",Cycles=1};}SaveMaintenance();}
 // A positive indication from the previous boot is history, never a fresh request to restart again.
 void ObserveUnplannedReturn(string host,RebootSnapshot? old,RebootSnapshot next){
  if(!RebootSnapshotPolicy.NewBoot(old,next)&&!RebootSnapshotPolicy.SupersedesPositive(old,next))return;
  bool previous=old!.Required==true||old.PreservedRequired==true||History.SelectMany(j=>j.Targets).Any(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&t.State?.RebootRequired==true&&DateTimeOffset.TryParse(t.State.Ended,out var ended)&&DateTimeOffset.TryParse(old.BootUtc,out var prior)&&DateTimeOffset.TryParse(next.BootUtc,out var boot)&&ended>=prior&&ended<boot);
  if(!previous||observedReturns.GetValueOrDefault(host)==next.BootUtc)return;
  if(rebootWatches.TryGetValue(host,out var watch)&&!watch.Done)return;
  if(maintenancePlans.TryGetValue(host,out var active)&&!active.Done)return;
  maintenancePlans[host]=new(){Host=host,Identity=ConnectionStore.Capture(new TargetRecord{Host=host}).ConnectionIdentity,BootUtc=next.BootUtc,Install=AutoServiceEnabled,Phase="После перезагрузки: ожидание готовности",ReadyAfter=DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(postReboot.SettleSeconds,0,1800)),Next=DateTimeOffset.UtcNow};SaveMaintenance();observedReturns[host]=next.BootUtc;Store.Write("observed-return-scans.json",observedReturns);
 }
 async Task AdvanceMaintenance(Func<ComputerRow,Task<RebootSnapshot?>>? statusProbe=null,Func<TargetRecord,Task<LiveUpdateActivity>>? activityProbe=null,bool dispatch=true){if(TransportBudget41.Paused||advancingMaintenance||closing)return;statusProbe??=r=>ReadClientStatus(r,force:true);activityProbe??=remote.LiveUpdates;advancingMaintenance=true;try{
  foreach(var p in maintenancePlans.Values.Where(p=>!p.Done&&p.Next<=DateTimeOffset.UtcNow).Take(3).ToList()){
   NormalizePlan44(p);p.Next=DateTimeOffset.UtcNow.AddMinutes(1);if(!AutoServiceEnabled&&p.Install){p.Done=true;p.Phase="Автоматическое продолжение выключено";continue;}var row=Computers.FirstOrDefault(r=>r.Host.Equals(p.Host,StringComparison.OrdinalIgnoreCase));
   if(row==null||row.Target.AdDisabled){p.Done=true;p.Phase="Требуется действие";p.Detail=row==null?"Компьютер отсутствует в рабочем списке":"Учётная запись отключена в AD";continue;}
   if(ForeignControl37(p.Host)){p.Done=true;p.Phase="Требуется действие";p.Detail="Управление передано другой консоли; автоматическое продолжение прекращено";continue;}
   var captured=ConnectionStore.Capture(row.Target);if(captured.ConnectionIdentity!=p.Identity){p.Done=true;p.Phase="Требуется действие";p.Detail="Идентичность подключения изменилась; проверьте цель перед продолжением";continue;}
   if(PlanExpired44(p,DateTimeOffset.UtcNow)){p.Done=true;p.Phase="Требуется действие";p.Detail="Срок автоматического продолжения истёк (2 суток)";continue;}
   var job=History.FirstOrDefault(j=>j.Id==p.JobId);var target=job?.Targets.FirstOrDefault(t=>t.Host.Equals(p.Host,StringComparison.OrdinalIgnoreCase));
   if(target!=null&&ActiveTarget(target)){p.Phase="Выполняется обслуживание";continue;}
   if(target!=null&&!TerminalSuccess(target)){if(target.Status is "ConnectionFailed" or "Unavailable" or "AccessDenied" or "SubmissionUnknown"){p.Detail="Связь/передача не подтверждена; требуется проверка результата, повторный запуск не выполняется";continue;}p.Done=true;p.Phase=target.Status=="Cancelled"?"Обслуживание остановлено":"Требуется действие";p.Detail="Автоматическое продолжение прекращено: "+ComputerRow.StatusText(target.Status)+". См. результат задания.";continue;}
   if(p.Phase=="Ожидается перезагрузка"){
    var snapshot=await statusProbe(row).WaitAsync(TimeSpan.FromSeconds(105));if(p.Done)continue;if(snapshot==null){p.Detail="Клиент пока не ответил; повторный опрос запланирован";continue;}
    if(!RebootSnapshotPolicy.NewBoot(new(){BootUtc=p.BootUtc},snapshot)){if(snapshot.Required==false&&(!rebootWatches.TryGetValue(p.Host,out var scheduledWatch32)||scheduledWatch32.Done)&&HeaderRebootJob(row)==null){p.Phase="Флаги перезагрузки сняты";p.Detail="Свежий опрос не подтвердил требование перезагрузки; обслуживание продолжается";p.Next=DateTimeOffset.UtcNow;}else p.Detail="Перезагрузите компьютер в согласованное время; продолжение сохранено";continue;}
    p.BootUtc=snapshot.BootUtc;p.Phase="После перезагрузки: ожидание готовности";p.ReadyAfter=DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(postReboot.SettleSeconds,0,1800));p.Detail="";continue;
   }
   if(DateTimeOffset.UtcNow<p.ReadyAfter)continue;
   // Current operation may have completed with a reboot requirement. Observe once and wait for a new boot.
   if(target!=null&&p.Phase!="После перезагрузки: ожидание готовности"){
    var snapshot=await statusProbe(row).WaitAsync(TimeSpan.FromSeconds(105));if(p.Done)continue;if(snapshot==null){p.Detail="Не удалось обновить статус клиента; будет повторный опрос";continue;}
    if(DateTimeOffset.TryParse(snapshot.BootUtc,out var completedBoot)&&DateTimeOffset.TryParse(target.State?.Ended,out var completedAt)&&completedBoot>completedAt&&p.BootUtc!=snapshot.BootUtc){p.BootUtc=snapshot.BootUtc;p.Phase="После перезагрузки: ожидание готовности";p.ReadyAfter=DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(postReboot.SettleSeconds,0,1800));p.Detail="";continue;}
    if(snapshot.Required==true){
     p.Phase="Ожидается перезагрузка";p.BootUtc=snapshot.BootUtc;p.Detail="Перезагрузите в согласованное время. После возврата обслуживание продолжится автоматически.";continue;
    }
    if(snapshot.Required==null){p.Done=true;p.Phase="Требуется действие";p.Detail="Установка завершена; проверка флагов неполная. Уточните статус клиента перед новым циклом.";continue;}
    if(!p.Install||target.State?.UpdatesRemaining==0){p.Done=true;p.Phase="Обслуживание завершено";p.Detail="Результат сохранён; получение отчёта WSUS отслеживается отдельно.";continue;}
   }
   if(History.Any(j=>IsWsusJob(j)&&j.Targets.Any(t=>t.Host.Equals(p.Host,StringComparison.OrdinalIgnoreCase)&&ActiveTarget(t)))){p.Detail="Ожидается завершение другого задания WSUS";continue;}
   if(p.Install&&p.Phase=="После перезагрузки: ожидание готовности"){
    var fresh=await statusProbe(row).WaitAsync(TimeSpan.FromSeconds(105));if(p.Done)continue;if(fresh==null){p.Detail="Ожидается свежий снимок клиента";continue;}
    if(fresh.Required!=false){p.Phase=fresh.Required==true?"Ожидается перезагрузка":"Требуется действие";p.Done=fresh.Required==null;p.BootUtc=fresh.BootUtc;p.Detail=fresh.Required==true?"После загрузки подтверждено новое требование перезагрузки; источники в журнале":"Не удалось уточнить флаги; установка автоматически не продолжается";continue;}
   }
   if(p.Cycles>=3){p.Done=true;p.Phase="Требуется действие";p.Detail="Достигнут предел: 3 цикла. Проверьте оставшиеся обновления; автоматический повтор остановлен.";continue;}
   try{var live=await activityProbe(captured).WaitAsync(TimeSpan.FromSeconds(105));if(live.InstallerBusy||live.InstallationEvidence.Length>0||live.Limitation.Length>0){p.Detail="Клиент занят или готовность не подтверждена; повторный опрос запланирован";continue;}}
   catch(Exception ex){p.Detail="Проверка готовности не удалась: "+ShortConnectionError(ex.Message);continue;}
   if(closing||TransportBudget41.Paused||p.Done)continue;QueueMaintenanceJob(p,captured);
  }
 }catch(TimeoutException){foreach(var p44 in maintenancePlans.Values.Where(p=>!p.Done)){p44.Detail="Проверка состояния затянулась; клиентские задания не остановлены. Повторная проверка через 2 минуты.";p44.Next=DateTimeOffset.UtcNow.AddMinutes(2);}}finally{foreach(var entry in maintenancePlans.Values)Event31(entry.Host,"maintenance",entry.Phase+"|"+entry.Detail,entry.Phase,entry.Phase=="Требуется действие"?"Warning":"Info",entry.Detail);SaveMaintenance();advancingMaintenance=false;RefreshAttentionButton();if(ComputerGrid.SelectedItem is ComputerRow selected&&maintenancePlans.ContainsKey(selected.Host))ShowCurrent();if(dispatch&&!TransportBudget41.Paused){await ReleaseFinishedPlans37();await SendQueue();}}}
 void QueueMaintenanceJob(MaintenancePlan p,TargetRecord captured){
  // Persist the id before creating history/queue: recovery reuses the same job, never repeats deployment with a new id.
  if(p.NextJobId.Length==0){p.NextJobId=Guid.NewGuid().ToString();SaveMaintenance();}
  if(p.PlanId37.Length==0)p.PlanId37=p.JobId.Length>0?p.JobId:p.NextJobId;
  var job=History.FirstOrDefault(j=>j.Id==p.NextJobId);
  if(job==null){var t=Store.Clone(captured);t.Status="Queued";t.State=null;t.Output="";job=new(){PlanId37=p.PlanId37,SearchTimeoutMinutes37=Store.Read("search-minutes37.json",20),AutomaticChild31=true,Id=p.NextJobId,Name=p.Install?"Полное обслуживание · продолжение":"После перезагрузки · поиск и отчёт",OutputChannel="WSUS",Steps=new(){new(){Kind=p.Install?"Updates":"UpdateScan",Name=p.Install?"WSUS · установка и запрос отчёта":"WSUS · проверка и запрос отчёта"}},Targets=new(){t}};History.Insert(0,job);Save();}
  if(p.JobId!=job.Id)p.Cycles++;p.JobId=job.Id;p.NextJobId="";p.ReadyAfter=default;p.Phase="Выполняется обслуживание";p.Detail="";SaveMaintenance();
  foreach(var t in job.Targets.Where(t=>t.Status=="Queued"&&t.State==null))if(!queue.Any(q=>q.Job.Id==job.Id&&q.Target.Host==t.Host))queue.Enqueue((job,t));
 }
 void RestoreMaintenanceQueues(){foreach(var p in maintenancePlans.Values.Where(p=>!p.Done)){
  var id=p.NextJobId.Length>0?p.NextJobId:p.JobId;var job=History.FirstOrDefault(j=>j.Id==id);if(job==null)continue;
  foreach(var t in job.Targets.Where(t=>t.Status=="Queued"&&t.State==null))if(!queue.Any(q=>q.Job.Id==job.Id&&q.Target.Host==t.Host))queue.Enqueue((job,t));
 }}
 void ExpirePlans44(){foreach(var p in maintenancePlans.Values)NormalizePlan44(p);foreach(var p in maintenancePlans.Values.Where(p=>!p.Done&&PlanExpired44(p,DateTimeOffset.UtcNow)).ToList()){FinishPlan44(p.Host);p.Phase="Требуется действие";p.Detail="Срок автоматического продолжения истёк (2 суток). Работающие клиентские задания не остановлены.";SaveMaintenance();}}

 string MaintenanceText(string host)=>maintenancePlans.TryGetValue(host,out var p)?"\n\n## Обслуживание клиента\n"+p.Phase+(p.Detail.Length>0?"\n"+p.Detail:"")+"\nПлан начат: "+Display36.Date(p.StartedUtc,true)+" · задание: "+p.JobId+"\nРежим: "+(p.Install?"полное обслуживание":"только поиск и отчёт")+"; циклов: "+p.Cycles+" / 3":"";
 void RefreshAttentionButton(){if(AttentionButton==null)return;var rows=AttentionEntries36().Where(x=>x.Required).ToList();AttentionButton.Content="Требуют внимания · "+rows.Count;AttentionButton.Foreground=rows.Any(x=>x.Tone=="Error")?Theme.Tone("Error"):rows.Count>0?Theme.Tone("Warning"):Theme.Brush("TextPrimary");}
 void Attention_Click(object sender,RoutedEventArgs e)=>AttentionWindow36().ShowDialog();
 async void FullMaintenance_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{ReadWsus();MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;await Submit(new(){new(){Kind="Updates",Name="Полное обслуживание · установка и запрос отчёта"}},"WSUS",fullMaintenance:true);});
}


