using System.IO;
using System.Text;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRebootObservationFix(){
  var now=DateTimeOffset.UtcNow;var oldBoot=now.AddDays(-2).ToString("o");var watch=new RebootWatch{Confirmed=true,RequestedUtc=now.AddMinutes(-30).ToString("o"),ExpectedUtc=now.AddMinutes(-29),BeforeBootUtc=oldBoot};
  var snapshot=new RebootSnapshot{BootUtc=oldBoot,Required=true};ObserveWatchBoot(watch,snapshot,now);if(!watch.Overdue||!watch.Phase.Contains("не перезагрузился")||RebootReturned(watch,snapshot))throw new Exception("Old boot hidden by normal waiting");
  snapshot.BootUtc=now.AddMinutes(-10).ToString("o");snapshot.Required=null;ObserveWatchBoot(watch,snapshot,now);if(watch.Overdue||!RebootReturned(watch,snapshot))throw new Exception("New boot incorrectly depends on reboot flags");
  snapshot.BootUtc="";ObserveWatchBoot(watch,snapshot,now);if(!watch.Phase.Contains("прочитать не удалось"))throw new Exception("Missing boot reported unchanged");
  var row=Computers.Last();var prior=rebootWatches.GetValueOrDefault(row.Host);try{
   var job=new RemoteJob{Name="Перезагрузка текущего компьютера"};rebootWatches[row.Host]=watch;watch.JobId=job.Id;watch.Done=false;
   RegisterRebootPlan(job,row,new(){RebootPlan=new(){RequestedUtc=watch.RequestedUtc,DueUtc=watch.ExpectedUtc.ToString("o"),DispatchUtc=now.ToString("o"),DispatchExitCode=5,DispatchError="Access denied"},ClientUtc=now.ToString("o")});
   if(!watch.Done||PostRebootText(row.Host).Length!=0)throw new Exception("Dispatch failure still waits");
   watch.Done=false;RegisterRebootPlan(job,row,new(){RebootPlan=new(){Cancelled=true}});if(!watch.Done||!watch.Phase.Contains("отменена"))throw new Exception("Cancelled plan still waits");
  }finally{if(prior==null)rebootWatches.Remove(row.Host);else rebootWatches[row.Host]=prior;Store.Write("reboot-watches.json",rebootWatches);}
  var code=HeaderRebootCode(0,"Dispatch fixture",false).Replace("{{REBOOT_JOB}}","00000000-0000-0000-0000-000000000026");
  if(code.Contains("/f ")||!code.Contains("DispatchExitCode")||!code.Contains("DispatchUtc"))throw new Exception("Unsafe dispatch or missing receipt");
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"header-reboot-dispatch.ps1"),code,new UTF8Encoding(true));
 }
}

