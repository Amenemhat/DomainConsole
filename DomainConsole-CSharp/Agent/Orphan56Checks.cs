using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
   }finally{ReservationAbsent56=probe;}
  }
 }
}
