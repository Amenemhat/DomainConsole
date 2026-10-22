using System.IO;
using System.Text;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease25(){
  var row=Computers.Last();var savedCache=reportCache.GetValueOrDefault(row.Host);var savedConnection=clientConnections.GetValueOrDefault(row.Host);bool wasLive=liveReports.Contains(row.Host);var originalSelected=Computers.Select(r=>r.Selected).ToList();var originalRecentAnchor=recentAnchor;var originalTreeAnchor=treeAnchor;RemoteJob? evidenceJob=null;
  try {
   clientConnections.Remove(row.Host);overviewErrors.Remove(row.Host);
   var report=new WsusReport{NeededCount=0,RequiredComplete=true,FullDomainName=row.Host,LastReportedUtc=DateTimeOffset.UtcNow.ToString("o"),LastSyncResult="Succeeded"};reportCache[row.Host]=new(){Report=report,Server=OverviewServer,ReceivedUtc=DateTimeOffset.UtcNow.ToString("o")};liveReports.Remove(row.Host);
   if(OverviewStatus(row).Kind!="Stale")throw new Exception("Saved report must be marked stale until refreshed");liveReports.Add(row.Host);
   if(OverviewStatus(row).Kind!="Success")throw new Exception("Fresh success is not green");
   report.LastSyncResult="Unknown";if(report.Severity("")!="Unknown")throw new Exception("Unknown sync result falsely marked failed");report.LastSyncResult="Succeeded";
   report.LastReportedUtc=DateTimeOffset.UtcNow.AddDays(-2).ToString("o");if(OverviewStatus(row).Kind!="Stale")throw new Exception("Old success incorrectly requests intervention");
   report.FailedCount=1;liveReports.Remove(row.Host);if(OverviewStatus(row).Kind!="Error")throw new Exception("Old known failure hidden by freshness");report.FailedCount=0;report.InstalledPendingRebootCount=1;if(OverviewStatus(row).Kind!="Reboot")throw new Exception("Pending reboot icon missing");report.InstalledPendingRebootCount=0;
   overviewErrors[row.Host]="Server timeout";if(OverviewStatus(row).Kind!="Stale"||!OverviewStatus(row).Hint.Contains("Server timeout"))throw new Exception("Failed WSUS read replaced saved health");overviewErrors.Remove(row.Host);
   reportCache[row.Host].Found=false;if(OverviewStatus(row).Kind!="NoWsus")throw new Exception("Missing WSUS identity not distinguished");
   clientConnections[row.Host]=new(){Error="Access is denied",LastAttempt=DateTimeOffset.UtcNow};if(LinkKind(row.Host)!="Access"||!StatusTags(row).Contains("NoWsus"))throw new Exception("Access denied confused with offline or server absence");
   clientConnections[row.Host].Error="WinRM timeout";if(LinkKind(row.Host)!="Unreachable")throw new Exception("Unreachable has no dedicated state");clientConnections.Remove(row.Host);
   reportCache[row.Host].Found=true;
   evidenceJob=new(){OutputChannel="WSUS",Targets=new(){new(){Host=row.Host,Name=row.Name,Status="Failed",State=new(){Status="Failed",Ended=DateTimeOffset.UtcNow.ToString("o")}}}};History.Insert(0,evidenceJob);
   if(OverviewStatus(row).Kind!="Error")throw new Exception("Fresh query of old server report hides newer task failure");History.Remove(evidenceJob);
   reportCache[row.Host].Found=false;evidenceJob=new(){Name="Перезагрузка текущего компьютера",IndependentControl=true,OutputChannel="Commands",Targets=new(){new(){Host=row.Host,Name=row.Name,Status="Queued"}}};History.Insert(0,evidenceJob);
   if(OverviewStatus(row).Kind!="Reboot"||!StatusTags(row).Contains("Reboot"))throw new Exception("Scheduled reboot missing outside WSUS jobs");History.Remove(evidenceJob);evidenceJob=null;
   if(AccessError("Сервер недоступен"))throw new Exception("Network failure mistaken for access denied");
   var rebootCode=HeaderRebootCode(60,"Сохраните 'документы'",false);if(!rebootCode.Contains("AddSeconds(60)")||!rebootCode.Contains("reboot-cancel.flag")||!rebootCode.Contains("shutdown.exe /r /t 0")||rebootCode.Contains("/f ")||!rebootCode.Contains("''документы''"))throw new Exception("Reboot forces apps or loses quoted message");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"header-reboot.ps1"),rebootCode.Replace("{{REBOOT_JOB}}","00000000-0000-0000-0000-000000000025"),new UTF8Encoding(true));
   var visible=Computers.Take(4).ToList();foreach(var item in visible)item.Selected=false;
   MarkRange(visible[0],visible,true,false,false);MarkRange(visible[2],visible,true,true,false);if(!visible.Take(3).All(r=>r.Selected)||visible[3].Selected)throw new Exception("Shift checkbox range invalid");
   MarkRange(visible[1],visible,false,false,false);if(!visible[0].Selected||visible[1].Selected||!visible[2].Selected)throw new Exception("Ctrl toggle cleared other targets");
   MarkRange(visible[3],new(){visible[0],visible[3]},false,true,false);if(!visible[0].Selected||!visible[2].Selected)throw new Exception("Hidden anchor changed hidden range");
   statusFilter.Add("NoWsus");statusFilter.Add("Stale");RefreshStatusCounts();var union=ScopedStatusRows();if(!union.Contains(row)||union.DistinctBy(r=>r.Host,StringComparer.OrdinalIgnoreCase).Count()!=union.Count)throw new Exception("Status union loses clients or duplicates them");
   if(!StatusFilterButton.Content.ToString()!.Contains("ПК:"))throw new Exception("Filter count missing");statusFilter.Clear();RefreshStatusCounts();
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"wsus-overview.ps1"),RemoteService.OverviewScript(new(){Host="wsus.invalid",Port=8530},new(){"alpha.invalid","missing.invalid","alpha.other.invalid","broken.invalid","duplicate.invalid"}),new UTF8Encoding(true));
  }finally{if(evidenceJob!=null)History.Remove(evidenceJob);if(savedCache==null)reportCache.Remove(row.Host);else reportCache[row.Host]=savedCache;if(savedConnection==null)clientConnections.Remove(row.Host);else clientConnections[row.Host]=savedConnection;if(wasLive)liveReports.Add(row.Host);else liveReports.Remove(row.Host);overviewErrors.Remove(row.Host);statusFilter.Clear();for(int i=0;i<Computers.Count;i++)Computers[i].Selected=originalSelected[i];recentAnchor=originalRecentAnchor;treeAnchor=originalTreeAnchor;RefreshOutcomes();UpdateSelectionCount();}
 }
}

