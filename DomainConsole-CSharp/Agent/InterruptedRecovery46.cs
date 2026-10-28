using System;
using System.IO;
using System.Diagnostics;
using System.Management;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  // Called only while Coordination37 is held. This reconciles bookkeeping, never
  // retries an update, kills a process, or treats a missing result as success.
  static bool RecoverInterrupted46(string file,RemoteState state){
   if(!controlHeld46||state==null||!string.IsNullOrEmpty(state.Ended)||state.Status!="Running"&&state.Status!="Queued"||state.RestoreStatus=="Pending"||state.RestoreStatus=="InterventionRequired"||state.DiagnosticStatus=="Pending"||state.DiagnosticStatus=="Running")return false;
   DateTimeOffset started,updated;if(!DateTimeOffset.TryParse(state.Started,out started)||!DateTimeOffset.TryParse(state.Updated,out updated)||DateTimeOffset.UtcNow-updated<TimeSpan.FromMinutes(2))return false;
   try{
    DateTime boot=DateTime.MinValue;using(var q=new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))foreach(ManagementObject os in q.Get())boot=ManagementDateTimeConverter.ToDateTime((string)os["LastBootUpTime"]).ToUniversalTime();
    bool afterBoot=boot>started.UtcDateTime;
    // Within the same boot, require a known executor identity and update operation.
    // Unknown arbitrary scripts may have detached children and stay blocked.
    if(!afterBoot&&(!state.HasUpdateSteps||state.ExecutorPid37<=0||string.IsNullOrEmpty(state.ExecutorStarted37)))return false;
    if(state.ExecutorPid37>0){try{using(var executor=Process.GetProcessById(state.ExecutorPid37)){if(!executor.HasExited&&executor.StartTime.ToUniversalTime().ToString("o")==state.ExecutorStarted37)return false;}}catch(ArgumentException){}catch{return false;}}
    string folder=Path.GetDirectoryName(file);
    using(var q=new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name='DomainConsole.Agent.exe'"))foreach(ManagementObject p in q.Get()){var command=p["CommandLine"] as string;if(command==null||command.IndexOf(folder,StringComparison.OrdinalIgnoreCase)>=0)return false;}
    dynamic scheduler=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));scheduler.Connect();dynamic root=scheduler.GetFolder("\\");foreach(dynamic task in root.GetTasks(1)){if(((string)task.Name).StartsWith("DomainConsoleCSharp-"+state.Id,StringComparison.OrdinalIgnoreCase)&&((int)task.State==4||(int)task.State==2))return false;}
    if(InstallerBusy37())return false;
    if(!RecoverInterruptedServices47(folder))return false;
    string original=File.ReadAllText(file),backup=file+".interrupted46.bak";
    if(File.Exists(backup))return false;File.WriteAllText(backup,original);
    state=Json.Deserialize<RemoteState>(original);state.Status="InterventionRequired";state.Ended=DateTime.UtcNow.ToString("o");state.Updated=state.Ended;state.StageEnded=state.Ended;
    state.Error="DC_INTERRUPTED46: "+(afterBoot?"Исполнитель прерван до новой загрузки Windows.":"Исполнитель отсутствует, задача не выполняется, конечный результат не сохранён.")+" Результат операции неизвестен; повторная установка автоматически не запускается. Выполните свежую проверку результата.";
    string temp=file+"."+Guid.NewGuid()+".tmp";try{File.WriteAllText(temp,Json.Serialize(state));File.Replace(temp,file,null);}finally{if(File.Exists(temp))File.Delete(temp);}return true;
   }catch{return false;}
  }
 }
}
