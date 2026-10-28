using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.ServiceProcess;
using System.Collections.Generic;
namespace DomainConsole.Agent {
 internal sealed class ServiceRecovery47 {
  internal static readonly string[] Allowed={"UsoSvc","wuauserv","bits","cryptsvc"};
  readonly string root; readonly Action<string> restore;
  internal ServiceRecovery47(string path,Action<string> restoreService=null){root=path;restore=restoreService??StartService;}
  internal string Root=>root;
  internal static bool Protected(string kind)=>kind=="CacheCleanup"||kind=="ReportRecovery";
  static void StartService(string name){
   using(var service=new ServiceController(name)){
    service.Refresh();
    if(service.Status==ServiceControllerStatus.StopPending)service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20));
    service.Refresh();
    if(service.Status==ServiceControllerStatus.Stopped)service.Start();
    service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(20));
   }
  }
  // Called after the child/job handles have been disposed. A failed wait never
  // starts a service concurrently with the still-running maintenance script.
  internal List<string> Recover(){
   var errors=new List<string>();
   if(!Directory.Exists(root))return errors;
   try{
    if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Каталог восстановления служб является ссылкой");
    if(pid>0){try{using(var p=Process.GetProcessById(pid)){
     if(!p.HasExited&&p.StartTime.ToUniversalTime()==started&&!p.WaitForExit(10000))throw new IOException("Остановка PowerShell не подтверждена; восстановление служб отложено");
    }}catch(ArgumentException){}}
    foreach(var file in Directory.GetFiles(root,"*.pending")){
     string name=Path.GetFileNameWithoutExtension(file);
     if(!Allowed.Contains(name,StringComparer.OrdinalIgnoreCase)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0){errors.Add("Недопустимая запись восстановления: "+Path.GetFileName(file));continue;}
     try{restore(name);File.Delete(file);}catch(Exception ex){errors.Add(name+": "+ex.Message);}
    }
   }catch(Exception ex){errors.Add(ex.Message);}
   return errors;
  }
  int pid; DateTime started;
  internal void Remember(Process child){pid=child.Id;started=child.StartTime.ToUniversalTime();}
 }
}
