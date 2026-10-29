using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyEventHistory50(){
  var step=new StepResult{Kind="CacheCleanup",Name="Очистка кэша: Download",ExitCode=0,Ended="2026-01-01T10:00:00Z",Cache50=new(){Isolated=1,PendingDelete=1,CleanupStarted=true}};
  var result=StepEvent50(step);if(result.Title!="Кэш скачивания очищен"||result.Detail.Contains("копия")||result.Kind!="Success")throw new Exception("Cache result lost or invented backup");
  step.Cache50.CleanupStarted=false;if(StepEvent50(step).Kind!="Warning")throw new Exception("Missing physical cleanup hidden");
  step.Cache50=new(){Isolated=1,Backups=1};if(!StepEvent50(step).Detail.Contains("копия сохранена"))throw new Exception("Backup result missing");
  step.Cache50=new();if(StepEvent50(step).Title.Contains("очищен"))throw new Exception("Absent cache reported reset");
  step.ExitCode=1;step.Error="primary failure";if(StepEvent50(step).Kind!="Error")throw new Exception("Cache error hidden by stale summary");
  step.ExitCode=0;step.Cache50=new(){Isolated=1,Backups=1};
  var service=new ClientEvent31{Operation36="link",Kind="Success",Text="Связь WinRM подтверждена"};
  if(EventMatches47(service,0)||!EventMatches47(service,2)||!EventMatches47(service,3))throw new Exception("Service filtering incorrect");
  if(!EventMatches47(new(){Operation36="link-state50",Kind="Warning"},0))throw new Exception("Confirmed link loss hidden");
  var raw=new[]{new ClientEvent31{Operation36="link",Text="error",At="2026-01-01T10:00:00Z"},new ClientEvent31{Operation36="link",Text="ok",At="2026-01-01T10:01:00Z"},new ClientEvent31{Operation36="link",Text="error",At="2026-01-01T10:02:00Z"}};
  if(CompactEvents34(raw).Count!=3)throw new Exception("Separate connection episodes merged");
  if(CompactEvents34(new[]{new ClientEvent31{Operation36="maintenance|plan1",Text="wait"},new ClientEvent31{Operation36="maintenance|plan2",Text="wait"}}).Count!=2)throw new Exception("Different plans merged");
  const string host="history50.fixture.invalid";
  try{
   var data=new PollResult{State=new(){Status="Completed",Stage="WSUS · запрос отправки отчёта",Results=new(){step,new(){Kind="UpdateScan",Name="WSUS · поиск после очистки",ExitCode=0,UpdatesFound50=1,Ended="2026-01-01T10:01:00Z"}}}};
   RecordJobEvents50(host,"job1",data);RecordJobEvents50(host,"job1",data);
   var events=memoryEvents32[host];if(events.Count!=2||!events.Any(e=>e.Text=="Кэш скачивания очищен")||!events.Any(e=>e.Text=="После очистки найдено обновлений: 1"))throw new Exception("Job result repeated or replaced by final report");
   RecordJobEvents50(host,"job2",data);if(CompactEvents34(memoryEvents32[host]).Count!=4)throw new Exception("Different cleanup runs merged");
   data.State.Results[1].ExitCode=1;data.State.Results[1].Error="search failed";data.State.Status="Failed";data.State.Error="search failed";
   RecordJobEvents50(host,"job3",data);if(!memoryEvents32[host].Any(e=>e.Operation36.Contains("job3")&&e.Kind=="Error")||!memoryEvents32[host].Any(e=>e.Operation36.Contains("job3")&&e.Text=="Кэш скачивания очищен"))throw new Exception("Later search failure hid successful cleanup");
  }finally{
   if(eventWrites39.TryGetValue(host,out var writes))AwaitUi39(writes);
   var folder=EventFolder31(host);if(System.IO.Directory.Exists(folder))System.IO.Directory.Delete(folder,true);
   foreach(var key in eventSignatures31.Keys.Where(k=>k.StartsWith(host.ToUpperInvariant()+"|")).ToList())eventSignatures31.Remove(key);
   memoryEvents32.Remove(host);recentEvents31.Remove(host);pendingEvents32.Remove(host);groupedEvents39.Remove(host);
  }
 }
}
