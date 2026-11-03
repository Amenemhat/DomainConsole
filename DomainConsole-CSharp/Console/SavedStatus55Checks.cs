using System.IO;
using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifySavedStatus55(){
  const string host="saved-status55.fixture.invalid";
  var now=DateTimeOffset.UtcNow;var stamp=now.AddHours(-12).ToString("o");
  var row=new ComputerRow(new(){Host=host,Name="Saved status 0.53"});
  var proof=new ReadyProof43{Key=Identity40.EndpointKey(ConnectionStore.Capture(row.Target))+"|"+OverviewServer+"|ssl="+(WsusSsl.IsChecked==true)+"|",At=stamp,Report=stamp,Scan=stamp,Reboot=stamp};
  int queued=queue.Count;
  try{
   // The 0.53 schema is intentionally unchanged. Reload from JSON, without a live report or client call.
   readyProofs43[host]=JsonSerializer.Deserialize<ReadyProof43>(JsonSerializer.Serialize(proof,Store.Options),Store.Options)!;
   foreach(var link in new[]{"Unknown","SavedConnected","Unreachable","Access"}){
    clientConnections.Remove(host);
    if(link=="SavedConnected")clientConnections[host]=new(){LastSuccess=now.AddHours(-12)};
    if(link=="Unreachable")clientConnections[host]=new(){LastSuccess=now.AddHours(-12),Error="offline",Lost38=true};
    if(link=="Access")clientConnections[host]=new(){LastSuccess=now.AddHours(-12),Error="Access is denied"};
    var assessment=AssessClientCore41(row);
    if(assessment.Kind!="Success"||!assessment.Details.Contains(Display36.Date(stamp)))throw new Exception("Saved 0.53 proof lost after restart: "+link+" / "+assessment.Title);
    row.SetOutcome(assessment.Kind,assessment.Details);row.SetConnection(link,"fixture");
    if(row.DisplayOutcome51!="Success")throw new Exception("Connection replaced successful update glyph: "+link);
    foreach(var result in new[]{"Error","Reboot","Needed"}){row.SetOutcome(result,"fixture");if(row.DisplayOutcome51!=result)throw new Exception("Connection hides retained problem: "+result);}
   }
   ConnectionStore.Profiles[host]=new(){Disconnected54=true};
   if(AssessClientCore41(row).Kind!="Success")throw new Exception("Manual disconnect erased history");
   ConnectionStore.Profiles.Remove(host);
   rebootSnapshots[host]=new(){Required=false,CollectedUtc=stamp};rebootProbeFailures40.Add(host);
   if(AssessClientCore41(row).Kind!="Success")throw new Exception("Failed read erased previous successful result");
   rebootProbeFailures40.Remove(host);
   rebootSnapshots[host]=new(){Required=true,CollectedUtc=now.ToString("o"),Sources=new(){"CBS"}};
   if(AssessClientCore41(row).Kind!="Reboot")throw new Exception("Saved success hides newly confirmed reboot");
   rebootSnapshots.Remove(host);readyProofs43.Remove(host);
   if(AssessClientCore41(row).Kind=="Success")throw new Exception("Unknown client falsely restored as successful");
   if(queue.Count!=queued)throw new Exception("Restoring status submitted client work");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"ui-saved-status55.txt"),"PASS: 0.53 proof JSON reload; no live report; unknown/offline/access/disconnected; retained errors/reboot; failed read; new reboot precedence; no invented success; no task submission.");
  }finally{readyProofs43.Remove(host);clientConnections.Remove(host);ConnectionStore.Profiles.Remove(host);rebootSnapshots.Remove(host);rebootProbeFailures40.Remove(host);criteria45.Remove(host);}
 }
}
