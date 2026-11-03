using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  static void OrphanSmoke56(string root){
   Directory.CreateDirectory(root);var job=FixtureJob37("A",Guid.NewGuid().ToString());FixtureFolder37(root,job);
   var orphan=Guid.NewGuid().ToString();var actual=Guid.NewGuid().ToString();Directory.CreateDirectory(Path.Combine(root,actual));File.WriteAllText(Path.Combine(root,actual,"job.json"),"{}");
   var record=new ControlRecord37{OwnerId="A",PlanId=job.PlanId37,LastJobId=orphan,UpdatedUtc=DateTime.UtcNow.AddMinutes(-10).ToString("o"),JobIds=new List<string>{orphan,actual}};WriteControl37(record);
   var probe=ReservationAbsent56;
   try{
    ReservationAbsent56=_=>false;ControlGate37(()=>{if(PruneOrphans56(record))throw new Exception("Live reservation pruned");return true;});
    ReservationAbsent56=_=>throw new IOException("fixture unknown");ControlGate37(()=>{if(PruneOrphans56(record))throw new Exception("Unknown reservation pruned");return true;});
    ReservationAbsent56=_=>true;if(PruneOrphans56(record))throw new Exception("Prune bypassed coordination mutex");
    record.UpdatedUtc=DateTime.UtcNow.ToString("o");ControlGate37(()=>{if(PruneOrphans56(record))throw new Exception("In-flight reservation pruned");return true;});
    record.UpdatedUtc=DateTime.UtcNow.AddMinutes(-10).ToString("o");
    ControlGate37(()=>{if(!PruneOrphans56(record))throw new Exception("Missing job reservation remained blocked");return true;});
    var saved=ReadControl37();if(saved.JobIds.Contains(orphan)||!saved.JobIds.Contains(actual)||saved.OwnerId!="A"||saved.PlanId!=job.PlanId37||Directory.GetFiles(root,"*.bak").Length!=1)throw new Exception("Orphan repair modified unrelated state or lost backup");
    job.Manual56=true;job.OutputChannel="Commands";ReserveControl37();if(ReadControl37().JobIds.Contains(job.Id))throw new Exception("Ordinary manual command acquired WSUS plan");
    File.WriteAllText(FilePath("job.json"),Json.Serialize(job));var before=File.ReadAllText(ControlFile37);
    using(var lane=new Mutex(false,@"Global\DomainConsoleCSharp.Execution.WSUS")){
     bool held=false;try{try{held=lane.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held)throw new Exception("Fixture WSUS lane already occupied");
      using(var child=Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"run \""+Folder+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){
       var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
       if(!child.WaitForExit(60000)){child.Kill();throw new Exception("Manual command blocked behind WSUS");}
       if(child.ExitCode!=0)throw new Exception("Manual child failed: "+stderr.Result);
      }
     }finally{if(held)lane.ReleaseMutex();}
    }
    var result=Json.Deserialize<RemoteState>(File.ReadAllText(FilePath("status.json")));
    if(result.Status!="Completed"||File.ReadAllText(ControlFile37)!=before)throw new Exception("Manual command failed or changed unrelated WSUS ownership");
   }finally{ReservationAbsent56=probe;}
  }
 }
}
