using System.IO;
using System.Text;
using System.Diagnostics;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease41(){
  foreach(var item in new[]{("192.0.2.4","192.0.2.4"),("::ffff:192.0.2.4","192.0.2.4"),("::ffff:c000:204","192.0.2.4"),("2001:db8::4",""),("invalid","")})if(ClientReadiness41.IPv4(item.Item1)!=item.Item2)throw new Exception("IPv4 display normalization failed");
  var now=DateTimeOffset.UtcNow;var report=new WsusReport{NeededCount=0,RequiredComplete=true,LastReportedUtc=now.AddMinutes(-1).ToString("o"),LastSyncUtc=now.AddMinutes(-2).ToString("o"),LastSyncResult="Succeeded",TaskEndedUtc=now.ToString("o")};var proof=new RebootView40(false,true,now.ToString("o"),Array.Empty<string>(),"");
  ClientAssessment41 Evaluate(WsusReport? r=null,RebootView40? p=null,bool active=false,bool searched=true,bool saved=false)=>ClientReadiness41.Evaluate(r??report,saved,true,p??proof,new(){Status="Completed",Ended=now.ToString("o"),ReportRequested=true},active,searched,0,now.AddMinutes(-2).ToString("o"),"",now,24);
  if(Evaluate().Kind!="Success")throw new Exception("Fresh report predating job Ended falsely warns");
  if(Evaluate(searched:false).Kind=="Success"||Evaluate(saved:true).Kind=="Success"||Evaluate(p:proof with{Fresh=false}).Kind=="Success"||Evaluate(p:proof with{Required=null}).Kind=="Success")throw new Exception("Missing checks falsely prove readiness");
  report.FailedCount=1;if(Evaluate().Kind!="Error"||Evaluate(active:true).Kind!="Working")throw new Exception("Error/active operation priority failed");report.FailedCount=0;
  report.NeededCount=2;if(Evaluate().Kind!="Needed")throw new Exception("Needed updates hidden");report.NeededCount=0;
  if(Evaluate(p:proof with{Required=true,Sources=new[]{"RebootRequired"}}).Kind!="Reboot")throw new Exception("Fresh reboot proof missing");
  if(ReportPresentation35.AgeAt41(report.LastReportedUtc,now)==ReportPresentation35.AgeAt41(report.LastReportedUtc,now.AddMinutes(6)))throw new Exception("Relative report age froze");
  report.LastReportedUtc=now.AddMinutes(10).ToString("o");if(Evaluate().Kind=="Success")throw new Exception("Future report is fresh");report.LastReportedUtc=now.AddMinutes(-1).ToString("o");
  var watch=Stopwatch.StartNew();for(int n=0;n<3200;n++)_ = Evaluate();if(watch.Elapsed.TotalSeconds>4)throw new Exception("Status matrix blocks UI");
  var row=Current();var old=reportCache.GetValueOrDefault(row.Host);var oldProof=rebootSnapshots.GetValueOrDefault(row.Host);var job=new RemoteJob{OutputChannel="WSUS",Steps=new(){new(){Kind="UpdateScan"}},Targets=new(){new(){Host=row.Host,Name=row.Name,Status="Completed",State=new(){Status="Completed",Started=now.AddMinutes(-3).ToString("o"),Ended=now.ToString("o"),UpdatesRemaining=0,ReportRequested=true,Results=new(){new(){Kind="UpdateScan",ExitCode=0,Ended=now.AddMinutes(-2).ToString("o")}}}}}};bool live=liveReports.Contains(row.Host);var tab=MainTabs.SelectedItem;
  try{History.Insert(0,job);liveReports.Add(row.Host);reportCache[row.Host]=new(){Found=true,Server=OverviewServer,ReceivedUtc=now.ToString("o"),Report=report};rebootSnapshots[row.Host]=new(){Required=false,CollectedUtc=now.ToString("o"),Address40="::ffff:192.0.2.4",AddressKey40=Identity40.EndpointKey(row.Target)};historyViewId="";MainTabs.SelectedItem=WsusTab;ShowCurrent();
   if(OverviewStatus(row).Kind!="Success"||OperationGlyph38.Kind!="Success"||!WsusOutput.Text.Contains("Проверено · необходимых обновлений нет")||RebootCard40.Visibility!=Visibility.Collapsed||row.Address40!="192.0.2.4")throw new Exception("Unified readiness adapters disagree");
   CaptureContent38("release41-ready",1480,940,true);rebootSnapshots[row.Host].Required=true;rebootSnapshots[row.Host].Sources=new(){"RebootRequired"};UpdateWsusStage();if(OperationGlyph38.Kind!="Reboot"||RebootCard40.Visibility!=Visibility.Collapsed||OverviewStatus(row).Kind!="Reboot")throw new Exception("Reboot is not the single main result");CaptureContent38("release41-reboot",1000,640,true);
  }finally{History.Remove(job);if(old==null)reportCache.Remove(row.Host);else reportCache[row.Host]=old;if(oldProof==null)rebootSnapshots.Remove(row.Host);else rebootSnapshots[row.Host]=oldProof;if(!live)liveReports.Remove(row.Host);MainTabs.SelectedItem=tab;ShowCurrent();}
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe-wrapper41.ps1"),RemoteService.SnapshotWrapper41(false),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe-slow41.ps1"),RemoteService.SnapshotWrapperTemplate41.Replace("@@SCRIPT@@",PowerShellBridge.Literal("Start-Sleep -Seconds 90")).Replace("@@KIND@@","Activity"),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe-clean-preview41.ps1"),RemoteService.LegacyProbeInventory41.Replace("@@CLEAN@@","$false"),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe-clean-apply41.ps1"),RemoteService.LegacyProbeInventory41.Replace("@@CLEAN@@","$true"),new UTF8Encoding(true));
 }
}
