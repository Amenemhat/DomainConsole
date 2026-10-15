using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void CompleteLocalCancellation(string host,string id,string message){var job=History.FirstOrDefault(j=>j.Id==id);var target=job?.Targets.FirstOrDefault(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase));if(job==null||target==null)return;target.Status="Cancelled";target.State=new(){Id=id,Status="Cancelled",Stage="Отмена подтверждена",Ended=DateTimeOffset.UtcNow.ToString("o"),Updated=DateTimeOffset.UtcNow.ToString("o")};target.Output="## Отмена задания\n"+message;Sync(job);Save();if((ComputerGrid.SelectedItem as ComputerRow)?.Host.Equals(host,StringComparison.OrdinalIgnoreCase)==true)ShowCurrent();}
 public static bool InactiveEvidence(PollResult data)=>!data.AgentAlive&&(data.TaskState is "Ready" or "Disabled" or "Missing");
 public static bool InterruptedByBoot(RemoteState state,PollResult data)=>InactiveEvidence(data)&&DateTimeOffset.TryParse(data.BootUtc,out var boot)&&DateTimeOffset.TryParse(state.Started,out var start)&&boot>start;
 public static void ReconcileMissing(PollResult data,RemoteJob job,DateTimeOffset now){
  if(data.State!=null||!InactiveEvidence(data)||!DateTimeOffset.TryParse(job.Created,out var created)||now-created<TimeSpan.FromMinutes(2))return;
  data.State=new(){Id=job.Id,Status="InterventionRequired",Stage="Исполнение не подтверждено",Started=job.Created,Ended=now.ToString("o"),Updated=now.ToString("o"),Error="Файл состояния отсутствует; исполнитель не найден, задача планировщика: "+data.TaskState+". Результат неизвестен. Активная очередь освобождена; повторный запуск только вручную. Отменить эту запись можно кнопкой остановки в истории."};
 }
}
