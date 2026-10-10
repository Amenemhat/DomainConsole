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
 internal static partial class Program {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=100};
  static readonly object Gate=new object();static string Folder;static RemoteState State;static RemoteJob Job;
  static string FilePath(string name)=>Path.Combine(Folder,name);
  static string Quote(string s){
   var result=new StringBuilder("\"");int slashes=0;
   foreach(char c in s){if(c=='\\'){slashes++;continue;}if(c=='\"'){result.Append('\\',slashes*2+1);result.Append(c);}else{result.Append('\\',slashes);result.Append(c);}slashes=0;}
   result.Append('\\',slashes*2);result.Append('\"');return result.ToString();
  }
  static bool statusWritePending;static DateTime nextStatusWrite;
  static void WriteJson(string name,object value){lock(Gate){var path=FilePath(name);var staging=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(staging,Json.Serialize(value),new UTF8Encoding(true));for(int attempt=0;;attempt++){try{if(File.Exists(path))File.Replace(staging,path,null);else File.Move(staging,path);return;}catch(IOException){if(attempt>=4)throw;Thread.Sleep(50*(attempt+1));}}}finally{if(File.Exists(staging))try{File.Delete(staging);}catch(IOException){}}}}
  static void Save(){lock(Gate){State.Updated=DateTime.UtcNow.ToString("o");if(statusWritePending&&DateTime.UtcNow<nextStatusWrite)return;try{WriteJson("status.json",State);if(statusWritePending)Log("Запись статуса восстановлена; операция продолжала контролироваться агентом.");statusWritePending=false;}catch(IOException ex){if(!statusWritePending)Log("WARNING: Временно не удалось сохранить статус; операция продолжает выполняться: "+ex.Message);statusWritePending=true;nextStatusWrite=DateTime.UtcNow.AddSeconds(3);}}}
  static void Log(string message){lock(Gate)File.AppendAllText(FilePath("runner.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine,Encoding.UTF8);}
  static void Stage(string value){lock(Gate){DateTimeOffset started;if(DateTimeOffset.TryParse(State.StageStarted,out started))Log("Этап завершён: "+State.Stage+"; секунд: "+(int)(DateTimeOffset.UtcNow-started).TotalSeconds);State.Stage=value;State.StageEnded="";State.StageStarted=DateTime.UtcNow.ToString("o");State.Progress=null;State.ProgressValue="";State.LastOutputUtc="";State.LastProgressUtc="";State.LastMovementUtc=DateTime.UtcNow.ToString("o");State.ActivityMessage="";State.TransferText="";State.ForceCandidatePid=0;Log("Начат этап: "+value);Save();}}
  static int Report(){Stage("WSUS · запрос отправки отчёта");int a=Run("UsoClient.exe","Report","report"),b=Run("wuauclt.exe","/reportnow","report");State.ReportRequestStatus="Запросы завершены: UsoClient="+a+", wuauclt="+b+". Это результат команд клиента; получение отчёта проверяется отдельно на сервере.";Log(State.ReportRequestStatus);Save();return a==0||b==0?0:1;}
  static bool Cancelled()=>File.Exists(FilePath("cancel.flag"))||File.Exists(FilePath("stop-now.flag"));
  static string CurrentKind="";
  [DllImport("kernel32.dll")]static extern uint GetOEMCP();
  static int Run(string exe,string args,string prefix=null,int diagnosticTimeout=0){
   var psi=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};string name=prefix??"system";
   using(var command=diagnosticTimeout==0&&(CurrentKind=="CMD"||CurrentKind=="PowerShell")&&name.StartsWith("step-")?new CommandProcess(exe,args):null)using(var group=diagnosticTimeout>0?new DiagnosticProcessGroup():null)using(var ordinary=command==null?new Process{StartInfo=psi}:null)using(var output=new ProgressLogWriter(FilePath(name+".out")))using(var rawOut=new FileStream(FilePath(name+".stdout.bin"),FileMode.Append,FileAccess.Write,FileShare.ReadWrite))using(var rawErr=new FileStream(FilePath(name+".stderr.bin"),FileMode.Append,FileAccess.Write,FileShare.ReadWrite)){
    var process=command==null?ordinary:command.Process;var native=Encoding.GetEncoding((int)GetOEMCP());if(command==null)process.Start();if(group!=null)group.Attach(process);if(State!=null){lock(Gate){if(group!=null)State.DiagnosticProcessId=process.Id;else{State.ProcessId=process.Id;State.ProcessName=Path.GetFileName(exe);}Save();}}
    var stdout=Task.Run(()=>OutputReader.Read((command==null?process.StandardOutput.BaseStream:command.Output),rawOut,native,line=>{File.AppendAllText(FilePath(name+".stdout.raw"),line+Environment.NewLine,Encoding.UTF8);output.WriteLine(line);if(State!=null&&group==null&&!string.IsNullOrWhiteSpace(line)){lock(Gate){State.LastOutputUtc=DateTime.UtcNow.ToString("o");if(ProgressText.Key(line)!=null){var value=ProgressText.Value(line);var number=value.HasValue?value.Value.ToString():"";if(State.ProgressValue!=number)State.LastProgressUtc=State.LastOutputUtc;State.ProgressValue=number;State.Progress=value;if(ProgressText.IsChkdsk(line))State.ActivityMessage=value.HasValue?line.Trim():"CHKDSK: прогресс пересчитывается; ожидайте итог проверки.";}Save();}}}));
    var stderr=Task.Run(()=>OutputReader.Read((command==null?process.StandardError.BaseStream:command.Error),rawErr,native,line=>File.AppendAllText(FilePath(name+".err"),line+Environment.NewLine,Encoding.UTF8)));
    bool timeout=false,interrupted=false;var clock=Stopwatch.StartNew();long signalAt=0;
    while(!process.WaitForExit(250)){
     if(command!=null&&File.Exists(FilePath("force-stop.flag"))){command.Force();State.StopStatus="Forced";State.StopMessage="Принудительное завершение подтверждено администратором";Save();}
     else if(command!=null&&File.Exists(FilePath("stop-now.flag"))){if(!interrupted){interrupted=true;signalAt=clock.ElapsedMilliseconds;bool sent=command.Interrupt();State.StopStatus=sent?"InterruptSent":"ForceRequired";State.StopMessage=sent?"Отправлен Ctrl+C; ожидается завершение команды":"Команда не принимает Ctrl+C; доступно отдельное принудительное завершение";Save();}else if(clock.ElapsedMilliseconds-signalAt>5000&&State.StopStatus!="ForceRequired"){State.StopStatus="ForceRequired";State.StopMessage="Команда продолжает работу после Ctrl+C; доступно принудительное завершение";Save();}}
     if(group!=null&&(clock.ElapsedMilliseconds>=diagnosticTimeout||File.Exists(FilePath("diagnostic-cancel.flag"))||(File.Exists(FilePath("stop-now.flag"))&&CurrentKind!="ReportRecovery"))){timeout=true;group.Stop();if(!process.WaitForExit(10000))throw new Exception("Диагностический процесс не завершился после остановки.");break;}
    }
    if(group!=null&&!Task.WaitAll(new[]{stdout,stderr},10000)){group.Stop();throw new Exception("Диагностические потоки не закрылись после остановки.");}if(group==null)Task.WaitAll(stdout,stderr);if(State!=null){lock(Gate){if(group!=null)State.DiagnosticProcessId=0;else{State.ProcessId=0;State.ProcessName="";}Save();}}return timeout?124:process.ExitCode;
   }
  }
  static int PowerShell(string code,string prefix,int diagnosticTimeout=0){
   var file=FilePath(prefix+".command.txt");File.WriteAllText(file,code,new UTF8Encoding(true));
   var loader="[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false);$OutputEncoding=[Console]::OutputEncoding;$ProgressPreference='SilentlyContinue';$ErrorActionPreference='Stop';$global:LASTEXITCODE=$null;try{& ([ScriptBlock]::Create([IO.File]::ReadAllText('"+file.Replace("'","''")+"',[Text.Encoding]::UTF8)));if($null -ne $LASTEXITCODE){[IO.File]::WriteAllText('"+FilePath(prefix+".native.json").Replace("'","''")+"',[string]$LASTEXITCODE)}}catch{[Console]::Error.WriteLine($_.Exception.Message);[Console]::Error.WriteLine('Target: '+[string]$_.TargetObject);[Console]::Error.WriteLine($_.InvocationInfo.PositionMessage);exit 1}";
   var exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
   return Run(exe,"-NoLogo -NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(loader)),prefix,diagnosticTimeout);
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
   if(Cancelled())return 0;Stage("WSUS · создание сеанса Windows Update Agent");
   dynamic session=Com("Microsoft.Update.Session");session.ClientApplicationID="DomainConsole CSharp";
   dynamic search=session.CreateUpdateSearcher();search.ServerSelection=1;search.Online=true;
   dynamic result=SearchWithRetry(search);if((int)result.ResultCode!=2)throw new Exception("Search ResultCode: "+result.ResultCode);
   var details=new List<object>();int failures=0;int total=(int)result.Updates.Count;
   State.UpdatesRemaining=total;Log("Поиск завершён. Применимых обновлений: "+total);Save();
   if(Cancelled()){WriteJson(prefix+".updates.json",details);return 0;}if(scanOnly){for(int n=0;n<total;n++){dynamic u=result.Updates.Item(n);details.Add(new{Title=(string)u.Title,Downloaded=(bool)u.IsDownloaded});}WriteJson(prefix+".updates.json",details);return Report();}
   for(int i=0;i<total;i++){
    if(Cancelled())break;dynamic update=result.Updates.Item(i);
    if((bool)update.InstallationBehavior.CanRequestUserInput){Log("Skipped interactive update: "+update.Title);continue;}
    if(!(bool)update.EulaAccepted)update.AcceptEula();dynamic one=Com("Microsoft.Update.UpdateColl");one.Add(update);
    Stage("Скачивание · "+(string)update.Title);State.Progress=total==0?100:100*i/total;Save();
    dynamic downloader=session.CreateUpdateDownloader();downloader.Updates=one;dynamic downloaded=AsyncDownload(downloader);
    dynamic downloadItem=downloaded.GetUpdateResult(0);if((int)downloadItem.ResultCode!=2||(int)downloadItem.HResult!=0){failures++;int hr=(int)downloadItem.HResult;if(hr==0)hr=(int)downloaded.HResult;string failure="Не скачано: "+(string)update.Title+"; HRESULT "+ExecutionDiagnosis.Code(hr)+"; ResultCode="+downloadItem.ResultCode+". "+ExecutionDiagnosis.Explain(hr);Log(failure);if(string.IsNullOrEmpty(State.Error))State.Error=failure;ApplyRecommendation(failure);details.Add(new{Title=(string)update.Title,Download=(int)downloadItem.ResultCode,HResult=hr});WriteJson(prefix+".updates.json",details);Save();continue;}
    if(Cancelled()){details.Add(new{Title=(string)update.Title,Download=2,HResult=0});WriteJson(prefix+".updates.json",details);break;}Stage("Установка · "+(string)update.Title);dynamic installer=session.CreateUpdateInstaller();installer.Updates=one;installer.AllowSourcePrompts=false;
    dynamic installed=AsyncInstall(installer);dynamic item=installed.GetUpdateResult(0);int code=(int)item.ResultCode;
    details.Add(new{Title=(string)update.Title,ResultCode=code,HResult=(int)item.HResult,RebootRequired=(bool)installed.RebootRequired});
    if(code!=2){failures++;int hr=(int)item.HResult;string installError="Не установлено: "+(string)update.Title+"; HRESULT "+ExecutionDiagnosis.Code(hr)+". "+ExecutionDiagnosis.Explain(hr);Log(installError);if(string.IsNullOrEmpty(State.Error))State.Error=installError;ApplyRecommendation(installError);}State.RebootRequired=State.RebootRequired||(bool)installed.RebootRequired;
    State.UpdatesRemaining=total-i-1;WriteJson(prefix+".updates.json",details);if(State.RebootRequired)break;
   }
   dynamic info=Com("Microsoft.Update.SystemInfo");State.RebootRequired=State.RebootRequired||(bool)info.RebootRequired;WriteJson(prefix+".updates.json",details);
   if(Cancelled())return failures>0?1:0;int report=Report();return failures>0?1:report;
  }
  static string UpdateLogCode(string prefix){DateTimeOffset start;if(!DateTimeOffset.TryParse(State.Started,out start))start=DateTimeOffset.UtcNow.AddHours(-1);
   string folder=FilePath("").Replace("'","''"),log=FilePath(prefix+".WindowsUpdate.log").Replace("'","''"),quality=FilePath(prefix+".quality.log").Replace("'","''");
   return "$ProgressPreference='SilentlyContinue';$since=[DateTime]::Parse('"+start.UtcDateTime.AddMinutes(-Math.Max(0,Math.Min(1440,Job==null?15:Job.DiagnosticLookbackMinutes))).ToString("o")+"').ToUniversalTime();"+
    "$all=@(Get-ChildItem (Join-Path $env:windir 'Logs\\WindowsUpdate') -Filter '*.etl'|Where-Object {$_.LastWriteTimeUtc -ge $since}|Sort-Object LastWriteTimeUtc -Descending);$etl=@($all|Select-Object -First 8 -ExpandProperty FullName);"+
    "if(!$etl.Count){'WARNING: Нет ETL-файлов, обновлённых в интервале задания; расшифровка недоступна.'|Set-Content '"+quality+"' -Encoding UTF8;exit 0};if($all.Count -gt 8){'WARNING: Выбраны последние 8 ETL-файлов; журнал интервала может быть неполным.'|Set-Content '"+quality+"' -Encoding UTF8};"+
    "'## Технический журнал · преобразование ETL';'ETL выбраны по времени изменения файла; это не гарантирует, что все записи относятся к интервалу задания.';foreach($file in $etl){Get-Item -LiteralPath $file|Select-Object Name,LastWriteTimeUtc|Format-Table|Out-String;Copy-Item -LiteralPath $file -Destination (Join-Path '"+folder+"' ([IO.Path]::GetFileName($file))) -Force -ErrorAction Stop};Get-WindowsUpdateLog -ETLPath $etl -LogPath '"+log+"';"+
    "$bad=@(Select-String -LiteralPath '"+log+"' -Pattern 'No Format Information|Unknown\\(');if($bad.Count){('WARNING: Журнал Windows Update расшифрован не полностью: '+$bad.Count+' записей без формата. Для Windows до 1709 требуется доступ к серверу символов Microsoft; исходные ETL сохранены.')|Add-Content '"+quality+"' -Encoding UTF8}";
  }
  static void CollectUpdateDiagnostics(string prefix)=>CollectDiagnostics(prefix,UpdateLogCode(prefix),300000);
  static void CollectDiagnostics(string prefix,string command,int timeout){State.DiagnosticStatus="Running";State.DiagnosticStarted=DateTime.UtcNow.ToString("o");State.DiagnosticEnded="";State.DiagnosticError="";Save();try{int code=PowerShell(command,prefix+"-diagnostics",timeout);State.DiagnosticStatus=code==124?"TimedOut":code==0?"Completed":"Failed";if(code!=0)State.DiagnosticError=code==124?"Сбор диагностики остановлен по лимиту 5 минут либо запросу администратора. Основное задание уже завершено; доступны частичные журналы.":"Диагностическая команда завершилась с кодом "+ExecutionDiagnosis.Code(code);}catch(Exception ex){State.DiagnosticStatus="Failed";State.DiagnosticError=ex.Message;Log(ex.ToString());}finally{State.DiagnosticProcessId=0;State.DiagnosticEnded=DateTime.UtcNow.ToString("o");Save();}}
  static void Verify(){
   if(File.Exists(FilePath("original.json"))&&!File.Exists(FilePath("restored.json")))Restore();
   dynamic session=Com("Microsoft.Update.Session");dynamic search=session.CreateUpdateSearcher();search.ServerSelection=1;dynamic result=search.Search("IsInstalled=0 and IsHidden=0");
   State.UpdatesRemaining=(int)result.Updates.Count;State.RebootRequired=(bool)Com("Microsoft.Update.SystemInfo").RebootRequired;
   WriteJson("post-reboot.json",new{Checked=DateTime.UtcNow.ToString("o"),SearchResult=(int)result.ResultCode,RemainingUpdates=State.UpdatesRemaining,RebootRequired=State.RebootRequired});State.Status="VerifiedAfterReboot";State.Stage="Проверка после перезагрузки завершена";Save();DeleteTask("Verify");
  }
  static void EmitEncodingFixture(){var stream=Console.OpenStandardOutput();Action<Encoding,string> write=(encoding,text)=>{var bytes=encoding.GetBytes(text);for(int i=0;i<bytes.Length;i+=3){int count=Math.Min(3,bytes.Length-i);stream.Write(bytes,i,count);stream.Flush();Thread.Sleep(1);}};write(Encoding.ASCII,"Header OEM\r\n");write(Encoding.Unicode,"\r\n\r\nНачало проверки системных файлов.\r\nПроверка 10% завершена.\rПроверка 20% завершена.\r\nЗащита ресурсов Windows обнаружила повреждённые файлы.\r\n");write(new UTF8Encoding(false),"UTF8: Привет мир ✓\r\n");write(Encoding.ASCII,"OEM diagnostic: 25% retained\r\n");}
  static int VerifyOutput(string folder){Directory.CreateDirectory(folder);Folder=folder;State=new RemoteState();var oem="Русская OEM строка\r\n";var decoded=new StringBuilder();using(var input=new MemoryStream(Encoding.GetEncoding(866).GetBytes(oem)))using(var binary=new MemoryStream())OutputReader.Read(input,binary,Encoding.GetEncoding(866),line=>decoded.Append(line));if(decoded.ToString().Trim()!="Русская OEM строка")throw new Exception("OEM866 decoding failed.");var script=FilePath("fixture.cmd");File.WriteAllText(script,"@echo off\r\necho Header\r\necho [==== 10.0%% ====]\r\necho.\r\necho [==== 20.0%% ====]\r\necho Error diagnostic: 25%% complete\r\necho [==== 30.0%% ====]\r\necho Done\r\nexit /b 0\r\n",Encoding.ASCII);if(Run("cmd.exe","/d /c "+Quote(script),"fixture")!=0)return 1;var text=File.ReadAllText(FilePath("fixture.out"));var raw=File.ReadAllText(FilePath("fixture.stdout.raw"));if(text.Contains("10.0%")||!text.Contains("20.0%")||!text.Contains("30.0%")||!text.Contains("Error diagnostic: 25% complete")||!text.Contains("Header")||!text.Contains("Done")||!raw.Contains("10.0%"))throw new Exception("Progress output integration check failed.");var executable=System.Reflection.Assembly.GetExecutingAssembly().Location;if(Run(executable,"emit-encoding-fixture","encoding")!=0)return 1;var encoded=File.ReadAllText(FilePath("encoding.out"));if(!encoded.Contains("Начало проверки")||!encoded.Contains("Проверка 20% завершена.")||encoded.Contains("Проверка 10% завершена.")||!encoded.Contains("повреждённые файлы")||!encoded.Contains("Привет мир ✓")||!encoded.Contains("OEM diagnostic: 25% retained")||encoded.Contains("\u0004"))throw new Exception("Mixed UTF-16 / UTF-8 / OEM pipe check failed.");Console.WriteLine("Progress replaced; Cyrillic mixed encoding and raw binary stdout verified.");return 0;}
  static int DiagnosticSmoke(string folder){Directory.CreateDirectory(folder);Folder=folder;State=new RemoteState{Status="Failed",Ended=DateTime.UtcNow.ToString("o"),StageEnded=DateTime.UtcNow.ToString("o"),Error="original failure"};string ended=State.Ended;string child=FilePath("child.pid").Replace("'","''");CollectDiagnostics("timeout","$child=Start-Process cmd.exe -ArgumentList '/d /c ping 127.0.0.1 -n 90 >nul' -PassThru;$child.Id|Set-Content '"+child+"';while($true){Start-Sleep -Milliseconds 100}",3500);if(State.DiagnosticStatus!="TimedOut"||State.Status!="Failed"||State.Ended!=ended||State.Error!="original failure")throw new Exception("Diagnostic timeout changed main outcome.");if(!File.Exists(FilePath("child.pid")))throw new Exception("Diagnostic child fixture did not start.");if(File.Exists(FilePath("child.pid"))){int pid=int.Parse(File.ReadAllText(FilePath("child.pid")).Trim());try{var p=Process.GetProcessById(pid);if(!p.HasExited)throw new Exception("Diagnostic child survived timeout.");}catch(ArgumentException){}}CollectDiagnostics("success","Write-Output 'diagnostic done'",10000);if(State.DiagnosticStatus!="Completed"||State.Ended!=ended||State.Error!="original failure")throw new Exception("Diagnostic completion changed failure.");Console.WriteLine("Bounded diagnostic group stopped only its child processes; main outcome and duration preserved.");return 0;}
  static int CommandStopSmoke(){foreach(bool ignore in new[]{false,true}){using(var child=new CommandProcess(System.Reflection.Assembly.GetExecutingAssembly().Location,"stop-fixture "+(ignore?"ignore":"exit"))){var reader=new StreamReader(child.Output);if(reader.ReadLine()!="READY")throw new Exception("Console fixture not ready");if(!child.Interrupt())throw new Exception("Ctrl+C delivery failed");if(ignore){if(child.Process.WaitForExit(1000))throw new Exception("Ignoring Ctrl+C fixture exited unexpectedly");child.Force();if(!child.Process.WaitForExit(5000))throw new Exception("Explicit force did not terminate owned process");}else if(!child.Process.WaitForExit(8000))throw new Exception("Ctrl+C did not stop console fixture");}}return 0;}
  static int Main(string[] args){
   if(args.Length==2&&args[0]=="stop-fixture"){Console.CancelKeyPress+=(s,e)=>e.Cancel=args[1]=="ignore";Console.WriteLine("READY");Thread.Sleep(60000);return 0;}
   if(args.Length==1&&args[0]=="command-stop-smoke")return CommandStopSmoke();
   if(args.Length==1&&args[0]=="emit-encoding-fixture"){EmitEncodingFixture();return 0;}
   if(args.Length==2&&args[0]=="update-smoke")return UpdateSmoke(args[1]);
   if(args.Length==2&&args[0]=="cleanup-smoke")return ClientCleanup.Smoke(args[1]);
   if(args.Length==2&&args[0]=="cleanup")return ClientCleanup.Run(args[1]);
   if(args.Length==2&&args[0]=="diagnostic-smoke")return DiagnosticSmoke(args[1]);
   if(args.Length==2&&args[0]=="output-smoke")return VerifyOutput(args[1]);
   if(args.Length!=2)return 2;Folder=Path.GetFullPath(args[1]);if(!Directory.Exists(Folder))return 2;
   try{Job=Json.Deserialize<RemoteJob>(File.ReadAllText(FilePath("job.json")));Guid parsed;if(!Guid.TryParse(Job.Id,out parsed))throw new Exception("Invalid job ID");
    if(args[0]=="restore"||args[0]=="verify"){
     if(File.Exists(FilePath("status.json")))State=Json.Deserialize<RemoteState>(File.ReadAllText(FilePath("status.json")));else State=new RemoteState{Id=Job.Id};
     if(args[0]=="restore")Restore();else Verify();return 0;
    }
    if(args[0]!="run")return 2;
    if(File.Exists(FilePath("status.json"))){var previous=Json.Deserialize<RemoteState>(File.ReadAllText(FilePath("status.json")));if(previous.Status!="Queued")return 0;}
    State=new RemoteState{SupportsWuaCancel=true,SupportsImmediateStop=true,Id=Job.Id,Total=Job.Steps.Count,Started=DateTime.UtcNow.ToString("o"),Status="Queued",Stage="Ожидание других заданий"};Save();
    var diagnostics=new List<string>();using(var mutex=new Mutex(false,ExecutionLane())){
     bool owns=false;try{try{while(!(owns=mutex.WaitOne(500))){if(Cancelled()){State.Status="Cancelled";State.Ended=DateTime.UtcNow.ToString("o");Save();return 0;}}}catch(AbandonedMutexException){owns=true;}
      State.Status="Running";Save();using(var heartbeat=new Timer(o=>{try{if(File.Exists(FilePath("cancel.flag"))&&!File.Exists(FilePath("stop-now.flag"))){State.StopStatus="AfterCurrent";State.StopMessage="Остановка после текущей команды / обновления запрошена";}if(File.Exists(FilePath("stop-now.flag"))&&CurrentKind!="CMD"&&CurrentKind!="PowerShell"){if(State.StopStatus!="AbortRequested"&&State.StopStatus!="ForceRequired"){State.StopStatus="AwaitingSafePoint";State.StopMessage="Остановка запрошена; ожидается безопасная точка встроенной операции";}}Save();}catch{}},null,5000,5000)){
       try{
        if(Cancelled()){State.Status="Cancelled";return 0;}
        using(var legacyGate=new Mutex(false,@"Global\DomainConsoleCSharp.Execution")){bool legacy=false;try{try{legacy=legacyGate.WaitOne(0);}catch(AbandonedMutexException){legacy=true;}if(!legacy)throw new Exception("На клиенте выполняется задание старого исполнителя. Дождитесь его завершения; разделение очередей доступно для новых заданий.");}finally{if(legacy)legacyGate.ReleaseMutex();}}
        if(Job.MicrosoftSource&&Job.Steps.Any(s=>(s.Kind=="Updates"||s.Kind=="UpdateScan")))throw new Exception("Separate Microsoft repair and WSUS update jobs.");
        if(Job.ExclusiveMaintenance||Job.MicrosoftSource){using(var maintenance=new Mutex(false,@"Global\DomainConsoleCSharp.Execution.WSUS")){bool taken=false;try{try{taken=maintenance.WaitOne(0);}catch(AbandonedMutexException){taken=true;}if(!taken)throw new Exception("На клиенте выполняется операция WSUS. Обслуживание не начато; дождитесь её завершения.");if((bool)Com("Microsoft.Update.Installer").IsBusy)throw new Exception("Установщик Windows Update занят. Обслуживание не начато.");}finally{if(taken)maintenance.ReleaseMutex();}}}
        if(Job.MicrosoftSource)EnableMicrosoft();
        foreach(var step in Job.Steps){
         if(Cancelled()){State.Status="Cancelled";break;}
         State.Step++;CurrentKind=step.Kind;Stage(step.Name);var prefix="step-"+State.Step;var stepResult=new StepResult{Name=step.Name,Started=DateTime.UtcNow.ToString("o")};
         try{
          if(step.Kind=="Updates")stepResult.ExitCode=Updates(prefix);
          else if(step.Kind=="UpdateScan")stepResult.ExitCode=Updates(prefix,true);
          else if(step.Kind=="Diagnostics")stepResult.ExitCode=PowerShell(UpdateLogCode(prefix),prefix,300000);
          else if(step.Kind=="CacheCleanup")stepResult.ExitCode=PowerShell(step.Code,prefix,120000);
          else if(step.Kind=="ReportRecovery")stepResult.ExitCode=PowerShell(step.Code,prefix,120000);
          else if(step.Kind=="ClientDiagnostics")stepResult.ExitCode=PowerShell("$dcFolder='"+Folder.Replace("'","''")+"';"+step.Code,prefix,120000);
          else if(step.Kind=="DeliveryDiagnostics"){stepResult.ExitCode=PowerShell("$dcFolder='"+Folder.Replace("'","''")+"';"+step.Code,prefix,300000);if(stepResult.ExitCode==0){Report();PowerShell("Get-Service wuauserv,bits,cryptsvc,UsoSvc | Select-Object Name,Status,StartType | Format-Table | Out-String",prefix+"-after-report");}}
          else if(step.Kind=="PowerShell")stepResult.ExitCode=PowerShell(step.Code,prefix);
          else if(step.Kind=="CMD"){
           var file=FilePath(prefix+".cmd");File.WriteAllText(file,step.Code,Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage));stepResult.ExitCode=Run("cmd.exe","/d /c \"\""+file+"\"\"",prefix);
          }else throw new Exception("Unknown command kind");
          var nativeFile=FilePath(prefix+".native.json");int nativeCode;if(File.Exists(nativeFile)&&int.TryParse(File.ReadAllText(nativeFile),out nativeCode))stepResult.NativeExitCode=nativeCode;
          if(stepResult.ExitCode==3010){State.RebootRequired=true;stepResult.ExitCode=0;}
         }catch(Exception ex){stepResult.ExitCode=1;stepResult.Error=ex.Message+" [HRESULT 0x"+ex.HResult.ToString("X8")+"]";State.Error=stepResult.Error;Log(ex.ToString());}
         if(stepResult.ExitCode!=0&&!Cancelled()){if(string.IsNullOrWhiteSpace(stepResult.Error))stepResult.Error=!string.IsNullOrWhiteSpace(State.Error)&&(step.Kind=="Updates"||step.Kind=="UpdateScan")?State.Error:"Код завершения: "+ExecutionDiagnosis.Code(stepResult.ExitCode)+". "+ExecutionDiagnosis.Explain(stepResult.ExitCode);State.Error=stepResult.Error;ApplyRecommendation(stepResult.Error);Log(stepResult.Error);}
         stepResult.Ended=DateTime.UtcNow.ToString("o");State.StageEnded=stepResult.Ended;lock(Gate){State.Results.Add(stepResult);}Save();
         if(stepResult.ExitCode!=0&&!Job.ContinueOnError&&!Cancelled()){State.Status="Failed";Save();}
         if((step.Kind=="Updates"||step.Kind=="UpdateScan")&&stepResult.ExitCode!=0){diagnostics.Add(prefix);State.DiagnosticStatus="Pending";Save();}
         if(stepResult.ExitCode!=0&&!Job.ContinueOnError)break;
        }
        if(Cancelled())State.Status="Cancelled";
        if(State.Status=="Running")State.Status=State.Results.Any(r=>r.ExitCode!=0)?"CompletedWithErrors":State.RebootRequired?"AwaitingReboot":"Completed";
       }catch(Exception ex){State.Status="Failed";State.Error=ex.Message;Log(ex.ToString());}
       finally{
        if(State.RestoreStatus=="Pending"){try{Restore();}catch(Exception ex){State.RestoreStatus="InterventionRequired";State.Status="InterventionRequired";State.Error=ex.Message;Log(ex.ToString());}}
        State.Ended=DateTime.UtcNow.ToString("o");if(State.Status=="Cancelled"){State.StopStatus="Stopped";State.StopMessage="Задание остановлено администратором";}Save();
        if(State.RebootRequired&&Job.Steps.Any(s=>(s.Kind=="Updates"||s.Kind=="UpdateScan"))){try{StartupTask("Verify","verify");}catch(Exception ex){Log(ex.Message);}}
        if(Job.AutoReboot&&State.RebootRequired&&(State.Status=="Completed"||State.Status=="AwaitingReboot")&&State.RestoreStatus!="InterventionRequired"){
         if(Run("shutdown.exe","/r /t 900 /c \"Domain Console: maintenance completed. Reboot in 15 minutes.\"","reboot")==0){State.Status="RebootScheduled";Save();}
        }
       }
      }
     }finally{if(owns)mutex.ReleaseMutex();}
    }
    foreach(var prefix in diagnostics)CollectUpdateDiagnostics(prefix);
    ClientCleanup.RemoveLaunchTask(Job.Id);
    return 0;
   }catch(Exception ex){try{Log(ex.ToString());}catch{}Console.Error.WriteLine(ex);return 1;}
  }
 }
}

