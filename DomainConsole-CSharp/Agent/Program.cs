using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.ServiceProcess;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 public class RegistrySnapshot {public string Key {get;set;}public string Name {get;set;}public bool Exists {get;set;}public int Value {get;set;}}
 internal static class Program {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=100};
  static readonly object Gate=new object();static string Folder;static RemoteState State;static RemoteJob Job;
  static string FilePath(string name)=>Path.Combine(Folder,name);
  static string Quote(string s){
   var result=new StringBuilder("\"");int slashes=0;
   foreach(char c in s){if(c=='\\'){slashes++;continue;}if(c=='\"'){result.Append('\\',slashes*2+1);result.Append(c);}else{result.Append('\\',slashes);result.Append(c);}slashes=0;}
   result.Append('\\',slashes*2);result.Append('\"');return result.ToString();
  }
  static void WriteJson(string name,object value){lock(Gate){var path=FilePath(name);File.WriteAllText(path+".tmp",Json.Serialize(value),new UTF8Encoding(true));if(File.Exists(path))File.Delete(path);File.Move(path+".tmp",path);}}
  static void Save(){lock(Gate){State.Updated=DateTime.UtcNow.ToString("o");WriteJson("status.json",State);}}
  static void Log(string message){lock(Gate)File.AppendAllText(FilePath("runner.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine,Encoding.UTF8);}
  static void Stage(string value){lock(Gate){DateTimeOffset started;if(DateTimeOffset.TryParse(State.StageStarted,out started))Log("Этап завершён: "+State.Stage+"; секунд: "+(int)(DateTimeOffset.UtcNow-started).TotalSeconds);State.Stage=value;State.StageStarted=DateTime.UtcNow.ToString("o");State.Progress=null;State.ProgressValue="";State.LastOutputUtc="";State.LastProgressUtc="";Log("Начат этап: "+value);Save();}}
  static int Report(){Stage("WSUS · запрос отправки отчёта");int a=Run("UsoClient.exe","Report","report"),b=Run("wuauclt.exe","/reportnow","report");State.ReportRequestStatus="Запросы завершены: UsoClient="+a+", wuauclt="+b+". Приём сервером ещё не подтверждён.";Log(State.ReportRequestStatus);Save();return a==0||b==0?0:1;}
  static bool Cancelled()=>File.Exists(FilePath("cancel.flag"));
  [DllImport("kernel32.dll")]static extern uint GetOEMCP();
  static int Run(string exe,string args,string prefix=null){
   var psi=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};string name=prefix??"system";
   using(var process=new Process{StartInfo=psi})using(var output=new ProgressLogWriter(FilePath(name+".out")))using(var rawOut=new FileStream(FilePath(name+".stdout.bin"),FileMode.Append,FileAccess.Write,FileShare.ReadWrite))using(var rawErr=new FileStream(FilePath(name+".stderr.bin"),FileMode.Append,FileAccess.Write,FileShare.ReadWrite)){
    var native=Encoding.GetEncoding((int)GetOEMCP());process.Start();if(State!=null){lock(Gate){State.ProcessId=process.Id;State.ProcessName=Path.GetFileName(exe);Save();}}
    var stdout=Task.Run(()=>OutputReader.Read(process.StandardOutput.BaseStream,rawOut,native,line=>{File.AppendAllText(FilePath(name+".stdout.raw"),line+Environment.NewLine,Encoding.UTF8);output.WriteLine(line);if(State!=null&&!string.IsNullOrWhiteSpace(line)){lock(Gate){State.LastOutputUtc=DateTime.UtcNow.ToString("o");var match=Regex.Match(line,@"(?<!\d)(\d{1,3}(?:[.,]\d+)?)\s*%");if(match.Success&&ProgressText.Key(line)!=null){var number=match.Groups[1].Value.Replace(',','.');decimal value=decimal.Parse(number,System.Globalization.CultureInfo.InvariantCulture);if(value<=100){if(State.ProgressValue!=number)State.LastProgressUtc=State.LastOutputUtc;State.ProgressValue=number;State.Progress=(int)value;}}Save();}}}));
    var stderr=Task.Run(()=>OutputReader.Read(process.StandardError.BaseStream,rawErr,native,line=>File.AppendAllText(FilePath(name+".err"),line+Environment.NewLine,Encoding.UTF8)));
    process.WaitForExit();Task.WaitAll(stdout,stderr);if(State!=null){lock(Gate){State.ProcessId=0;State.ProcessName="";Save();}}return process.ExitCode;
   }
  }
  static int PowerShell(string code,string prefix){
   var file=FilePath(prefix+".command.txt");File.WriteAllText(file,code,new UTF8Encoding(true));
   var loader="[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false);$OutputEncoding=[Console]::OutputEncoding;$ProgressPreference='SilentlyContinue';$ErrorActionPreference='Stop';try{& ([ScriptBlock]::Create([IO.File]::ReadAllText('"+file.Replace("'","''")+"',[Text.Encoding]::UTF8)))}catch{[Console]::Error.WriteLine($_.Exception.Message);exit 1}";
   var exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
   return Run(exe,"-NoLogo -NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(loader)),prefix);
  }
  static void StartupTask(string suffix,string mode){
   var executable=Path.Combine(Folder,"DomainConsole.Agent.exe");var action=Quote(executable)+" "+mode+" "+Quote(Folder);
   int code=Run("schtasks.exe","/Create /TN "+Quote("DomainConsoleCSharp-"+suffix+"-"+Job.Id)+" /TR "+Quote(action)+" /SC ONSTART /RU SYSTEM /RL HIGHEST /F","scheduler");
   if(code!=0)throw new Exception("Cannot register startup recovery task: "+code);
  }
  static void DeleteTask(string suffix){Run("schtasks.exe","/Delete /TN "+Quote("DomainConsoleCSharp-"+suffix+"-"+Job.Id)+" /F","scheduler");}
  static void EnableMicrosoft(){
   var list=new List<RegistrySnapshot>();
   foreach(var pair in new[]{new[]{@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate","DoNotConnectToWindowsUpdateInternetLocations"},new[]{@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU","UseWUServer"}}){
    using(var key=Registry.LocalMachine.OpenSubKey(pair[0])){var value=key==null?null:key.GetValue(pair[1]);if(value!=null&&!(value is int))throw new Exception("Unexpected registry type: "+pair[1]);list.Add(new RegistrySnapshot{Key=pair[0],Name=pair[1],Exists=value!=null,Value=value==null?0:(int)value});}
   }
   using(var service=new ServiceController("wuauserv")){WriteJson("service-original.json",service.Status.ToString());}
   WriteJson("original.json",list);StartupTask("Restore","restore");State.RestoreStatus="Pending";Save();
   foreach(var item in list)using(var key=Registry.LocalMachine.CreateSubKey(item.Key)){key.SetValue(item.Name,0,RegistryValueKind.DWord);}
   int stop=Run("net.exe","stop wuauserv","services");Log("wuauserv stop result: "+stop);
   if(Run("net.exe","start wuauserv","services")!=0)throw new Exception("Cannot start wuauserv after source change.");
  }
  static void Restore(){
   if(!File.Exists(FilePath("original.json")))return;
   var list=Json.Deserialize<List<RegistrySnapshot>>(File.ReadAllText(FilePath("original.json")));
   foreach(var item in list)using(var key=Registry.LocalMachine.CreateSubKey(item.Key)){if(item.Exists)key.SetValue(item.Name,item.Value,RegistryValueKind.DWord);else key.DeleteValue(item.Name,false);}
   Log("wuauserv stop result: "+Run("net.exe","stop wuauserv","services"));
   if(Run("net.exe","start wuauserv","services")!=0)throw new Exception("Settings restored, but wuauserv restart failed.");
   if(File.Exists(FilePath("service-original.json"))&&Json.Deserialize<string>(File.ReadAllText(FilePath("service-original.json")))=="Stopped"){if(Run("net.exe","stop wuauserv","services")!=0)throw new Exception("Cannot restore stopped service state.");}
   WriteJson("restored.json",new{RestoredAt=DateTime.UtcNow.ToString("o")});if(State!=null){State.RestoreStatus="Restored";Save();}DeleteTask("Restore");
  }
  static dynamic Com(string name){var type=Type.GetTypeFromProgID(name);if(type==null)throw new Exception("COM component missing: "+name);return Activator.CreateInstance(type);}
  static int Updates(string prefix,bool scanOnly=false){
   Stage("WSUS · проверка политики клиента");string source;
   using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate")){source=key==null?"":key.GetValue("WUServer") as string;if(string.IsNullOrWhiteSpace(source))throw new Exception("Политика WUServer не задана.");Log("Источник клиента: "+source);}
   using(var au=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU")){if(au==null||Convert.ToInt32(au.GetValue("UseWUServer",0))!=1)throw new Exception("UseWUServer не равен 1. Проверка остановлена без изменения политики.");}
   Uri address;if(!Uri.TryCreate(source,UriKind.Absolute,out address)||(address.Scheme!="http"&&address.Scheme!="https"))throw new Exception("Некорректный адрес WUServer.");
   Stage("WSUS · DNS и предварительная проверка TCP");try{var addresses=System.Net.Dns.GetHostAddresses(address.DnsSafeHost);Log("DNS: "+string.Join(", ",addresses.Select(a=>a.ToString())));
   using(var tcp=new System.Net.Sockets.TcpClient()){var connect=tcp.ConnectAsync(address.DnsSafeHost,address.Port);if(!connect.Wait(5000))throw new Exception("TCP-подключение к WSUS не завершилось за 5 секунд. Проверьте сеть, VPN и порт.");Log("TCP-порт WSUS доступен. Это не проверка HTTP, TLS или авторизации WUA.");}}catch(Exception ex){Log("Предварительная проверка сети: "+ex.Message+". Продолжается проверка через WUA: он может использовать собственный прокси-маршрут.");}
   Stage("WSUS · создание сеанса Windows Update Agent");
   dynamic session=Com("Microsoft.Update.Session");session.ClientApplicationID="DomainConsole CSharp";
   dynamic search=session.CreateUpdateSearcher();search.ServerSelection=1;search.Online=true;Stage("WSUS · подключение WUA и поиск обновлений");
   dynamic result=search.Search("IsInstalled=0 and IsHidden=0");if((int)result.ResultCode!=2)throw new Exception("Search ResultCode: "+result.ResultCode);
   var details=new List<object>();int failures=0;int total=(int)result.Updates.Count;
   State.UpdatesRemaining=total;Log("Поиск завершён. Применимых обновлений: "+total);Save();
   if(scanOnly){for(int n=0;n<total;n++){dynamic u=result.Updates.Item(n);details.Add(new{Title=(string)u.Title,Downloaded=(bool)u.IsDownloaded});}WriteJson(prefix+".updates.json",details);return Report();}
   for(int i=0;i<total;i++){
    if(Cancelled())break;dynamic update=result.Updates.Item(i);
    if((bool)update.InstallationBehavior.CanRequestUserInput){Log("Skipped interactive update: "+update.Title);continue;}
    if(!(bool)update.EulaAccepted)update.AcceptEula();dynamic one=Com("Microsoft.Update.UpdateColl");one.Add(update);
    Stage("Скачивание · "+(string)update.Title);State.Progress=total==0?100:100*i/total;Save();
    dynamic downloader=session.CreateUpdateDownloader();downloader.Updates=one;dynamic downloaded=downloader.Download();
    if(!(bool)update.IsDownloaded){failures++;details.Add(new{Title=(string)update.Title,Download=(int)downloaded.ResultCode,HResult=(int)downloaded.HResult});continue;}
    Stage("Установка · "+(string)update.Title);dynamic installer=session.CreateUpdateInstaller();installer.Updates=one;installer.AllowSourcePrompts=false;
    dynamic installed=installer.Install();dynamic item=installed.GetUpdateResult(0);int code=(int)item.ResultCode;
    details.Add(new{Title=(string)update.Title,ResultCode=code,HResult=(int)item.HResult,RebootRequired=(bool)installed.RebootRequired});
    if(code!=2)failures++;State.RebootRequired=State.RebootRequired||(bool)installed.RebootRequired;
    State.UpdatesRemaining=total-i-1;WriteJson(prefix+".updates.json",details);if(State.RebootRequired)break;
   }
   dynamic info=Com("Microsoft.Update.SystemInfo");State.RebootRequired=State.RebootRequired||(bool)info.RebootRequired;WriteJson(prefix+".updates.json",details);
   int report=Report();return failures>0?1:report;
  }
  static void Verify(){
   if(File.Exists(FilePath("original.json"))&&!File.Exists(FilePath("restored.json")))Restore();
   dynamic session=Com("Microsoft.Update.Session");dynamic search=session.CreateUpdateSearcher();search.ServerSelection=1;dynamic result=search.Search("IsInstalled=0 and IsHidden=0");
   State.UpdatesRemaining=(int)result.Updates.Count;State.RebootRequired=(bool)Com("Microsoft.Update.SystemInfo").RebootRequired;
   WriteJson("post-reboot.json",new{Checked=DateTime.UtcNow.ToString("o"),SearchResult=(int)result.ResultCode,RemainingUpdates=State.UpdatesRemaining,RebootRequired=State.RebootRequired});State.Status="VerifiedAfterReboot";State.Stage="Проверка после перезагрузки завершена";Save();DeleteTask("Verify");
  }
  static void EmitEncodingFixture(){var stream=Console.OpenStandardOutput();Action<Encoding,string> write=(encoding,text)=>{var bytes=encoding.GetBytes(text);for(int i=0;i<bytes.Length;i+=3){int count=Math.Min(3,bytes.Length-i);stream.Write(bytes,i,count);stream.Flush();Thread.Sleep(1);}};write(Encoding.ASCII,"Header OEM\r\n");write(Encoding.Unicode,"\r\n\r\nНачало проверки системных файлов.\r\nПроверка 10% завершена.\rПроверка 20% завершена.\r\nЗащита ресурсов Windows обнаружила повреждённые файлы.\r\n");write(new UTF8Encoding(false),"UTF8: Привет мир ✓\r\n");write(Encoding.ASCII,"OEM diagnostic: 25% retained\r\n");}
  static int VerifyOutput(string folder){Directory.CreateDirectory(folder);Folder=folder;State=new RemoteState();var oem="Русская OEM строка\r\n";var decoded=new StringBuilder();using(var input=new MemoryStream(Encoding.GetEncoding(866).GetBytes(oem)))using(var binary=new MemoryStream())OutputReader.Read(input,binary,Encoding.GetEncoding(866),line=>decoded.Append(line));if(decoded.ToString().Trim()!="Русская OEM строка")throw new Exception("OEM866 decoding failed.");var script=FilePath("fixture.cmd");File.WriteAllText(script,"@echo off\r\necho Header\r\necho [==== 10.0%% ====]\r\necho.\r\necho [==== 20.0%% ====]\r\necho Error diagnostic: 25%% complete\r\necho [==== 30.0%% ====]\r\necho Done\r\nexit /b 0\r\n",Encoding.ASCII);if(Run("cmd.exe","/d /c "+Quote(script),"fixture")!=0)return 1;var text=File.ReadAllText(FilePath("fixture.out"));var raw=File.ReadAllText(FilePath("fixture.stdout.raw"));if(text.Contains("10.0%")||!text.Contains("20.0%")||!text.Contains("30.0%")||!text.Contains("Error diagnostic: 25% complete")||!text.Contains("Header")||!text.Contains("Done")||!raw.Contains("10.0%"))throw new Exception("Progress output integration check failed.");var executable=System.Reflection.Assembly.GetExecutingAssembly().Location;if(Run(executable,"emit-encoding-fixture","encoding")!=0)return 1;var encoded=File.ReadAllText(FilePath("encoding.out"));if(!encoded.Contains("Начало проверки")||!encoded.Contains("Проверка 20% завершена.")||encoded.Contains("Проверка 10% завершена.")||!encoded.Contains("повреждённые файлы")||!encoded.Contains("Привет мир ✓")||!encoded.Contains("OEM diagnostic: 25% retained")||encoded.Contains("\u0004"))throw new Exception("Mixed UTF-16 / UTF-8 / OEM pipe check failed.");Console.WriteLine("Progress replaced; Cyrillic mixed encoding and raw binary stdout verified.");return 0;}
  static int Main(string[] args){
   if(args.Length==1&&args[0]=="emit-encoding-fixture"){EmitEncodingFixture();return 0;}
   if(args.Length==2&&args[0]=="output-smoke")return VerifyOutput(args[1]);
   if(args.Length!=2)return 2;Folder=Path.GetFullPath(args[1]);if(!Directory.Exists(Folder))return 2;
   try{Job=Json.Deserialize<RemoteJob>(File.ReadAllText(FilePath("job.json")));Guid parsed;if(!Guid.TryParse(Job.Id,out parsed))throw new Exception("Invalid job ID");
    if(args[0]=="restore"||args[0]=="verify"){
     if(File.Exists(FilePath("status.json")))State=Json.Deserialize<RemoteState>(File.ReadAllText(FilePath("status.json")));else State=new RemoteState{Id=Job.Id};
     if(args[0]=="restore")Restore();else Verify();return 0;
    }
    if(args[0]!="run")return 2;
    if(File.Exists(FilePath("status.json"))){var previous=Json.Deserialize<RemoteState>(File.ReadAllText(FilePath("status.json")));if(previous.Status!="Queued")return 0;}
    State=new RemoteState{Id=Job.Id,Total=Job.Steps.Count,Started=DateTime.UtcNow.ToString("o"),Status="Queued",Stage="Ожидание других заданий"};Save();
    using(var mutex=new Mutex(false,@"Global\DomainConsoleCSharp.Execution")){
     bool owns=false;try{try{owns=mutex.WaitOne();}catch(AbandonedMutexException){owns=true;}
      State.Status="Running";Save();using(var heartbeat=new Timer(o=>{try{Save();}catch{}},null,5000,5000)){
       try{
        if(Cancelled()){State.Status="Cancelled";return 0;}
        if(Job.MicrosoftSource&&Job.Steps.Any(s=>(s.Kind=="Updates"||s.Kind=="UpdateScan")))throw new Exception("Separate Microsoft repair and WSUS update jobs.");
        if(Job.MicrosoftSource)EnableMicrosoft();
        foreach(var step in Job.Steps){
         if(Cancelled()){State.Status="Cancelled";break;}
         State.Step++;Stage(step.Name);var prefix="step-"+State.Step;var stepResult=new StepResult{Name=step.Name,Started=DateTime.UtcNow.ToString("o")};
         try{
          if(step.Kind=="Updates")stepResult.ExitCode=Updates(prefix);
          else if(step.Kind=="UpdateScan")stepResult.ExitCode=Updates(prefix,true);
          else if(step.Kind=="Diagnostics")stepResult.ExitCode=PowerShell("Get-WindowsUpdateLog -LogPath "+"'"+FilePath(prefix+".WindowsUpdate.log").Replace("'","''")+"'",prefix);
          else if(step.Kind=="PowerShell")stepResult.ExitCode=PowerShell(step.Code,prefix);
          else if(step.Kind=="CMD"){
           var file=FilePath(prefix+".cmd");File.WriteAllText(file,step.Code,Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage));stepResult.ExitCode=Run("cmd.exe","/d /c \"\""+file+"\"\"",prefix);
          }else throw new Exception("Unknown command kind");
          if(stepResult.ExitCode==3010){State.RebootRequired=true;stepResult.ExitCode=0;}
         }catch(Exception ex){stepResult.ExitCode=1;stepResult.Error=ex.Message+" [HRESULT 0x"+ex.HResult.ToString("X8")+"]";State.Error=stepResult.Error;Log(ex.ToString());}
         if(stepResult.ExitCode!=0){if(string.IsNullOrWhiteSpace(stepResult.Error))stepResult.Error="Код завершения: "+ExecutionDiagnosis.Code(stepResult.ExitCode)+". "+ExecutionDiagnosis.Explain(stepResult.ExitCode);State.Error=stepResult.Error;Log(stepResult.Error);}
         stepResult.Ended=DateTime.UtcNow.ToString("o");lock(Gate){State.Results.Add(stepResult);}Save();
         if(stepResult.ExitCode!=0&&!Job.ContinueOnError){State.Status="Failed";Save();}
         if((step.Kind=="Updates"||step.Kind=="UpdateScan")&&stepResult.ExitCode!=0){try{PowerShell("Get-WindowsUpdateLog -LogPath '"+FilePath(prefix+".WindowsUpdate.log").Replace("'","''")+"'",prefix+"-diagnostics");}catch(Exception ex){Log(ex.Message);}}
         if(stepResult.ExitCode!=0&&!Job.ContinueOnError)break;
        }
        if(Cancelled())State.Status="Cancelled";
        if(State.Status=="Running")State.Status=State.Results.Any(r=>r.ExitCode!=0)?"CompletedWithErrors":State.RebootRequired?"AwaitingReboot":"Completed";
       }catch(Exception ex){State.Status="Failed";State.Error=ex.Message;Log(ex.ToString());}
       finally{
        if(State.RestoreStatus=="Pending"){try{Restore();}catch(Exception ex){State.RestoreStatus="InterventionRequired";State.Status="InterventionRequired";State.Error=ex.Message;Log(ex.ToString());}}
        State.Ended=DateTime.UtcNow.ToString("o");Save();
        if(State.RebootRequired&&Job.Steps.Any(s=>(s.Kind=="Updates"||s.Kind=="UpdateScan"))){try{StartupTask("Verify","verify");}catch(Exception ex){Log(ex.Message);}}
        if(Job.AutoReboot&&State.RebootRequired&&(State.Status=="Completed"||State.Status=="AwaitingReboot")&&State.RestoreStatus!="InterventionRequired"){
         if(Run("shutdown.exe","/r /t 900 /c \"Domain Console: maintenance completed. Reboot in 15 minutes.\"","reboot")==0){State.Status="RebootScheduled";Save();}
        }
       }
      }
     }finally{if(owns)mutex.ReleaseMutex();}
    }
    return 0;
   }catch(Exception ex){try{Log(ex.ToString());}catch{}Console.Error.WriteLine(ex);return 1;}
  }
 }
}
