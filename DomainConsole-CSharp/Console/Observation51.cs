using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 readonly HashSet<string> stoppedObservations51=new(Store.Read("stopped-observations51.json",new List<string>()),StringComparer.OrdinalIgnoreCase);
 static bool ObservationExpired51(string started,DateTimeOffset now)=>!DateTimeOffset.TryParse(started,out var at)||at>now.AddMinutes(1)||now>=at.AddDays(2);
 bool LostObservation51(string host)=>clientConnections.TryGetValue(host,out var c)&&c.Error.Length>0&&(c.Lost38||c.Kind38(DateTimeOffset.UtcNow)=="Access");
 string ObservationKey51(ComputerRow row)=>Identity40.EndpointKey(ConnectionStore.Capture(row.Target))+"|"+row.JobId;
 bool ObservationStopped51(ComputerRow row)=>stoppedObservations51.Contains(ObservationKey51(row));
 bool ObservationExpired51(ComputerRow row)=>ObservationExpired51(History.FirstOrDefault(j=>j.Id==row.JobId)?.Created??row.Target.State?.Started??"",DateTimeOffset.UtcNow);
 void RefreshObservation51(ComputerRow selected){
  var operation=OperationRow38(selected);bool active=IsCurrentExecution(operation.Target.Status);
  StopObservation51.Visibility=active&&(LostObservation51(selected.Host)||ObservationStopped51(operation))?Visibility.Visible:Visibility.Collapsed;
  StopObservation51.Content=ObservationStopped51(operation)||ObservationExpired51(operation)?"Проверить результат":"Завершить ожидание";
  StopObservation51.ToolTip="Меняется только наблюдение в консоли. Клиентская операция не останавливается; защита от второго установщика сохраняется.";
 }
 async void StopObservation51_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  var selected=Current();var row=OperationRow38(selected);if(!IsCurrentExecution(row.Target.Status))return;
  var key=ObservationKey51(row);
  if(ObservationStopped51(row)||ObservationExpired51(row)){
   if(TransportBudget41.Paused){Notify("Для проверки результата включите опрос.");return;}
   stoppedObservations51.Remove(key);Store.Write("stopped-observations51.json",stoppedObservations51.ToList());
   await Poll(row,force:true); // Reads/reconciles the existing job; never creates another installer.
  }else{
   stoppedObservations51.Add(key);Store.Write("stopped-observations51.json",stoppedObservations51.ToList());
   Event31(row.Host,"observation51","stopped","Ожидание результата в консоли завершено","Warning","Исход операции неизвестен. Клиентское задание не остановлено; повторная установка не запущена.",scope:row.JobId);
  }
  RefreshOutcomes();if(CurrentHost(row.Host))ShowCurrent();
 });
}
