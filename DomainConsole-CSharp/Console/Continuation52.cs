using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void PrepareReportContinuation52(RemoteJob job,string host){
  job.AutomaticChild31=true;
  var previous=maintenancePlans.GetValueOrDefault(host);
  job.BeginPlan38=AutoServiceEnabled&&!(previous?.Done==true&&previous.Phase.Contains("остановлено"));
  if(job.BeginPlan38)job.PlanId37=previous is {Done:false}?previous.PlanId37:job.Id;
 }
 bool AttachReportContinuation52(RemoteJob job,string host){
  if(!job.AutomaticChild31||!job.Steps.Any(s=>s.Kind=="ReportQueueVerify")||!maintenancePlans.TryGetValue(host,out var p)||p.PlanId37!=job.PlanId37)return false;
  if(!p.Done){p.JobId=job.Id;p.NextJobId="";p.Phase="Восстановление отчётности";p.Next=DateTimeOffset.UtcNow;SaveMaintenance();}
  return true;
 }
 void ResumeConnection52(string host,DateTimeOffset previous,DateTimeOffset now){
  if(TransportBudget41.Paused||!maintenancePlans.TryGetValue(host,out var plan)||plan.Done)return;
  if(previous==default||now-previous>=TimeSpan.FromMinutes(10))plan.RecheckAfterConnection52=true;
  plan.Next=now;plan.Detail=plan.RecheckAfterConnection52?"Связь восстановлена; проверяем прежнее задание и свежие данные клиента перед продолжением.":"Связь восстановлена; проверяем результат прежнего задания.";
  SaveMaintenance();
 }
}
