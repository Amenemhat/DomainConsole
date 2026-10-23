using System.IO;
using System.Text;
using System.Diagnostics;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 static async Task VerifyTransportBudget41(){
  var target=new TargetRecord{Host="probe-admission41.fixture.invalid"};string code="# DC-ENDPOINT41:"+TransportBudget41.Key(target)+"\nfixture";var release=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);bool paused=TransportBudget41.Paused;TransportBudget41.Paused=false;TransportBudget41.Reset(target);int calls=0;
  try{var first=TransportBudget41.Run(code,CancellationToken.None,t=>{Interlocked.Increment(ref calls);return release.Task;});bool rejected=false;try{await TransportBudget41.Run(code,CancellationToken.None,t=>{Interlocked.Increment(ref calls);return Task.FromResult("extra");});}catch(IOException){rejected=true;}if(!rejected||calls!=1)throw new Exception("Admission spawned duplicate endpoint work");release.SetResult("done");await first;
   bool failed=false;try{await TransportBudget41.Run(code,CancellationToken.None,t=>Task.FromException<string>(new TimeoutException("fixture")));}catch(TimeoutException){failed=true;}if(!failed)throw new Exception("Timeout not propagated");rejected=false;try{await TransportBudget41.Run(code,CancellationToken.None,t=>{Interlocked.Increment(ref calls);return Task.FromResult("extra");});}catch(IOException){rejected=true;}if(!rejected||calls!=1)throw new Exception("Unknown outcome automatically retried");TransportBudget41.Reset(target);if(await TransportBudget41.Run(code,CancellationToken.None,t=>Task.FromResult("resumed"))!="resumed")throw new Exception("Explicit resume failed");
  }finally{release.TrySetResult("done");TransportBudget41.Reset(target);TransportBudget41.Paused=paused;}
 }

 void VerifyRelease41(){
  AwaitUi39(VerifyTransportBudget41());
  var readError=new ClientEvent31{Operation36="read-фоновая активность",Kind="Warning",Details="Этап WinRM/аутентификации: RAW_READ_FAILURE"};if(readError.Body38!="Ошибка WinRM/аутентификации")throw new Exception("Read event leaks full WinRM error");
  foreach(var item in new[]{("192.0.2.4","192.0.2.4"),("::ffff:192.0.2.4","192.0.2.4"),("::ffff:c000:204","192.0.2.4"),("2001:db8::4",""),("invalid","")})if(ClientReadiness41.IPv4(item.Item1)!=item.Item2)throw new Exception("IPv4 display normalization failed");
  var now=DateTimeOffset.UtcNow;var report=new WsusReport{NeededCount=0,RequiredComplete=true,LastReportedUtc=now.AddMinutes(-1).ToString("o"),LastSyncUtc=now.AddMinutes(-2).ToString("o"),LastSyncResult="Succeeded",TaskEndedUtc=now.ToString("o")};var proof=new RebootView40(false,true,now.ToString("o"),Array.Empty<string>(),"");
  ClientAssessment41 Evaluate(WsusReport? r=null,RebootView40? p=null,bool active=false,bool searched=true,bool saved=false)=>ClientReadiness41.Evaluate(r??report,saved,true,p??proof,new(){Status="Completed",Ended=now.ToString("o"),ReportRequested=true},active,searched,0,now.AddMinutes(-2).ToString("o"),"",now,24);
  if(LogViewer.LineTone(Evaluate().Title)!="Success"||LogViewer.LineTone("Требуется перезагрузка")!="Warning")throw new Exception("Primary journal tones disagree with readiness card");
  if(Evaluate().Kind!="Success")throw new Exception("Fresh report predating job Ended falsely warns");
  if(Evaluate(searched:false).Kind=="Success"||Evaluate(saved:true).Kind=="Success"||Evaluate(p:proof with{Fresh=false}).Kind=="Success"||Evaluate(p:proof with{Required=null}).Kind=="Success")throw new Exception("Missing checks falsely prove readiness");
  report.FailedCount=1;if(Evaluate().Kind!="Error"||Evaluate(active:true).Kind!="Working")throw new Exception("Error/active operation priority failed");report.FailedCount=0;
  report.NeededCount=2;if(Evaluate().Kind!="Needed")throw new Exception("Needed updates hidden");report.NeededCount=0;
  if(Evaluate(p:proof with{Required=true,Sources=new[]{"RebootRequired"}}).Kind!="Reboot")throw new Exception("Fresh reboot proof missing");
  if(ReportPresentation35.AgeAt41(report.LastReportedUtc,now)==ReportPresentation35.AgeAt41(report.LastReportedUtc,now.AddMinutes(6)))throw new Exception("Relative report age froze");
  report.LastReportedUtc=now.AddMinutes(10).ToString("o");if(Evaluate().Kind=="Success")throw new Exception("Future report is fresh");report.LastReportedUtc=now.AddMinutes(-1).ToString("o");
  var watch=Stopwatch.StartNew();for(int n=0;n<3200;n++)_ = Evaluate();if(watch.Elapsed.TotalSeconds>4)throw new Exception("Status matrix blocks UI");
  var row=Current();var old=reportCache.GetValueOrDefault(row.Host);var oldProof=rebootSnapshots.GetValueOrDefault(row.Host);var job=new RemoteJob{OutputChannel="WSUS",Steps=new(){new(){Kind="UpdateScan"}},Targets=new(){new(){Host=row.Host,Name=row.Name,Status="Completed",State=new(){Status="Completed",Started=now.AddMinutes(-3).ToString("o"),Ended=now.ToString("o"),UpdatesRemaining=0,ReportRequested=true,Results=new(){new(){Kind="UpdateScan",ExitCode=0,Ended=now.AddMinutes(-2).ToString("o")}}}}}};bool live=liveReports.Contains(row.Host);var tab=MainTabs.SelectedItem;bool ticking=timer.IsEnabled;timer.Stop();
  try{History.Insert(0,job);liveReports.Add(row.Host);reportCache[row.Host]=new(){Found=true,Server=OverviewServer,ReceivedUtc=now.ToString("o"),Report=report};rebootSnapshots[row.Host]=new(){Required=false,CollectedUtc=now.ToString("o"),Address40="::ffff:192.0.2.4",AddressKey40=Identity40.EndpointKey(row.Target)};historyViewId="";MainTabs.SelectedItem=WsusTab;RefreshOutcomes();ShowCurrent();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();
   if(OverviewStatus(row).Kind!="Success"||OperationGlyph38.Kind!="Success"||!WsusOutput.VisibleText().Contains("Проверено · необходимых обновлений нет")||row.Outcome!="Success"||RebootCard40.Visibility!=Visibility.Collapsed||row.Address40!="192.0.2.4")throw new Exception("Unified readiness adapters disagree");
   CaptureContent38("release41-ready",1480,940,true);rebootSnapshots[row.Host].Required=true;rebootSnapshots[row.Host].Sources=new(){"RebootRequired"};RefreshOutcomes();ShowCurrent();UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);UpdateLayout();if(OperationGlyph38.Kind!="Reboot"||RebootCard40.Visibility!=Visibility.Collapsed||OverviewStatus(row).Kind!="Reboot")throw new Exception("Reboot is not the single main result");CaptureContent38("release41-reboot",1000,640,true);
  }finally{History.Remove(job);if(old==null)reportCache.Remove(row.Host);else reportCache[row.Host]=old;if(oldProof==null)rebootSnapshots.Remove(row.Host);else rebootSnapshots[row.Host]=oldProof;if(!live)liveReports.Remove(row.Host);MainTabs.SelectedItem=tab;ShowCurrent();if(ticking)timer.Start();}
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe-wrapper41.ps1"),RemoteService.SnapshotWrapper41(false),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe-slow41.ps1"),RemoteService.SnapshotWrapperTemplate41.Replace("@@SCRIPT@@",PowerShellBridge.Literal("Start-Sleep -Seconds 90")).Replace("@@KIND@@","Activity"),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe-clean-preview41.ps1"),RemoteService.LegacyProbeInventory41.Replace("@@CLEAN@@","$false"),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe-clean-apply41.ps1"),RemoteService.LegacyProbeInventory41.Replace("@@CLEAN@@","$true"),new UTF8Encoding(true));
 }
}
