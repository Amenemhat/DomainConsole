using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Collections.Generic;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  // Never infer completion of an actual operation from age. Only an absent job
  // description AND result, with no scheduler entry/agent, is a lost reservation.
  static Func<string,bool> ReservationAbsent56=ReservationAbsentOnClient56;
  static bool ReservationAbsentOnClient56(string id){
   using(var q=new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name='DomainConsole.Agent.exe'"))
    foreach(ManagementObject p in q.Get()){var command=p["CommandLine"] as string;if(string.IsNullOrEmpty(command)||command.IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0)return false;}
   dynamic scheduler=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));scheduler.Connect();
   foreach(dynamic task in scheduler.GetFolder("\\").GetTasks(1)){
    if(((string)task.Name).IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0)return false;
    if(((string)task.Xml).IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0)return false;
   }
   return true;
  }
  static bool PruneOrphans56(ControlRecord37 record){
   DateTimeOffset updated;
   if(!controlHeld46||!DateTimeOffset.TryParse(record.UpdatedUtc,out updated)||DateTimeOffset.UtcNow-updated<TimeSpan.FromMinutes(5))return false;
   var removed=new List<string>();var root=Path.GetDirectoryName(Folder);
   foreach(var id in record.JobIds.ToArray()){
    Guid parsed;if(!Guid.TryParse(id,out parsed)||id==Job.Id)continue;
    try{
     var folder=Path.Combine(root,id);
     if(Directory.Exists(folder)&&(File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)continue;
     if(new[]{"job.json","status.json","submitted.flag"}.Any(n=>File.Exists(Path.Combine(folder,n))))continue;
     if(!ReservationAbsent56(id))continue;
     removed.Add(id);
    }catch{ /* Unknown is not absent. Keep the reservation. */ }
   }
   if(removed.Count==0)return false;
   var backup=ControlFile37+".before-orphan56-"+Guid.NewGuid()+".bak";
   File.Copy(ControlFile37,backup,false);
   record.JobIds.RemoveAll(id=>removed.Contains(id));
   if(removed.Contains(record.LastJobId))record.LastJobId="";
   WriteControl37(record);
   File.AppendAllText(Path.Combine(root,"coordination56.log"),DateTime.UtcNow.ToString("o")+" Released missing reservations: "+string.Join(",",removed)+"; backup="+Path.GetFileName(backup)+Environment.NewLine);
   return true;
  }
 }
}
