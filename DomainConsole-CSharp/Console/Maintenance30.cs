using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class AttentionRow30 {public string Host {get;set;}="";public string Text {get;set;}="";public Action Stop {get;set;}=()=>{};}
public sealed class ReportWait30 {public string Id {get;set;}="";public DateTimeOffset Until {get;set;}public DateTimeOffset Next {get;set;}}
public sealed class MaintenancePlan {
 public string Host {get;set;}="";public string Identity {get;set;}="";public string JobId {get;set;}="";public string Phase {get;set;}="";public string BootUtc {get;set;}="";public string StartedUtc {get;set;}=DateTime.UtcNow.ToString("o");public string NextJobId {get;set;}="";public bool Install {get;set;}public bool Done {get;set;}public int Cycles {get;set;}public DateTimeOffset Next {get;set;}public DateTimeOffset ReadyAfter {get;set;}public string Detail {get;set;}="";
}
public partial class MainWindow {
 readonly Dictionary<string,MaintenancePlan> maintenancePlans=new(Store.Read("maintenance-plans.json",new Dictionary<string,MaintenancePlan>()),StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,string> observedReturns=new(Store.Read("observed-return-scans.json",new Dictionary<string,string>()),StringComparer.OrdinalIgnoreCase);
 bool advancingMaintenance;
 static bool ActiveTarget(TargetRecord t)=>t.Status is "Queued" or "Sending" or "Submitted" or "Running" or "SubmissionUnknown";
 static bool TerminalSuccess(TargetRecord t)=>t.Status is "Completed" or "VerifiedAfterReboot" or "AwaitingReboot" or "RebootScheduled" or "RebootObserved";
 void SaveMaintenance()=>Store.Write("maintenance-plans.json",maintenancePlans);
 void BeginMaintenance(RemoteJob job){foreach(var t in job.Targets){maintenancePlans[t.Host]=new(){Host=t.Host,Identity=t.ConnectionIdentity,JobId=job.Id,Install=true,Phase="Выполняется полное обслуживание",Cycles=1};}SaveMaintenance();}
 // A positive indication from the previous boot is history, never a fresh request to restart again.
 void ObserveUnplannedReturn(string host,RebootSnapshot? old,RebootSnapshot next){
  if(!RebootSnapshotPolicy.NewBoot(old,next)&&!RebootSnapshotPolicy.SupersedesPositive(old,next))return;
  bool previous=old!.Required==true||old.PreservedRequired==true||History.SelectMany(j=>j.Targets).Any(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&t.State?.RebootRequired==true&&DateTimeOffset.TryParse(t.State.Ended,out var ended)&&DateTimeOffset.TryParse(old.BootUtc,out var prior)&&DateTimeOffset.TryParse(next.BootUtc,out var boot)&&ended>=prior&&ended<boot);
  if(!previous||observedReturns.GetValueOrDefault(host)==next.BootUtc)return;
  if(rebootWatches.TryGetValue(host,out var watch)&&!watch.Done)return;
  if(maintenancePlans.TryGetValue(host,out var active)&&!active.Done)return;
  maintenancePlans[host]=new(){Host=host,Identity=ConnectionStore.Capture(new TargetRecord{Host=host}).ConnectionIdentity,BootUtc=next.BootUtc,Phase="После перезагрузки: ожидание готовности",ReadyAfter=DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(postReboot.SettleSeconds,0,1800)),Next=DateTimeOffset.UtcNow};SaveMaintenance();observedReturns[host]=next.BootUtc;Store.Write("observed-return-scans.json",observedReturns);
 }
 async Task AdvanceMaintenance(Func<ComputerRow,Task<RebootSnapshot?>>? statusProbe=null,Func<TargetRecord,Task<LiveUpdateActivity>>? activityProbe=null,bool dispatch=true){if(advancingMaintenance||closing)return;statusProbe??=r=>ReadClientStatus(r,force:true);activityProbe??=remote.LiveUpdates;advancingMaintenance=true;try{
  foreach(var p in maintenancePlans.Values.Where(p=>!p.Done&&p.Next<=DateTimeOffset.UtcNow).Take(3).ToList()){
   p.Next=DateTimeOffset.UtcNow.AddMinutes(1);var row=Computers.FirstOrDefault(r=>r.Host.Equals(p.Host,StringComparison.OrdinalIgnoreCase));
   if(row==null||row.Target.AdDisabled){p.Done=true;p.Phase="Требуется действие";p.Detail=row==null?"Компьютер отсутствует в рабочем списке":"Учётная запись отключена в AD";continue;}
   var captured=ConnectionStore.Capture(row.Target);if(captured.ConnectionIdentity!=p.Identity){p.Done=true;p.Phase="Требуется действие";p.Detail="Идентичность подключения изменилась; проверьте цель перед продолжением";continue;}
   if(DateTimeOffset.TryParse(p.StartedUtc,out var since)&&DateTimeOffset.UtcNow-since>TimeSpan.FromDays(2)){p.Done=true;p.Phase="Требуется действие";p.Detail="Срок автоматического продолжения истёк (2 суток)";continue;}
   var job=History.FirstOrDefault(j=>j.Id==p.JobId);var target=job?.Targets.FirstOrDefault(t=>t.Host.Equals(p.Host,StringComparison.OrdinalIgnoreCase));
   if(target!=null&&ActiveTarget(target)){p.Phase="Выполняется обслуживание";continue;}
   if(target!=null&&!TerminalSuccess(target)){p.Done=true;p.Phase=target.Status=="Cancelled"?"Обслуживание остановлено":"Требуется действие";p.Detail="Автоматическое продолжение прекращено: "+ComputerRow.StatusText(target.Status)+". См. результат задания.";continue;}
   if(p.Phase=="Ожидается перезагрузка"){
    var snapshot=await statusProbe(row);if(snapshot==null){p.Detail="Клиент пока не ответил; повторный опрос запланирован";continue;}
    if(!RebootSnapshotPolicy.NewBoot(new(){BootUtc=p.BootUtc},snapshot)){p.Detail="Перезагрузите компьютер в согласованное время; продолжение сохранено";continue;}
    p.BootUtc=snapshot.BootUtc;p.Phase="После перезагрузки: ожидание готовности";p.ReadyAfter=DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(postReboot.SettleSeconds,0,1800));p.Detail="";continue;
   }
   if(DateTimeOffset.UtcNow<p.ReadyAfter)continue;
   // Current operation may have completed with a reboot requirement. Observe once and wait for a new boot.
   if(target!=null&&p.Phase!="После перезагрузки: ожидание готовности"){
    var snapshot=await statusProbe(row);if(snapshot==null){p.Detail="Не удалось обновить статус клиента; будет повторный опрос";continue;}
    if(DateTimeOffset.TryParse(snapshot.BootUtc,out var completedBoot)&&DateTimeOffset.TryParse(target.State?.Ended,out var completedAt)&&completedBoot>completedAt&&p.BootUtc!=snapshot.BootUtc){p.BootUtc=snapshot.BootUtc;p.Phase="После перезагрузки: ожидание готовности";p.ReadyAfter=DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(postReboot.SettleSeconds,0,1800));p.Detail="";continue;}
    if(snapshot.Required==true){
     p.Phase="Ожидается перезагрузка";p.BootUtc=snapshot.BootUtc;p.Detail="Перезагрузите в согласованное время. После возврата обслуживание продолжится автоматически.";continue;
    }
    if(snapshot.Required==null){p.Done=true;p.Phase="Требуется действие";p.Detail="Установка завершена; проверка флагов неполная. Уточните статус клиента перед новым циклом.";continue;}
    if(!p.Install||target.State?.UpdatesRemaining==0){p.Done=true;p.Phase="Обслуживание завершено";p.Detail="Результат сохранён; получение отчёта WSUS отслеживается отдельно.";continue;}
   }
   if(History.Any(j=>IsWsusJob(j)&&j.Targets.Any(t=>t.Host.Equals(p.Host,StringComparison.OrdinalIgnoreCase)&&ActiveTarget(t)))){p.Detail="Ожидается завершение другого задания WSUS";continue;}
   if(p.Install&&p.Phase=="После перезагрузки: ожидание готовности"){
    var fresh=await statusProbe(row);if(fresh==null){p.Detail="Ожидается свежий снимок клиента";continue;}
    if(fresh.Required!=false){p.Phase=fresh.Required==true?"Ожидается перезагрузка":"Требуется действие";p.Done=fresh.Required==null;p.BootUtc=fresh.BootUtc;p.Detail=fresh.Required==true?"После загрузки подтверждено новое требование перезагрузки; источники в журнале":"Не удалось уточнить флаги; установка автоматически не продолжается";continue;}
   }
   if(p.Cycles>=3){p.Done=true;p.Phase="Требуется действие";p.Detail="Достигнут предел: 3 цикла. Проверьте оставшиеся обновления; автоматический повтор остановлен.";continue;}
   try{var live=await activityProbe(captured);if(live.InstallerBusy||live.InstallationEvidence.Length>0||live.Limitation.Length>0){p.Detail="Клиент занят или готовность не подтверждена; повторный опрос запланирован";continue;}}
   catch(Exception ex){p.Detail="Проверка готовности не удалась: "+ShortConnectionError(ex.Message);continue;}
   if(closing||p.Done)continue;QueueMaintenanceJob(p,captured);
  }
 }finally{SaveMaintenance();advancingMaintenance=false;RefreshAttentionButton();if(ComputerGrid.SelectedItem is ComputerRow selected&&maintenancePlans.ContainsKey(selected.Host))ShowCurrent();if(dispatch)await SendQueue();}}
 void QueueMaintenanceJob(MaintenancePlan p,TargetRecord captured){
  // Persist the id before creating history/queue: recovery reuses the same job, never repeats deployment with a new id.
  if(p.NextJobId.Length==0){p.NextJobId=Guid.NewGuid().ToString();SaveMaintenance();}
  var job=History.FirstOrDefault(j=>j.Id==p.NextJobId);
  if(job==null){var t=Store.Clone(captured);t.Status="Queued";t.State=null;t.Output="";job=new(){Id=p.NextJobId,Name=p.Install?"Полное обслуживание · продолжение":"После перезагрузки · поиск и отчёт",OutputChannel="WSUS",Steps=new(){new(){Kind=p.Install?"Updates":"UpdateScan",Name=p.Install?"WSUS · установка и запрос отчёта":"WSUS · проверка и запрос отчёта"}},Targets=new(){t}};History.Insert(0,job);Save();}
  if(p.JobId!=job.Id)p.Cycles++;p.JobId=job.Id;p.NextJobId="";p.ReadyAfter=default;p.Phase="Выполняется обслуживание";p.Detail="";SaveMaintenance();
  foreach(var t in job.Targets.Where(t=>t.Status=="Queued"&&t.State==null))if(!queue.Any(q=>q.Job.Id==job.Id&&q.Target.Host==t.Host))queue.Enqueue((job,t));
 }
 void RestoreMaintenanceQueues(){foreach(var p in maintenancePlans.Values.Where(p=>!p.Done)){
  var id=p.NextJobId.Length>0?p.NextJobId:p.JobId;var job=History.FirstOrDefault(j=>j.Id==id);if(job==null)continue;
  foreach(var t in job.Targets.Where(t=>t.Status=="Queued"&&t.State==null))if(!queue.Any(q=>q.Job.Id==job.Id&&q.Target.Host==t.Host))queue.Enqueue((job,t));
 }}
 string MaintenanceText(string host)=>maintenancePlans.TryGetValue(host,out var p)?"\n\n## Обслуживание клиента\n"+p.Phase+(p.Detail.Length>0?"\n"+p.Detail:"")+"\nРежим: "+(p.Install?"полное обслуживание":"только поиск и отчёт")+"; циклов: "+p.Cycles+" / 3":"";
 void RefreshAttentionButton(){if(AttentionButton!=null)AttentionButton.Content="Требуют внимания · "+(maintenancePlans.Values.Count(p=>p.Phase is "Требуется действие" or "Ожидается перезагрузка")+reportRecoveries.Values.Count(p=>p.Phase=="Требуется действие"));}
 void Attention_Click(object sender,RoutedEventArgs e){
  var rows=maintenancePlans.Values.Where(p=>!p.Done||p.Phase=="Требуется действие").Select(p=>new AttentionRow30{Host=p.Host,Text=p.Host+" · "+p.Phase+"\n"+p.Detail,Stop=()=>{p.Done=true;p.Phase="Обслуживание остановлено";p.Detail="Остановлено пользователем; уже переданное задание не отменяется";SaveMaintenance();}}).Concat(reportRecoveries.Where(e=>!e.Value.Done||e.Value.Phase=="Требуется действие").Select(e=>new AttentionRow30{Host=e.Key,Text=e.Key+" · "+e.Value.Phase+"\n"+e.Value.Detail,Stop=()=>{CancelUnsentReportRecoveries(e.Key);e.Value.Done=true;e.Value.Phase="Автоматическое продолжение остановлено";SaveReportRecoveries();}})).ToList();
  var window=new Window{Owner=this,Title="Обслуживание · требуется действие",Width=720,Height=420,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new DockPanel{Margin=new(14)};var list=new ListBox{DisplayMemberPath="Text",ItemsSource=rows};var buttons=new WrapPanel();DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);var open=new Button{Content="Открыть компьютер"};open.Click+=(s,a)=>{if(list.SelectedItem is not AttentionRow30 entry)return;var row=Computers.FirstOrDefault(r=>r.Host.Equals(entry.Host,StringComparison.OrdinalIgnoreCase));if(row!=null){ComputerGrid.SelectedItem=row;MainTabs.SelectedItem=WsusTab;ShowCurrent();window.Close();}};buttons.Children.Add(open);var stop=new Button{Content="Прекратить автоматическое продолжение"};stop.Click+=(s,a)=>{if(list.SelectedItem is not AttentionRow30 entry)return;entry.Stop();RefreshAttentionButton();window.Close();};buttons.Children.Add(stop);panel.Children.Add(list);window.Content=panel;window.ShowDialog();
 }
 async void FullMaintenance_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{ReadWsus();MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;await Submit(new(){new(){Kind="Updates",Name="Полное обслуживание · установка и запрос отчёта"}},"WSUS",fullMaintenance:true);});
}
