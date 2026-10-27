using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  static bool ReadOnly37(RemoteJob job)=>job.Steps.Count>0&&job.Steps.All(s=>s.Kind=="Diagnostics"||s.Kind=="ClientDiagnostics"||s.Kind=="DeliveryDiagnostics"||s.Code.Contains("$dcReadOnly37=$true")||s.Code.Contains("$dcUpdateWizard=$true"));
  static string ControlFile37=>Path.Combine(Path.GetDirectoryName(Folder),"control37.json");
  static ControlRecord37 ReadControl37(){var value=File.Exists(ControlFile37)?Json.Deserialize<ControlRecord37>(File.ReadAllText(ControlFile37)):new ControlRecord37();if(value.Protocol!=1)throw new InvalidOperationException("Несовместимый протокол управления; обновите консоль.");return value;}
  static void WriteControl37(ControlRecord37 value){var temp=ControlFile37+"."+Guid.NewGuid()+".tmp";try{File.WriteAllText(temp,Json.Serialize(value));if(File.Exists(ControlFile37))File.Replace(temp,ControlFile37,null);else File.Move(temp,ControlFile37);}finally{if(File.Exists(temp))File.Delete(temp);}}
  [ThreadStatic] static bool controlHeld46;
  static T ControlGate37<T>(Func<T> action){using(var mutex=new Mutex(false,@"Global\DomainConsoleCSharp.Coordination37")){bool owns=false;try{try{owns=mutex.WaitOne(10000);}catch(AbandonedMutexException){owns=true;}if(!owns)throw new TimeoutException("Проверка владельца клиента занята; действие не начато.");bool previous=controlHeld46;controlHeld46=true;try{return action();}finally{controlHeld46=previous;}}finally{if(owns)mutex.ReleaseMutex();}}}
  static bool JobFinished37(string id){
   Guid guid;if(!Guid.TryParse(id,out guid))return false;var file=Path.Combine(Path.GetDirectoryName(Folder),id,"status.json");if(!File.Exists(file))return false;
   RemoteState state;try{state=Json.Deserialize<RemoteState>(File.ReadAllText(file));}catch{return false;}
   if(state!=null&&string.IsNullOrEmpty(state.Ended)&&RecoverInterrupted46(file,state))state=Json.Deserialize<RemoteState>(File.ReadAllText(file));
   if(state==null||string.IsNullOrEmpty(state.Ended)||state.RestoreStatus=="Pending"||state.RestoreStatus=="InterventionRequired"||state.DiagnosticStatus=="Pending"||state.DiagnosticStatus=="Running")return false;
   if(state.Status=="RebootScheduled"){
    var receipt=Path.Combine(Path.GetDirectoryName(Folder),id,"reboot-plan.json");bool cancelled=false;DateTime requested;
    if(File.Exists(receipt)){try{var data=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(receipt));cancelled=data.ContainsKey("Cancelled")&&Convert.ToBoolean(data["Cancelled"]);}catch{return false;}}
    if(!cancelled&&DateTime.TryParse(state.Started,out requested)){
     bool newBoot=false;using(var query=new System.Management.ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))foreach(System.Management.ManagementObject os in query.Get())newBoot=System.Management.ManagementDateTimeConverter.ToDateTime((string)os["LastBootUpTime"]).ToUniversalTime()>requested.ToUniversalTime();if(!newBoot)return false;
    }
   }
   if(state.ExecutorPid37>0){try{using(var p=Process.GetProcessById(state.ExecutorPid37)){if(!p.HasExited&&p.StartTime.ToUniversalTime().ToString("o")==state.ExecutorStarted37)return false;}}catch(ArgumentException){}catch{return false;}}
   return true;
  }
  static bool NoActiveJobs37(ControlRecord37 record)=>record.JobIds.All(JobFinished37);
  static void ReserveControl37(){if(ReadOnly37(Job))return;ControlGate37(()=>{
   var record=ReadControl37();if(record.Protocol!=1)throw new InvalidOperationException("Несовместимый протокол управления; обновите консоль.");if(record.JobIds.Contains(Job.Id)&&record.OwnerId==Job.ConsoleId37&&record.PlanId==Job.PlanId37)return true;
   bool idle=NoActiveJobs37(record);
   if(Job.AutomaticChild31&&Job.PlanId37.Length>0){var parent=Path.Combine(Path.GetDirectoryName(Folder),Job.PlanId37,"status.json");if(File.Exists(parent)){var prior=Json.Deserialize<RemoteState>(File.ReadAllText(parent));if(prior!=null&&prior.Error.StartsWith("DC_INTERRUPTED46:",StringComparison.Ordinal))throw new InvalidOperationException("Прежняя операция прервана; требуется ручная проверка результата, автоматическое продолжение остановлено.");}}
   if(Job.AutomaticChild31&&Job.PlanId37.Length>0&&Job.PlanId37!=Job.Id&&record.PlanId!=Job.PlanId37)throw new InvalidOperationException("DC_CONTROL_BUSY37|Прежний план больше не владеет клиентом; продолжение не запускалось.");
   // A plan remains owned across operations, lost connections and reboots.
   if(record.OwnerId.Length>0&&record.OwnerId!=Job.ConsoleId37&&(!idle||record.PlanId.Length>0))throw new InvalidOperationException("DC_CONTROL_BUSY37|Клиент обслуживается с «"+record.OwnerLabel+"». "+record.Operation+". Дождитесь завершения или явно передайте управление после проверки.");
   if(Job.ConsoleId37.Length==0&&record.OwnerId.Length>0)throw new InvalidOperationException("DC_CONTROL_BUSY37|Обновите консоль до 0.37: клиент использует согласованное управление.");
   if(record.OwnerId==Job.ConsoleId37&&Job.PlanId37.Length>0&&record.PlanId!=Job.PlanId37&&(!idle||(Job.AutomaticChild31&&Job.PlanId37!=Job.Id)))throw new InvalidOperationException("DC_CONTROL_BUSY37|Новый запуск не выполнен: другой план этой консоли ещё не завершён.");
   if(record.PlanId.Length>0&&Job.PlanId37.Length==0&&record.OwnerId!=Job.ConsoleId37)throw new InvalidOperationException("DC_CONTROL_BUSY37|План обслуживания принадлежит другой консоли.");
   if(idle)record.JobIds.Clear();record.AgentPath37=typeof(Program).Assembly.Location;record.OwnerId=Job.ConsoleId37;record.OwnerLabel=Job.ConsoleLabel37;
   if(Job.PlanId37.Length>0)record.PlanId=Job.PlanId37;record.LastJobId=Job.Id;record.Operation=Job.Name;record.UpdatedUtc=DateTime.UtcNow.ToString("o");if(record.StartedUtc.Length==0)record.StartedUtc=record.UpdatedUtc;
   if(!record.JobIds.Contains(Job.Id))record.JobIds.Add(Job.Id);WriteControl37(record);return true;
  });}
  static bool ReleaseControl37(){return ControlGate37(()=>{var record=ReadControl37();if(record.OwnerId!=Job.ConsoleId37||record.PlanId!=Job.PlanId37)return true;if(!NoActiveJobs37(record))return false;WriteControl37(new ControlRecord37());return true;});}
  static Func<bool> InstallerBusy37=()=> (bool)Com("Microsoft.Update.Installer").IsBusy;
  static void ClaimControl37(){ControlGate37(()=>{
   var record=ReadControl37();if(!NoActiveJobs37(record))throw new InvalidOperationException("Передача невозможна: есть незавершённое задание либо его завершение не подтверждено.");
   if(InstallerBusy37())throw new InvalidOperationException("Установщик Windows Update занят; передача управления не выполнена.");
   WriteControl37(new ControlRecord37{OwnerId=Job.ConsoleId37,OwnerLabel=Job.ConsoleLabel37,UpdatedUtc=DateTime.UtcNow.ToString("o")});return true;
  });}
 }
}
