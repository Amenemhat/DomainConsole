using System;
using System.IO;
using System.Management;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 internal static partial class Program {
  // A reservation with no status must not block forever after a reboot. Same-boot
  // absence is deliberately insufficient: deployment may still be in progress.
  static bool RecoverMissingResult49(string file,string id){
   if(!controlHeld46||File.Exists(file))return false;
   try{
    string folder=Path.GetDirectoryName(file),jobFile=Path.Combine(folder,"job.json");
    if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(jobFile)&FileAttributes.ReparsePoint)!=0)return false;
    var savedJob=Json.Deserialize<RemoteJob>(File.ReadAllText(jobFile));DateTimeOffset created;
    if(savedJob==null||savedJob.Id!=id||!DateTimeOffset.TryParse(savedJob.Created,out created)||DateTimeOffset.UtcNow-created<TimeSpan.FromMinutes(2))return false;
    DateTime boot=DateTime.MinValue;
    using(var q=new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))foreach(ManagementObject os in q.Get())boot=ManagementDateTimeConverter.ToDateTime((string)os["LastBootUpTime"]).ToUniversalTime();
    if(boot<=created.UtcDateTime)return false;
    using(var q=new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name='DomainConsole.Agent.exe'"))foreach(ManagementObject p in q.Get()){var command=p["CommandLine"] as string;if(command==null||command.IndexOf(folder,StringComparison.OrdinalIgnoreCase)>=0)return false;}
    dynamic scheduler=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));scheduler.Connect();
    foreach(dynamic task in scheduler.GetFolder("\\").GetTasks(1))if(((string)task.Name).StartsWith("DomainConsoleCSharp-"+id,StringComparison.OrdinalIgnoreCase)&&((int)task.State==4||(int)task.State==2))return false;
    if(InstallerBusy37()||!RecoverInterruptedServices47(folder))return false;
    var now=DateTime.UtcNow.ToString("o");
    var state=new RemoteState{Id=id,Status="InterventionRequired",Started=savedJob.Created,Updated=now,Ended=now,StageEnded=now,Stage="Результат прерванной операции неизвестен",Error="DC_INTERRUPTED46: После новой загрузки Windows файл результата отсутствует; исполнитель и выполняющаяся задача не найдены. Повторная операция автоматически не запускается. Выполните свежую проверку результата."};
    string temp=file+"."+Guid.NewGuid()+".tmp";
    try{File.WriteAllText(temp,Json.Serialize(state));File.Move(temp,file);}finally{if(File.Exists(temp))File.Delete(temp);}
    return true;
   }catch{return false;}
  }
  static void MissingResultSmoke49(string root){
   var job=FixtureJob37("missing-result49");FixtureFolder37(root,job);string file=FilePath("status.json");var busy=InstallerBusy37;
   try{
    InstallerBusy37=()=>false;
    if(ControlGate37(()=>JobFinished37(job.Id))||File.Exists(file))throw new Exception("Same-boot missing result incorrectly released");
    job.Created="2000-01-01T00:00:00Z";File.WriteAllText(FilePath("job.json"),Json.Serialize(job));
    InstallerBusy37=()=>true;
    if(ControlGate37(()=>JobFinished37(job.Id))||File.Exists(file))throw new Exception("Missing result bypassed active WUA");
    InstallerBusy37=()=>false;
    if(RecoverMissingResult49(file,job.Id)||File.Exists(file))throw new Exception("Missing-result recovery bypassed coordination gate");
    if(!ControlGate37(()=>JobFinished37(job.Id)))throw new Exception("Dead pre-boot reservation remained blocked");
    var state=Json.Deserialize<RemoteState>(File.ReadAllText(file));
    if(state.Status!="InterventionRequired"||!state.Error.StartsWith("DC_INTERRUPTED46:")||state.Ended.Length==0)throw new Exception("Missing result reported success");
    var original=File.ReadAllText(file);
    if(!ControlGate37(()=>JobFinished37(job.Id))||File.ReadAllText(file)!=original)throw new Exception("Missing-result recovery is not idempotent");
   }finally{InstallerBusy37=busy;}
  }
 }
}
