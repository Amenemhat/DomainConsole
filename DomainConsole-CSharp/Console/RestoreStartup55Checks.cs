using System.IO;
using System.Text.Json;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRestoreStartup55(){
  PollingControl43.Pause();timer.Stop();rebootTimer.Stop();
  LoadRows(Store.Read("computers.json",new List<TargetRecord>()));
  var facts=Computers.Select(row=>new{row.Host,row.Outcome,row.OutcomeHint,
   Report=reportCache.TryGetValue(row.Host,out var cache)?CacheMatches(cache):false,
   Proof=readyProofs43.TryGetValue(row.Host,out var proof)?proof.Key:"",
   ExpectedKey=Identity40.EndpointKey(ConnectionStore.Capture(row.Target))+"|"+OverviewServer+"|ssl="+(WsusSsl.IsChecked==true)+"|"+row.Target.DirectoryId,
   Job=HostJobs39(row.Host).FirstOrDefault(x=>IsWsusJob(x.Job)&&CurrentIdentity(x.Target)).Job?.Id,
   Boot=rebootSnapshots.GetValueOrDefault(row.Host)?.BootUtc}).ToList();
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"restore-status55.json"),JsonSerializer.Serialize(facts,Store.Options));
  if(Computers.Count!=1||Computers[0].Outcome!="Success")throw new Exception("Saved startup status was not restored: "+JsonSerializer.Serialize(facts));
  var row=Computers[0];var snapshot=rebootSnapshots[row.Host];var saved=readyProofs43[row.Host];
  // A failed read after a completed post-boot check must not revive an older reboot watch.
  var request=DateTimeOffset.Parse(snapshot.BootUtc).AddMinutes(-2).ToString("o");
  rebootWatches[row.Host]=new(){RequestedUtc=request,BeforeBootUtc=DateTimeOffset.Parse(snapshot.BootUtc).AddDays(-1).ToString("o"),ReadyAfter=DateTimeOffset.Parse(snapshot.BootUtc).AddMinutes(1),Confirmed=true,Scan=true};
  snapshot.Required=null;snapshot.Error="Fixture: transient read unavailable";
  RefreshOutcomes();
  if(row.Outcome!="Success"||!rebootWatches[row.Host].Done)throw new Exception("An obsolete reboot watch replaced a later durable successful result: "+row.Outcome+" / "+row.OutcomeHint);
  if(queue.Count!=0)throw new Exception("Restoration enqueued remote work");
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"restore-ready55.txt"),"PASS: actual startup, saved WSUS report, mixed-case host, completed job, persisted proof and reboot snapshot; no network or submitted work.");
  Application.Current.Shutdown(0);
 }
}
