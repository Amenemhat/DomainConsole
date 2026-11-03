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
  var row=Computers[0];var snapshot=rebootSnapshots[row.Host];var saved=readyProofs43[row.Host];var report=reportCache[row.Host];
  RefreshClientReport35(row);
  if(HeaderReport35.Text.Contains("нет данных")||row.Address40!="192.0.2.41")throw new Exception("Saved report/address disagrees with restored readiness");
  MainTabs.SelectedItem=WsusTab;ShowCurrent();UpdateLayout();CaptureContent38("restore55-success",1480,940);
  installerObservations53[row.Host]=new(row.Host.ToUpperInvariant()+"||",DateTimeOffset.Parse(saved.Reboot).AddSeconds(1).ToString("o"));
  if(!InstallerChanged53(row,saved.Reboot))throw new Exception("Legacy installer evidence was ignored");
  installerObservations53.Remove(row.Host);
  string oldKey=saved.Key,oldEndpoint=report.Endpoint41;
  var target=History[0].Targets[0];var oldStatus=target.Status;
  void Expect(string kind,string message){RefreshOutcomes();if(row.Outcome!=kind)throw new Exception(message+": "+row.Outcome+" / "+row.OutcomeHint);}
  try{
   ConnectionStore.Profiles[row.Host]=new(){Generation54=1};
   Expect("Unknown","Legacy evidence crossed a reconnect generation");
   report.Endpoint41=Identity40.EndpointKey(row.Target);
   saved.Key=report.Endpoint41+"|"+OverviewServer+"|ssl=False|"+row.Target.DirectoryId;
   Expect("Success","0.54 evidence was not restored");
   ConnectionStore.Profiles[row.Host].Generation54=2;
   Expect("Unknown","Current evidence crossed a reconnect generation");
   ConnectionStore.Profiles.Remove(row.Host);report.Endpoint41=oldEndpoint;saved.Key=oldKey;
   target.Status="Failed";Expect("Error","A real failure was hidden by saved success");target.Status=oldStatus;
   snapshot.Required=true;snapshot.Sources=new(){"RebootRequired"};Expect("Reboot","A new reboot requirement was hidden");snapshot.Required=false;snapshot.Sources.Clear();
   Expect("Success","Legacy proof did not survive the restart fixture");
  }finally{ConnectionStore.Profiles.Remove(row.Host);report.Endpoint41=oldEndpoint;saved.Key=oldKey;target.Status=oldStatus;snapshot.Required=false;snapshot.Sources.Clear();}
  if(queue.Count!=0)throw new Exception("Restoration enqueued remote work");
  File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"restore-ready55.txt"),"PASS: actual startup, saved WSUS report, mixed-case host, completed job, persisted proof and reboot snapshot; no network or submitted work.");
  Application.Current.Shutdown(0);
 }
}
