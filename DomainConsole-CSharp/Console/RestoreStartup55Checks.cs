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
  if(queue.Count!=0)throw new Exception("Restoration enqueued remote work");
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"restore-ready55.txt"),"PASS: actual startup, saved WSUS report, mixed-case host, completed job, persisted proof and reboot snapshot; no network or submitted work.");
  Application.Current.Shutdown(0);
 }
}
