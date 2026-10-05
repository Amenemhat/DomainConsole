using System.IO;
using System.Diagnostics;
using System.DirectoryServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DomainConsole.Shared;
namespace DomainConsole;
public static class Store {
 public static readonly string Root=Path.Combine(AppContext.BaseDirectory,"Data");
 public static readonly JsonSerializerOptions Options=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
 static Store(){Directory.CreateDirectory(Root);}
 public static T Read<T>(string file,T fallback)=>File.Exists(Path.Combine(Root,file))?JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Root,file)),Options)??fallback:fallback;
 public static void Write<T>(string file,T value){var path=Path.Combine(Root,file);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,Options),Encoding.UTF8);File.Move(path+".tmp",path,true);}
 public static T Clone<T>(T item)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(item,Options),Options)!;
}
public static class PowerShellBridge {
 public static string PlainError(string value){
  int start=value.IndexOf("<Objs",StringComparison.Ordinal);if(start<0)return value.Trim();
  try{var xml=XDocument.Parse(value[start..]);var lines=xml.Descendants().Where(x=>x.Name.LocalName=="S"&&(string?)x.Attribute("S")=="Error").Select(x=>Regex.Replace(x.Value,@"_x([0-9a-fA-F]{4})_",m=>((char)Convert.ToInt32(m.Groups[1].Value,16)).ToString()));return (value[..start].Replace("#< CLIXML","").Trim()+"\n"+string.Join("",lines)).Trim();}catch{return value.Trim();}
 }
 public static string Literal(string value)=>"'"+value.Replace("'","''")+"'";
 public static async Task<string> Execute(string code,CancellationToken token=default){
  var exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
  var wrapped="$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false);try{\n"+code+"\n}catch{[Console]::Error.WriteLine($_.Exception.Message);exit 1}";
  var start=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
  start.ArgumentList.Add("-NoLogo");start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-NonInteractive");start.ArgumentList.Add("-EncodedCommand");start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped)));
  using var p=Process.Start(start)??throw new Exception("Не удалось запустить встроенный PowerShell.");
  var stdout=p.StandardOutput.ReadToEndAsync(token);var stderr=p.StandardError.ReadToEndAsync(token);
  await p.WaitForExitAsync(token);var output=await stdout;var error=await stderr;
  if(p.ExitCode!=0)throw new Exception(string.IsNullOrWhiteSpace(error)?"PowerShell exit "+p.ExitCode:PlainError(error));return output.Trim();
 }
}
public sealed class PollResult {public string TaskState {get;set;}="";public long TaskResult {get;set;}public bool AgentAlive {get;set;}public RemoteState? State {get;set;}public string Output {get;set;}="";}
public sealed class WsusSettings {public string Host {get;set;}="";public int Port {get;set;}=8530;public bool Ssl {get;set;}}
public sealed class RemoteService {
 public Task<string> CheckConnection(TargetRecord target)=>PowerShellBridge.Execute(Invoke(target.Host,"'Подключение WinRM успешно.';whoami;hostname"));
 string Invoke(string host,string body)=>"Invoke-Command -ComputerName "+PowerShellBridge.Literal(host)+" -SessionOption (New-PSSessionOption -OpenTimeout 12000 -OperationTimeout 30000) -ScriptBlock { "+body+" } -ErrorAction Stop";
 public async Task Deploy(TargetRecord target,RemoteJob job){
  var agent=Path.Combine(AppContext.BaseDirectory,"Agent","DomainConsole.Agent.exe");
  if(!File.Exists(agent))throw new FileNotFoundException("Отсутствует C#-исполнитель Agent\\DomainConsole.Agent.exe. Используйте полный опубликованный комплект.");
  var local=Path.Combine(Store.Root,job.Id+".job.json");File.WriteAllText(local,JsonSerializer.Serialize(job,Store.Options),Encoding.UTF8);
  var id=PowerShellBridge.Literal(job.Id);
  var command="$s=New-PSSession -ComputerName "+PowerShellBridge.Literal(target.Host)+" -SessionOption (New-PSSessionOption -OpenTimeout 15000) -ErrorAction Stop;try{\n"+
   "$path=Invoke-Command $s {param($id);$root=Join-Path $env:ProgramData 'DomainConsoleCSharp';New-Item $root -ItemType Directory -Force|Out-Null;"+
   "$acl=New-Object Security.AccessControl.DirectorySecurity;$acl.SetAccessRuleProtection($true,$false);foreach($sid in @('S-1-5-18','S-1-5-32-544')){$rule=New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier($sid)),'FullControl','ContainerInherit,ObjectInherit','None','Allow');$acl.AddAccessRule($rule)};Set-Acl $root $acl;"+
   "$path=Join-Path $root $id;New-Item $path -ItemType Directory -Force|Out-Null;$path} -ArgumentList "+id+";\n"+
   "$exists=Invoke-Command $s {param($path);Test-Path (Join-Path $path 'submitted.flag')} -ArgumentList $path;\n"+
   "if(!$exists){Copy-Item "+PowerShellBridge.Literal(agent)+" -Destination ($path+'\\DomainConsole.Agent.exe') -ToSession $s -Force;Copy-Item "+PowerShellBridge.Literal(local)+" -Destination ($path+'\\job.json') -ToSession $s -Force;"+
   "Invoke-Command $s {param($path,$id);$action=New-ScheduledTaskAction -Execute (Join-Path $path 'DomainConsole.Agent.exe') -Argument ('run \"'+$path+'\"');$principal=New-ScheduledTaskPrincipal -UserId SYSTEM -LogonType ServiceAccount -RunLevel Highest;"+
   "$settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries;Register-ScheduledTask -TaskName ('DomainConsoleCSharp-'+$id) -Action $action -Principal $principal -Settings $settings -Force|Out-Null;"+
   "New-Item (Join-Path $path 'submitted.flag') -ItemType File -Force|Out-Null;Start-ScheduledTask ('DomainConsoleCSharp-'+$id)} -ArgumentList $path,"+id+"}}finally{Remove-PSSession $s}";
  await PowerShellBridge.Execute(command);
 }
 public async Task<PollResult> Poll(TargetRecord target,string id){
  if(!Guid.TryParse(id,out _))throw new ArgumentException("Некорректный ID задания.");
  var output=await PowerShellBridge.Execute(Invoke(target.Host,"$p=Join-Path (Join-Path $env:ProgramData 'DomainConsoleCSharp') "+PowerShellBridge.Literal(id)+";$state=$null;if(Test-Path ($p+'\\status.json')){$state=Get-Content ($p+'\\status.json') -Raw|ConvertFrom-Json};$text='';Get-ChildItem $p -File|Where-Object {$_.Name -match '\\.(log|out|err)$|updates.json$|post-reboot.json$'}|Sort-Object Name|ForEach-Object {$text+=('--- '+$_.Name+' ---'+[Environment]::NewLine+(Get-Content $_.FullName -Tail 300|Out-String))};$task=Get-ScheduledTask -TaskName ('DomainConsoleCSharp-'+"+PowerShellBridge.Literal(id)+") -ErrorAction SilentlyContinue;$taskState='Unknown';$taskResult=0;if($task){$taskState=[string]$task.State;$taskResult=(Get-ScheduledTaskInfo $task).LastTaskResult};$alive=@(Get-CimInstance Win32_Process -Filter \"Name='DomainConsole.Agent.exe'\"|Where-Object {$_.ExecutablePath -eq ($p+'\\DomainConsole.Agent.exe')}).Count -gt 0;@{State=$state;Output=$text;TaskState=$taskState;TaskResult=$taskResult;AgentAlive=$alive}|ConvertTo-Json -Depth 25 -Compress"));
  return JsonSerializer.Deserialize<PollResult>(output,Store.Options)??new();
 }
 public Task<string> Cancel(TargetRecord target,string id)=>PowerShellBridge.Execute(Invoke(target.Host,"$p=Join-Path (Join-Path $env:ProgramData 'DomainConsoleCSharp') "+PowerShellBridge.Literal(id)+";if(!(Test-Path $p)){throw 'Задание не найдено'};New-Item ($p+'\\cancel.flag') -ItemType File -Force|Out-Null;'Остановка запрошена после текущего шага.'"));
 public Task<string> AbortReboot(TargetRecord target)=>PowerShellBridge.Execute(Invoke(target.Host,"shutdown.exe /a;if($LASTEXITCODE -ne 0){throw ('shutdown exit '+$LASTEXITCODE)};'Перезагрузка отменена.'"));
 public Task<string> Logs(TargetRecord target,string id){
  var local=Path.Combine(Store.Root,"Logs",target.Host+"_"+id);Directory.CreateDirectory(local);
  return PowerShellBridge.Execute("$s=New-PSSession -ComputerName "+PowerShellBridge.Literal(target.Host)+" -ErrorAction Stop;try{$p=Invoke-Command $s {Join-Path (Join-Path $env:ProgramData 'DomainConsoleCSharp') "+PowerShellBridge.Literal(id)+"};Copy-Item ($p+'\\*') -Destination "+PowerShellBridge.Literal(local)+" -FromSession $s -Recurse -Force;"+PowerShellBridge.Literal(local)+"}finally{Remove-PSSession $s}");
 }
 public Task<string> ServicingDiagnostics(TargetRecord target,string id)=>PowerShellBridge.Execute(Invoke(target.Host,
  "'Время проверки (UTC): '+[DateTime]::UtcNow.ToString('o');'\nПроцессы: CPU — накопленное процессорное время, сравните два снимка';$processes=@(Get-Process dism,sfc,TiWorker,TrustedInstaller -ErrorAction SilentlyContinue);if(!$processes.Count){'На момент снимка DISM/SFC/TiWorker/TrustedInstaller не найдены.'};$processes|Select-Object Name,Id,CPU,StartTime,Responding|Format-Table -AutoSize|Out-String;"+
  "'Задание: ';"+PowerShellBridge.Literal(id)+";$jobPath=Join-Path (Join-Path $env:ProgramData 'DomainConsoleCSharp') "+PowerShellBridge.Literal(id)+";if(Test-Path ($jobPath+'\\status.json')){'Состояние задания:';$state=Get-Content ($jobPath+'\\status.json') -Encoding UTF8 -Raw|ConvertFrom-Json;'Статус: '+$state.Status+'; этап: '+$state.Stage;'Обновлён (UTC): '+$state.Updated+'; завершён (UTC): '+$state.Ended;'Ошибка: '+$state.Error;$state.Results|Select-Object Name,ExitCode,Error,Ended|Format-Table -Wrap|Out-String;'Полное состояние:';$state|ConvertTo-Json -Depth 20};"+
  "foreach($log in @((Join-Path $env:windir 'Logs\\DISM\\dism.log'),(Join-Path $env:windir 'Logs\\CBS\\CBS.log'))){if(Test-Path $log){$file=Get-Item $log;'\n--- '+$log+' ---';'Изменён (UTC): '+$file.LastWriteTimeUtc.ToString('o');'Размер: '+$file.Length;'Возраст журнала: '+[math]::Round(([DateTime]::UtcNow-$file.LastWriteTimeUtc).TotalMinutes,1)+' мин. Это системный журнал: последние записи могут относиться к другим заданиям.';$tail=@(Get-Content $log -Tail 80);'Ошибки в последних 80 строках:';$hits=@($tail|Where-Object {$_ -match '(?i)error|failed|0x800'});if($hits.Count){$hits|Select-Object -Last 10|Out-String}else{'Явных строк ошибки в этом фрагменте нет; успешность задания этим не подтверждается.'};'Последние 80 строк:';$tail|Out-String}else{'Не найден: '+$log}}"));
 public Task<string> Wsus(string host,int port,bool ssl,string computer)=>PowerShellBridge.Execute(Invoke(host,
  "[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,"+(ssl?"$true":"$false")+","+port+");$scope=New-Object Microsoft.UpdateServices.Administration.ComputerTargetScope;$scope.NameIncludes="+PowerShellBridge.Literal(computer.Split('.')[0])+";$targets=@($s.GetComputerTargets($scope)|Where-Object {$_.FullDomainName -eq "+PowerShellBridge.Literal(computer)+" -or $_.FullDomainName.Split('.')[0] -eq "+PowerShellBridge.Literal(computer.Split('.')[0])+"});if(!$targets.Count){throw 'Компьютер не найден в WSUS'};if($targets.Count -ne 1){throw 'Найдено несколько клиентов; уточните полное доменное имя'};$t=$targets[0];$text=($t|Select-Object FullDomainName,LastReportedStatusTime,LastSyncTime,LastSyncResult|Format-List|Out-String)+($t.GetUpdateInstallationSummary()|Format-List *|Out-String);@{LastReportedUtc=$t.LastReportedStatusTime.ToUniversalTime().ToString('o');Details=$text}|ConvertTo-Json -Compress"));
}
public static class DirectoryReader {
 public static List<TargetRecord> Read(){
  using var root=new DirectoryEntry("LDAP://RootDSE");var naming=(string?)root.Properties["defaultNamingContext"].Value??throw new Exception("Домен недоступен.");
  using var entry=new DirectoryEntry("LDAP://"+naming);using var search=new DirectorySearcher(entry){Filter="(&(objectCategory=computer)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))",PageSize=500};
  search.PropertiesToLoad.AddRange(new[]{"name","dnshostname","distinguishedname","operatingsystem"});using var results=search.FindAll();var list=new List<TargetRecord>();
  foreach(SearchResult r in results){string Get(string n)=>r.Properties[n].Count>0?r.Properties[n][0]?.ToString()??"":"";var name=Get("name");list.Add(new(){Name=name,Host=string.IsNullOrEmpty(Get("dnshostname"))?name:Get("dnshostname"),OU=Regex.Replace(Get("distinguishedname"),@"^CN=(?:\\.|[^,])+,",""),OS=Get("operatingsystem"),Status="Не проверен"});}return list.OrderBy(x=>x.Name).ToList();
 }
}
