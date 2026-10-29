using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.ServiceProcess;
using System.Collections.Generic;
using System.Globalization;
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
  internal void Remember(Process child){
   pid=child.Id;started=child.StartTime.ToUniversalTime();Directory.CreateDirectory(root);
   if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Каталог восстановления служб является ссылкой");
   string file=Path.Combine(root,"child.identity"),temp=file+"."+Guid.NewGuid()+".tmp";
   try{File.WriteAllText(temp,pid.ToString(CultureInfo.InvariantCulture)+"\n"+started.ToString("o"));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
  // A different executor may recover only a persisted, provably exited child.
  // Missing/partial identity is not evidence that the maintenance script stopped.
  internal void ReadIdentity(){
   string file=Path.Combine(root,"child.identity");
   if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Недопустимая ссылка в записи исполнителя");
   var lines=File.ReadAllLines(file);DateTime parsed;int value;
   if(lines.Length!=2||!int.TryParse(lines[0],out value)||value<=0||!DateTime.TryParseExact(lines[1],"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out parsed)||parsed.Kind!=DateTimeKind.Utc)throw new IOException("Не подтверждена идентичность прежнего PowerShell");
   pid=value;started=parsed;
  }
 }
}
