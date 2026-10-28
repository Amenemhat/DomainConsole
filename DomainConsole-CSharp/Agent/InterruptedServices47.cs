using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Management;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  static bool IsProtectedInterruptedStep47(string folder,RemoteState state){
   try{var file=Path.Combine(folder,"job.json");if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return false;var job=Json.Deserialize<RemoteJob>(File.ReadAllText(file));return job!=null&&job.Id==state.Id&&job.Id==Path.GetFileName(folder)&&state.Step>0&&state.Step<=job.Steps.Count&&ServiceRecovery47.Protected(job.Steps[state.Step-1].Kind);}catch{return false;}
  }
  static bool RecoverTerminalServices47(string file,RemoteState state){
   if(!controlHeld46)return false;
   try{
    string folder=Path.GetDirectoryName(file);
    using(var q=new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name='DomainConsole.Agent.exe'"))foreach(ManagementObject p in q.Get()){var command=p["CommandLine"] as string;if(command==null||command.IndexOf(folder,StringComparison.OrdinalIgnoreCase)>=0)return false;}
    dynamic scheduler=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));scheduler.Connect();foreach(dynamic task in scheduler.GetFolder("\\").GetTasks(1))if(((string)task.Name).StartsWith("DomainConsoleCSharp-"+state.Id,StringComparison.OrdinalIgnoreCase)&&((int)task.State==4||(int)task.State==2))return false;
    if(InstallerBusy37()||!RecoverInterruptedServices47(folder))return false;
    string before=File.ReadAllText(file),backup=file+".service-recovery47.bak";if(!File.Exists(backup))File.WriteAllText(backup,before);
    state.ServiceRecoveryError47="";state.Error+="\nВосстановление записанных служб подтверждено; исход операции не изменён.";
    string temp=file+"."+Guid.NewGuid()+".tmp";try{File.WriteAllText(temp,Json.Serialize(state));File.Replace(temp,file,null);}finally{if(File.Exists(temp))File.Delete(temp);}return true;
   }catch{return false;}
  }
  // Coordination gate, dead executor/task and idle WUA have already been checked.
  // This starts only services recorded by the interrupted protected step; it
  // never stops services or resumes the cache mutation / installation itself.
  static bool RecoverInterruptedServices47(string folder){
   string report=Path.Combine(folder,"service-recovery-interrupted47.log");
   try{
    if(!controlHeld46||(File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return false;
    var journals=Directory.GetDirectories(folder,"*.service-recovery");
    if(journals.Length==0)return true;
    var jobFile=Path.Combine(folder,"job.json");
    if((File.GetAttributes(jobFile)&FileAttributes.ReparsePoint)!=0)return false;
    var job=Json.Deserialize<RemoteJob>(File.ReadAllText(jobFile));
    if(job==null||job.Id!=Path.GetFileName(folder))return false;
    foreach(var path in journals){
     if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Каталог восстановления является ссылкой");
     if(Directory.GetFiles(path,"*.pending").Length==0)continue;
     var match=Regex.Match(Path.GetFileName(path),@"^step-([1-9][0-9]*)\.service-recovery$");int step;
     if(!match.Success||!int.TryParse(match.Groups[1].Value,out step)||step>job.Steps.Count||!ServiceRecovery47.Protected(job.Steps[step-1].Kind))throw new IOException("Не подтверждена принадлежность записи этапу восстановления");
     var recovery=new ServiceRecovery47(path,serviceRestorerFixture47);recovery.ReadIdentity();
     var errors=recovery.Recover();
     if(errors.Count>0)throw new IOException(string.Join("; ",errors));
    }
    File.WriteAllText(report,DateTime.UtcNow.ToString("o")+" Записанные службы восстановлены; исход прерванной операции остаётся неизвестным.");return true;
   }catch(Exception ex){File.WriteAllText(report,DateTime.UtcNow.ToString("o")+" Восстановление служб не подтверждено: "+ex.Message);return false;}
  }
 }
}
