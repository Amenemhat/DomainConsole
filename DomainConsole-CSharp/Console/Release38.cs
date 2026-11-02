using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class AdmissionReply38 {public bool Allowed {get;set;}public string ReasonCode {get;set;}="";public string Message {get;set;}="";public string ActiveJobId {get;set;}="";public string PlanId {get;set;}="";}
public partial class MainWindow {
 void MigrateAdmission38(){foreach(var job in History)foreach(var target in job.Targets){string error=target.State?.Error??"";if(target.Status=="InterventionRequired"&&target.State?.Results.Count==0&&(error.Contains("Владелец плана изменился; автоматическое продолжение не запускалось.",StringComparison.Ordinal) || error.Contains("‚« ¤Ґ«Ґж Ї« ­  Ё§¬Ґ­Ё«бп;  ўв®¬ вЁзҐбЄ®Ґ Їа®¤®«¦Ґ­ЁҐ ­Ґ § ЇгбЄ «®бм.",StringComparison.Ordinal) || error.Contains("┬ырфхыхЎ яырэр шчьхэшыё ; ртЄюьрЄшўхёъюх яЁюфюыцхэшх эх чряєёърыюё№.",StringComparison.Ordinal))){target.Status="LaunchRejected";target.State.Status="LaunchRejected";target.Output="## Запуск не выполнен\nПредыдущая версия отклонила запуск нового плана. Выполнявшееся задание этим не отменялось.";if(maintenancePlans.TryGetValue(target.Host,out var plan)&&plan.JobId==job.Id){var previous=History.FirstOrDefault(j=>j.Id!=job.Id&&IsWsusJob(j)&&j.Targets.Any(t=>t.Host.Equals(target.Host,StringComparison.OrdinalIgnoreCase)&&ActiveTarget(t)));if(previous!=null){var prior=previous.Targets.First(t=>t.Host.Equals(target.Host,StringComparison.OrdinalIgnoreCase));plan.JobId=previous.Id;plan.PlanId37=previous.PlanId37;plan.Identity=prior.ConnectionIdentity;plan.Done=false;plan.Phase="Выполняется обслуживание";plan.Detail="";}}}}SaveMaintenance();}
 double eventsHeight38=Math.Clamp(Store.Read("events-height38.json",150d),65,300);
 void EventsResize38(object sender,System.Windows.Controls.Primitives.DragDeltaEventArgs e){eventsHeight38=Math.Clamp(EventsRow38.ActualHeight+e.VerticalChange,65,Math.Max(65,ActualHeight*0.35));EventsRow38.Height=new GridLength(eventsHeight38);Store.Write("events-height38.json",eventsHeight38);}
 ComputerRow OperationRow38(ComputerRow selected){
  var active=History.FirstOrDefault(j=>IsWsusJob(j)==(MainTabs.SelectedItem==WsusTab)&&j.Targets.Any(t=>t.Host.Equals(selected.Host,StringComparison.OrdinalIgnoreCase)&&IsCurrentExecution(t.Status)));
  if(active!=null)return new(active.Targets.First(t=>t.Host.Equals(selected.Host,StringComparison.OrdinalIgnoreCase)),active.Id);
  return ExecutionRow(selected,MainTabs.SelectedItem==WsusTab);
 }
 void BeginAcceptedPlan38(RemoteJob job,TargetRecord target){if(AttachReportContinuation52(job,target.Host))return;if(maintenancePlans.TryGetValue(target.Host,out var old44)&&(old44.PlanId37==job.PlanId37||old44.JobId==job.Id))return;maintenancePlans[target.Host]=new(){StartedUtc=job.Created,DeadlineUtc44=DateTimeOffset.Parse(job.Created).AddDays(2).ToString("o"),PlanId37=job.PlanId37,Host=target.Host,Identity=target.ConnectionIdentity,JobId=job.Id,Install=true,Phase="Выполняется полное обслуживание",Cycles=job.Steps.Any(s=>s.Kind=="Updates")?1:0};SaveMaintenance();}
 public static AdmissionReply38 ParseAdmission38(string text){
  var match=Regex.Match(text,@"DC_ADMISSION38\|([A-Za-z0-9+/=]+)");if(match.Success){try{return JsonSerializer.Deserialize<AdmissionReply38>(Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value)),Store.Options)??new();}catch{}}
  return new(){ReasonCode="unknown",Message="Новый запуск не выполнен: допуск не подтверждён. Текущее задание продолжает наблюдаться."};
 }
 void RejectAdmission38(RemoteJob job,TargetRecord target,Exception error){var reply=ParseAdmission38(error.Message);target.Status="LaunchRejected";target.State=new(){Id=job.Id,Status="LaunchRejected",Ended=DateTime.UtcNow.ToString("o")};target.Output="## Запуск не выполнен\n"+reply.Message+"\nВыполнявшееся задание не отменено. Его статус отображается отдельно.";Event31(target.Host,"admission",job.Id,reply.Message,"Warning",reply.ActiveJobId.Length>0?"Действующее задание: "+reply.ActiveJobId:"");Notify(reply.Message);}
 void StopCurrent38_Click(object sender,RoutedEventArgs e){if(ComputerGrid.SelectedItem is not ComputerRow row)return;var operation=OperationRow38(row);if(operation.JobId.Length==0)return;var job=History.FirstOrDefault(j=>j.Id==operation.JobId);if(job==null)return;historyViewId=job.Id;MainTabs.SelectedItem=IsWsusJob(job)?WsusTab:CommandsTab;Cancel_Click(sender,e);}
}

