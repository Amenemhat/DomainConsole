using System.IO;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyConnectionCancel54(){
  var target=ConnectionStore.Capture(new(){Host="cancel54.fixture.invalid"});var started=new TaskCompletionSource();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
  var pending=TransportBudget41.Run("# DC-MANUAL41\n# DC-ENDPOINT41:"+TransportBudget41.Key(target),timeout.Token,async token=>{started.TrySetResult();await Task.Delay(Timeout.Infinite,token);return "unexpected";});
  await started.Task.WaitAsync(timeout.Token);TransportBudget41.CancelEndpoint54(target);
  try{await pending;throw new Exception("Disconnect did not cancel local transport wait");}catch(OperationCanceledException){}
 }
 void VerifyRelease54(){
  AwaitUi39(VerifyConnectionCancel54());
  var now=DateTimeOffset.UtcNow;var host="release54.fixture.invalid";var original=ConnectionStore.Profiles.GetValueOrDefault(host);
  try{
   ConnectionStore.Profiles[host]=new(){Address="192.0.2.10",Identity="identity54",Generation54=1};
   var frozen=ConnectionStore.Capture(new(){Host=host});
   ConnectionStore.Profiles[host]=new(){Address="192.0.2.11",Identity="identity54",Generation54=2};
   if(CurrentEndpoint(frozen)||ConnectionStore.Address(frozen)!="192.0.2.10")throw new Exception("Late endpoint reply accepted or historical endpoint mutated");
   if(TransportBudget41.Key(frozen)==TransportBudget41.Key(new(){Host=host}))throw new Exception("Reconnect reused prior transport lock");
   try{ConnectionStore.EnsureCurrent54(frozen);throw new Exception("Old endpoint could dispatch after reconnect");}catch(InvalidOperationException){}
   var current=ConnectionStore.Capture(new(){Host=host});if(!CurrentEndpoint(current))throw new Exception("Verified endpoint not accepted");
   ConnectionStore.Profiles[host].Disconnected54=true;
   if(CurrentEndpoint(current))throw new Exception("Disconnected endpoint remains active");
   try{ConnectionStore.EnsureConnected54(host);throw new Exception("Disconnect did not block transport");}catch(InvalidOperationException){}
  }finally{if(original==null)ConnectionStore.Profiles.Remove(host);else ConnectionStore.Profiles[host]=original;}
  var target=new TargetRecord{Host=host};var job=new RemoteJob{Targets=new(){target}};
  var poll=new PollResult{State=new(){Status="Running"},Updates=new(){new(){Id="package-a",Revision=1,Title="KB fixture A",Started54=now.ToString("o"),Ended54=now.ToString("o"),ResultCode=2,HResult=0}}};
  MergeUpdateHistory54(job,host,poll);MergeUpdateHistory54(job,host,poll);
  job=Store.Clone(job);var partial=new PollResult{State=new(){Status="Running"}};MergeUpdateHistory54(job,host,partial);
  if(job.Targets[0].UpdateHistory54.Count!=1||partial.Updates.Count!=1||partial.Updates[0].ResultCode!=2)throw new Exception("Per-package success lost or duplicated after restart/partial reply");
  var older=Store.Clone(partial.Updates[0]);older.ResultCode=null;older.Download=2;partial.Updates=new(){older};MergeUpdateHistory54(job,host,partial);if(partial.Updates[0].ResultCode!=2)throw new Exception("Delayed download overwrote retained installation result");
  var intent54=new UpdateAttempt54{Id="update54",Revision=3,Started54=now.AddMinutes(-1).ToString("o")};string job54=Guid.NewGuid().ToString();
  var entry54=new ClientUpdateHistory{Id="UPDATE54",Revision=3,Date=now.ToString("o"),Initiator54="DomainConsole CSharp / "+job54,Operation=1,ResultCode=2};
  if(!MatchesInstallHistory54(entry54,intent54,job54,now))throw new Exception("Exact attributable WUA result rejected");
  entry54.Initiator54="Windows Update";if(MatchesInstallHistory54(entry54,intent54,job54,now))throw new Exception("External installation attributed to console");
  entry54.Initiator54="DomainConsole CSharp / "+job54;entry54.Revision=4;if(MatchesInstallHistory54(entry54,intent54,job54,now))throw new Exception("Different revision attributed to job");
  var report=new WsusReport{NeededCount=1,RequiredComplete=true,LastReportedUtc=now.ToString("o"),LastSyncResult="Succeeded"};
  var reboot=DescribeReboot40(new(){Required=false,CollectedUtc=now.ToString("o"),BootUtc=now.AddDays(-1).ToString("o")},now.AddDays(-1).ToString("o"),now);
  var assessment=ClientReadiness41.Evaluate(report,false,true,reboot,new(){Status="Completed"},false,true,0,now.ToString("o"),"",now,24);
  if(assessment.Kind!="Discrepancy")throw new Exception("Server/client discrepancy hidden");
  report.LastReportedUtc=now.AddMinutes(-5).ToString("o");
  var waitingAssessment=ClientReadiness41.Evaluate(report,false,true,reboot,new(){Status="Completed"},false,true,0,now.ToString("o"),"",now,24);
  if(waitingAssessment.Kind!="ReportPending"||waitingAssessment.Tone!="Info")throw new Exception("Older server count presented as current installation requirement");
  report.LastReportedUtc=now.ToString("o");
  var nonzero=ClientReadiness41.Evaluate(report,false,true,reboot,new(){Status="Completed"},false,true,2,now.ToString("o"),"",now,24);
  if(nonzero.Kind!="Discrepancy")throw new Exception("Two nonzero disagreeing counts hidden");
  var nonzeroCriteria=Criteria45.Build(nonzero,report,reboot,new(){Status="Completed"},false,now.ToString("o"),2,false,"","");
  if(nonzeroCriteria[0].Checks[0].Tone!="Success")throw new Exception("Successful search with updates shown as failed search");
  var formatted=ResultPresentation36.Format("## Итог задания\nСостояние: Завершено\n## Результаты команд\nfixture: успешно · 0\n## Проверка перезагрузки клиента\nФлаги перезагрузки Windows Update / CBS отсутствуют.\n## Вывод команды\nTcpTestSucceeded : False");
  if(formatted.Contains("## Проверка перезагрузки")||formatted.Contains("## Технические подробности · результаты команд")||!formatted.Contains("TcpTestSucceeded : False")||!formatted.Contains("Результаты шагов"))throw new Exception("Duplicate sections or missing output/codes");
  var diagnostic=ResultPresentation36.Format(PresentResult(host,new(){State=new(){Status="Completed",Results=new(){new(){Kind="ClientDiagnostics",Name="diagnostic",ExitCode=0}}},Client=new(){TcpConnected54=false},Files=new(){new(){Name="step-1.out",Body="TcpTestSucceeded : False"}}}));
  if(!diagnostic.StartsWith("## Главное\nTCP-подключение")||!diagnostic.Contains("TcpTestSucceeded : False"))throw new Exception("Negative TCP result hidden behind successful command exit");
  var delayed=NewReportWait54("fixture54",now.AddMinutes(-8).ToString("o"));if(delayed.Until!=now.AddMinutes(2))throw new Exception("Report deadline restarted on delayed polling");
  var queue=new ClientReportProbe{QueueError="access denied",CollectedUtc=now.ToString("o")};if(!QueueHint52(queue).Contains("Неизвестно"))throw new Exception("Unread queue rendered as zero");
  var oldCache=reportCache.GetValueOrDefault(host);var oldWait=reportWaiting.GetValueOrDefault(host);
  try{
   reportWaiting[host]=new(){Id="fixture54",Server54="fixture:8530",Requested54=now,Until=now.AddMinutes(10)};
   RecordReportReceipt54(host,new(){LastReportedUtc=now.AddMinutes(-1).ToString("o")},"fixture:8530");
   if(!reportWaiting.ContainsKey(host))throw new Exception("Report before request cleared wait");
   RecordReportReceipt54(host,new(){LastReportedUtc=now.ToString("o")},"other:8530");
   if(!reportWaiting.ContainsKey(host))throw new Exception("Other WSUS server cleared report wait");
   RecordReportReceipt54(host,new(){LastReportedUtc=now.ToString("o")},"fixture:8530");
   if(reportWaiting.ContainsKey(host))throw new Exception("New report did not release wait");
  }finally{if(oldCache==null)reportCache.Remove(host);else reportCache[host]=oldCache;if(oldWait==null)reportWaiting.Remove(host);else reportWaiting[host]=oldWait;Store.Write("report-waits.json",reportWaiting);}
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"applicability54.ps1"),RemoteService.ApplicabilityScript54,new System.Text.UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"cancel-executor54.ps1"),RemoteService.CancelScript,new System.Text.UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"ui-release54.txt"),"Endpoint generations/disconnect, persisted individual outcomes, discrepancy, compact output, unknown queue and report delivery boundary verified.");
 }
}
