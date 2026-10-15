using System.IO;
using System.Text;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease251(){
  var row=Computers.Last();var cache=reportCache.GetValueOrDefault(row.Host);var reboot=rebootSnapshots.GetValueOrDefault(row.Host);var scope=statusScope;var connection=clientConnections.GetValueOrDefault(row.Host);var live=liveReports.Contains(row.Host);
  try {
   clientConnections.Remove(row.Host);var now=DateTimeOffset.UtcNow;
   reportCache[row.Host]=new(){Report=new(){LastSyncResult="Succeeded",LastReportedUtc=now.AddMinutes(-10).ToString("o"),InstalledPendingRebootCount=1},Server=OverviewServer,ReceivedUtc=now.ToString("o")};
   rebootSnapshots[row.Host]=new(){CollectedUtc=now.ToString("o"),BootUtc=now.AddMinutes(-1).ToString("o"),Required=false};
   if(RebootReason(row).Length>0||OverviewStatus(row).Kind=="Reboot"||StatusTags(row).Contains("Reboot"))throw new Exception("Old server snapshot overrides fresh post-reboot client observation");
   if(reportCache[row.Host].Report.InstalledPendingRebootCount!=1)throw new Exception("Client observation falsified server report");
   rebootSnapshots[row.Host].Required=true;if(RebootReason(row).Length==0)throw new Exception("Client still needs reboot but indicator is hidden");
   reportCache[row.Host].Report.LastReportedUtc=now.AddSeconds(1).ToString("o");reportCache[row.Host].Report.InstalledPendingRebootCount=0;if(RebootReason(row).Length>0)throw new Exception("Old reboot observation overrides a newer server report");
   var watch=new RebootWatch{RequestedUtc=now.AddMinutes(-2).ToString("o")};if(!RebootReturned(watch,rebootSnapshots[row.Host]))throw new Exception("New boot not recognized");watch.RequestedUtc=now.ToString("o");if(RebootReturned(watch,rebootSnapshots[row.Host]))throw new Exception("Old boot treated as completed restart");watch.RequestedUtc=now.AddMinutes(-2).ToString("o");rebootSnapshots[row.Host].Required=null;if(RebootReturned(watch,rebootSnapshots[row.Host]))throw new Exception("Unknown reboot flag reported confirmed");
   statusScope=Computers[0].OU;if(MatchesScope(row)||!MatchesScope(Computers[0]))throw new Exception("OU scope did not restrict unrelated computers");statusScope="OU=Office,DC=test,DC=invalid";if(!MatchesScope(Computers[0]))throw new Exception("OU scope excludes descendants");
   var ordered=OrderForOverview(Computers,new[]{row.Host},new[]{Computers[0].Host});if(ordered[0]!=row||ordered[1]!=Computers[0])throw new Exception("Pinned work targets do not precede recent targets");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"reboot-state.ps1"),RemoteService.RebootStateScript,new UTF8Encoding(true));
  }finally{if(cache==null)reportCache.Remove(row.Host);else reportCache[row.Host]=cache;if(reboot==null)rebootSnapshots.Remove(row.Host);else rebootSnapshots[row.Host]=reboot;if(connection==null)clientConnections.Remove(row.Host);else clientConnections[row.Host]=connection;statusScope=scope;if(live)liveReports.Add(row.Host);else liveReports.Remove(row.Host);RefreshOutcomes();}
 }
}
