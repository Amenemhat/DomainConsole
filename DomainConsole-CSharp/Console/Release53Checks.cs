using System.IO;
using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyRelease53(){
  var now=DateTimeOffset.UtcNow;string boot=now.AddDays(-3).ToString("o");
  var proof=new RebootSnapshot{Required=false,BootUtc=boot,CollectedUtc=now.ToString("o")};
  foreach(var elapsed in new[]{TimeSpan.FromMinutes(6),TimeSpan.FromHours(12),TimeSpan.FromDays(1)}){
   var restored=JsonSerializer.Deserialize<RebootSnapshot>(JsonSerializer.Serialize(proof,Store.Options),Store.Options)!;
   var view=DescribeReboot40(restored,boot,now+elapsed);
   if(!view.Fresh||view.Required!=false)throw new Exception("Elapsed time/restart erased complete negative flags");
  }
  if(DescribeReboot40(proof,now.AddSeconds(1).ToString("o"),now.AddMinutes(2)).Required!=null)throw new Exception("New boot reused old flags");
  proof.Error="CBS access denied";if(DescribeReboot40(proof,boot,now).Required!=null)throw new Exception("Partial negative accepted");
  proof.Error="";proof.Required=true;if(DescribeReboot40(proof,boot,now.AddDays(2)).Required!=true)throw new Exception("Positive requirement expired");
  var signal=new CollectorActivity53("Reboot","Чтение",now.AddSeconds(-10).ToString("o"),now.ToString("o"),now);
  if(!signal.Animate(now)||signal.Animate(now.AddMinutes(1))||(signal with{Stage="Stopping"}).Animate(now))throw new Exception("Collector animation misrepresents liveness");
  var row=new ComputerRow(new(){Host="status53.fixture.invalid",Name="Status fixture"});Computers.Add(row);
  var pending=queue.ToList();var paused=TransportBudget41.Paused;
  try{
   PollingControl43.Resume();var server=OverviewServer;
   var report=new WsusReport{LastReportedUtc=now.ToString("o"),LastSyncResult="Succeeded",NeededCount=0,RequiredComplete=true};
   if(!ApplyOverviewReply53(row,new(){Host=row.Host,Found=true,Report=report},server))throw new Exception("Successful report rejected");
   var saved=reportCache[row.Host];
   foreach(var reply in new OverviewReply?[]{null,new(){Found=false},new(){Found=true},new(){Error="Access denied"}}){
    if(ApplyOverviewReply53(row,reply,server)||!ReferenceEquals(saved,reportCache[row.Host])||!overviewErrors.ContainsKey(row.Host))throw new Exception("Failed lookup erased previous report or lacked reason");
   }
   if(!ApplyOverviewReply53(row,new(){Found=true,Report=report},server)||overviewErrors.ContainsKey(row.Host))throw new Exception("New success did not resolve lookup failure");
   var reboot=new RebootView40(false,true,now.ToString("o"),Array.Empty<string>(),"");
   PreserveReady43(row,new("Success","Проверено",""),false,null,saved,reboot,"scan");
   if(PreserveReady43(row,new("Working","Выполняется",""),true,null,saved,reboot,"scan").Kind!="Working"||!readyProofs43.ContainsKey(row.Host))throw new Exception("Transient work erased historical proof or was hidden");
   if(PreserveReady43(row,new("Error","Ошибка",""),false,null,saved,reboot,"scan").Kind!="Error")throw new Exception("Saved proof hides current error");
   if(PreserveReady43(row,new("Stale","Снимок",""),false,null,saved,reboot,"scan").Kind!="Success")throw new Exception("Historical success cannot be restored");
   var target=ConnectionStore.Capture(row.Target);var watch=new RebootWatch{JobId=Guid.NewGuid().ToString(),Confirmed=true,ExpectedUtc=now.AddMinutes(-5)};rebootWatches[row.Host]=watch;
   int resets=0;Task<string> Reset(TargetRecord _){resets++;return Task.FromResult("Stopped");}
   await TryResetRebootPoll51(row.Host,watch,target,Reset);await TryResetRebootPoll51(row.Host,watch,target,Reset);
   if(resets!=1||!watch.PollResetAttempted51||watch.Next<=now)throw new Exception("Post-boot collector recovery is not bounded/durable");
   watch.PollResetAttempted51=false;PollingControl43.Pause();await TryResetRebootPoll51(row.Host,watch,target,Reset);if(resets!=1)throw new Exception("Master OFF launched recovery");
   if(queue.Count!=pending.Count)throw new Exception("Status observation launched work");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"ui-release53.txt"),"PASS: same-boot flags at 6m/12h/1d and JSON restart; new boot/partial/positive; heartbeat animation; missing/partial/error WSUS preserves report; new success clears limitation; historical proof survives work but not current error; one durable recovery; OFF; no update submission");
  }finally{
   Computers.Remove(row);reportCache.Remove(row.Host);serverReports.Remove(row.Host);reportSeverity.Remove(row.Host);overviewErrors.Remove(row.Host);liveReports.Remove(row.Host);readyProofs43.Remove(row.Host);rebootWatches.Remove(row.Host);
   Store.Write("ready-proofs43.json",readyProofs43);Store.Write("wsus-report-cache.json",reportCache);Store.Write("reboot-watches.json",rebootWatches);
   if(paused)PollingControl43.Pause();else PollingControl43.Resume();
  }
 }
}
