using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class AdmissionReply38 {public bool Allowed {get;set;}public string ReasonCode {get;set;}="";public string Message {get;set;}="";public string ActiveJobId {get;set;}="";public string PlanId {get;set;}="";}
public partial class MainWindow {
 ComputerRow OperationRow38(ComputerRow selected){
  var active=History.FirstOrDefault(j=>j.Targets.Any(t=>t.Host.Equals(selected.Host,StringComparison.OrdinalIgnoreCase)&&IsCurrentExecution(t.Status)));
  if(active!=null)return new(active.Targets.First(t=>t.Host.Equals(selected.Host,StringComparison.OrdinalIgnoreCase)),active.Id);
  return ExecutionRow(selected,MainTabs.SelectedItem==WsusTab);
 }
 void BeginAcceptedPlan38(RemoteJob job,TargetRecord target){maintenancePlans[target.Host]=new(){PlanId37=job.PlanId37,Host=target.Host,Identity=target.ConnectionIdentity,JobId=job.Id,Install=true,Phase="Выполняется полное обслуживание",Cycles=1};SaveMaintenance();}
 public static AdmissionReply38 ParseAdmission38(string text){
  var match=Regex.Match(text,@"DC_ADMISSION38\|([A-Za-z0-9+/=]+)");if(match.Success){try{return JsonSerializer.Deserialize<AdmissionReply38>(Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value)),Store.Options)??new();}catch{}}
  return new(){ReasonCode="unknown",Message="Новый запуск не выполнен: допуск не подтверждён. Текущее задание продолжает наблюдаться."};
 }
 void RejectAdmission38(RemoteJob job,TargetRecord target,Exception error){var reply=ParseAdmission38(error.Message);target.Status="LaunchRejected";target.State=new(){Id=job.Id,Status="LaunchRejected",Ended=DateTime.UtcNow.ToString("o")};target.Output="## Запуск не выполнен\n"+reply.Message+"\nВыполнявшееся задание не отменено. Его статус отображается отдельно.";Event31(target.Host,"admission",job.Id,reply.Message,"Warning",reply.ActiveJobId.Length>0?"Действующее задание: "+reply.ActiveJobId:"");Notify(reply.Message);}
 void StopCurrent38_Click(object sender,RoutedEventArgs e){if(ComputerGrid.SelectedItem is not ComputerRow row)return;var operation=OperationRow38(row);if(operation.JobId.Length==0)return;var job=History.FirstOrDefault(j=>j.Id==operation.JobId);if(job==null)return;historyViewId=job.Id;MainTabs.SelectedItem=IsWsusJob(job)?WsusTab:CommandsTab;Cancel_Click(sender,e);}
}
