using System.IO;
using System.Text;
namespace DomainConsole;
public partial class MainWindow {
 bool outputCleanupBusy58;DateTimeOffset nextOutputCleanup58=DateTimeOffset.UtcNow.AddMinutes(1);
 async Task CleanOutput58(){
  if(closing||outputCleanupBusy58||DateTimeOffset.UtcNow<nextOutputCleanup58)return;outputCleanupBusy58=true;nextOutputCleanup58=DateTimeOffset.UtcNow.AddHours(1);
  try{
   var limits=Store.Read("history-limits58.json",new HistoryLimits58());limits.Validate();
   var hosts=Computers.Select(r=>r.Host).Concat(History.SelectMany(j=>j.Targets.Select(t=>t.Host))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
   foreach(var host in hosts){
    if(closing)return;
    if(!Directory.Exists(EventFolder31(host)))continue;
    var task=WriteEvents39(host,new(),eventWrites39.GetValueOrDefault(host)??Task.CompletedTask,Store.Read("event-days31.json",30));eventWrites39[host]=task;await task;
   }
   var eligible=History.SelectMany(j=>j.Targets.Select(t=>(Job:j,Target:t))).Where(x=>!ActiveTarget(x.Target)&&x.Target.State!=null&&x.Target.State.DiagnosticStatus is not ("Pending" or "Running" or "LegacyRunning")&&x.Target.State.RestoreStatus is not ("Pending" or "InterventionRequired")&&!IdentityConflict58(x.Target.Host)&&DateTimeOffset.TryParse(x.Target.State.Ended,out _)).ToList();
   var snapshot=eligible.Select(x=>(Key:x.Job.Id+"|"+x.Target.Host,Host:x.Target.Host,At:DateTimeOffset.Parse(x.Target.State!.Ended),Output:x.Target.Output,Folder:Path.Combine(Store.Root,"Logs",x.Target.Host+"_"+x.Job.Id),Archived:archivedJobs.Contains(x.Job.Id+x.Target.Host))).ToList();
   var removal=await Task.Run(()=>{
    var files=new Dictionary<string,string>();var candidates=new List<OutputCandidate58>();
    foreach(var item in snapshot){
     candidates.Add(new("output|"+item.Key,item.Host,item.At,Encoding.UTF8.GetByteCount(item.Output)));
     if(!item.Archived||!Directory.Exists(item.Folder)||(File.GetAttributes(item.Folder)&FileAttributes.ReparsePoint)!=0)continue;
     foreach(var path in Directory.GetFiles(item.Folder)){
      var info=new FileInfo(path);if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.Name is "status.json" or "job.json" or "updates.json")continue;
      string key="file|"+path;files[key]=path;candidates.Add(new(key,item.Host,item.At,info.Length));
     }
    }
    var expired=OutputRetention58.Expired(candidates,DateTimeOffset.UtcNow,limits);
    foreach(var key in expired.Where(files.ContainsKey)){try{File.Delete(files[key]);}catch(IOException){}catch(UnauthorizedAccessException){}}
    return expired;
   });
   if(closing)return;bool changed=false;
   foreach(var item in eligible){
    var original=snapshot.Single(x=>x.Key==item.Job.Id+"|"+item.Target.Host);
    if(removal.Contains("output|"+original.Key)&&!ActiveTarget(item.Target)&&item.Target.Output==original.Output){item.Target.Output="## Результат\nПодробный вывод удалён по сроку или лимиту хранения. Статус задания и результаты обновлений сохранены.\nЗавершено: "+Display36.Date(item.Target.State!.Ended);changed=true;}
   }
   if(changed){Save();if(ComputerGrid.SelectedItem is ComputerRow row)ShowCurrent();}
  }catch(Exception ex){Notify("Очистка подробных журналов отложена: "+ConnectionBrief31(ex.Message));}
  finally{outputCleanupBusy58=false;}
 }
}
