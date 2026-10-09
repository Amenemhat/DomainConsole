using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class UpdateForcePlan {
 public int MrtPid {get;set;} public int InstallerPid {get;set;}
 public string MrtStarted {get;set;}="";public string InstallerStarted {get;set;}="";
 public string InstallerName {get;set;}="";
 public string Description=>"MRT.exe · PID "+MrtPid+" · "+MrtStarted+"\n"+InstallerName+" · PID "+InstallerPid+" · "+InstallerStarted;
}
public sealed partial class RemoteService {
 public const string ForceUpdateDiscovery="""
$state=Get-Content (Join-Path $p 'status.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($state.Ended){throw 'Задание уже завершено; принудительная остановка не выполнялась.'}
if($state.Stage -notmatch '(?i)KB890830' -or $state.Stage -notmatch 'Установка'){throw 'Текущий этап не является установкой MRT / KB890830. Для других установщиков принудительная остановка пока не предусмотрена.'}
$stage=[DateTime]::Parse($state.StageStarted).ToUniversalTime()
$jobStart=[DateTime]::Parse($state.Started).ToUniversalTime()
$all=@(Get-CimInstance Win32_Process)
$candidates=@($all|Where-Object {$_.Name -ieq 'MRT.exe' -and $_.CreationDate -and $_.CreationDate.ToUniversalTime() -ge $stage.AddSeconds(-15) -and $_.CreationDate.ToUniversalTime() -le $stage.AddMinutes(2)})
if($candidates.Count -ne 1){throw 'Не найден единственный MRT, соответствующий времени текущего этапа. Процессы не завершались; выполните диагностику ожидания.'}
$mrt=$candidates[0]
$parents=@($all|Where-Object {$_.ProcessId -eq $mrt.ParentProcessId})
if($parents.Count -ne 1){throw 'Родитель MRT не найден; безопасно определить установщик нельзя.'}
$installer=$parents[0]
$expectedDir=Join-Path $env:windir 'SoftwareDistribution\Download\Install\'
if($installer.Name -notmatch '^Windows-KB890830[^\\/]*\.exe$' -or !$installer.CommandLine -or $installer.CommandLine.IndexOf($expectedDir,[StringComparison]::OrdinalIgnoreCase) -lt 0 -or !$installer.CreationDate -or $installer.CreationDate.ToUniversalTime() -lt $jobStart -or $installer.CreationDate -gt $mrt.CreationDate){throw 'Связь MRT с установщиком текущего задания не подтверждена. Процессы не завершались.'}
$plan=@{MrtPid=[int]$mrt.ProcessId;InstallerPid=[int]$installer.ProcessId;MrtStarted=$mrt.CreationDate.ToUniversalTime().ToString('o');InstallerStarted=$installer.CreationDate.ToUniversalTime().ToString('o');InstallerName=[string]$installer.Name}
""";
 public const string ForceUpdateApply="""
foreach($key in @('MrtPid','InstallerPid','MrtStarted','InstallerStarted','InstallerName')){if([string]$plan[$key] -cne [string]$expected.$key){throw 'Процессы изменились после подтверждения. Повторите остановку; завершение не выполнялось.'}}
New-Item (Join-Path $p 'cancel.flag') -ItemType File -Force|Out-Null
New-Item (Join-Path $p 'stop-now.flag') -ItemType File -Force|Out-Null
function Audit($text){$line=[DateTime]::UtcNow.ToString('o')+' '+$text;Add-Content (Join-Path $p 'force-update.log') $line -Encoding UTF8;Write-Output $text}
function KillVerified($processId,$started,$name,$parentId){
 $process=Get-Process -Id $processId -ErrorAction SilentlyContinue
 if(!$process){Audit ('Уже завершён: '+$name+' PID '+$processId);return}
 $live=Get-CimInstance Win32_Process -Filter ('ProcessId='+$processId)
 if(!$live -or $live.Name -ine $name -or $live.CreationDate.ToUniversalTime().ToString('o') -cne $started -or ($parentId -and $live.ParentProcessId -ne $parentId)){throw 'PID или связь процессов изменились. Принудительное завершение отказано.'}
 # Process.StartTime is checked on the same process handle used by Kill: PID reuse must not target another process.
 if([Math]::Abs(($process.StartTime.ToUniversalTime()-[DateTime]::Parse($started).ToUniversalTime()).TotalMilliseconds) -gt 1000){throw 'Время запуска процесса изменилось; завершение отказано.'}
 $process.Kill()
 if(!$process.WaitForExit(5000)){throw 'Завершение процесса не подтверждено.'}
 Audit ('Принудительно завершён: '+$name+' PID '+$processId)
}
Audit 'Подтверждено администратором: прерывание MRT и установщика KB890830; следующие шаги отменены.'
try{
 KillVerified $plan.MrtPid $plan.MrtStarted 'MRT.exe' $plan.InstallerPid
 Start-Sleep -Seconds 2
 KillVerified $plan.InstallerPid $plan.InstallerStarted $plan.InstallerName 0
 Audit 'Процессы MRT остановлены. Итог WUA ещё не подтверждён; ожидайте обновления статуса задания. Если WUA не возвращается, выполните диагностику ожидания; может потребоваться плановая перезагрузка.'
}catch{Audit ('Ошибка прерывания: '+$_.Exception.Message);throw}
""";
 public async Task<UpdateForcePlan> InspectUpdateForce(TargetRecord target,string id){
  if(!Guid.TryParse(id,out _))throw new ArgumentException("Некорректное задание.");if(IsLocal(target.Host))RequireLocalAdmin();
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
  var json=await PowerShellBridge.Execute(Invoke(target.Host,"$p=Join-Path (Join-Path $env:ProgramData 'DomainConsoleCSharp') "+PowerShellBridge.Literal(id)+";"+ForceUpdateDiscovery+"\n$plan|ConvertTo-Json -Compress"),timeout.Token);
  return JsonSerializer.Deserialize<UpdateForcePlan>(json,Store.Options)??throw new Exception("Не получен план остановки.");
 }
 public async Task<string> ForceUpdate(TargetRecord target,string id,UpdateForcePlan expected){
  if(!Guid.TryParse(id,out _))throw new ArgumentException("Некорректное задание.");if(IsLocal(target.Host))RequireLocalAdmin();
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
  var body="$p=Join-Path (Join-Path $env:ProgramData 'DomainConsoleCSharp') "+PowerShellBridge.Literal(id)+";"+ForceUpdateDiscovery+"\n$expected="+PowerShellBridge.Literal(JsonSerializer.Serialize(expected))+"|ConvertFrom-Json;"+ForceUpdateApply;
  return await PowerShellBridge.Execute(Invoke(target.Host,body),timeout.Token);
 }
}
public partial class MainWindow {
 async Task ForceWsus(ComputerRow row){
  var plan=await remote.InspectUpdateForce(row.Target,row.JobId);
  if(MessageBox.Show(this,"Компьютер: "+row.Host+"\n\n"+plan.Description+"\n\nЗавершить эти два процесса принудительно? Установка KB890830 может завершиться ошибкой. Следующие шаги задания будут отменены. Службы Windows Update и другие установщики не завершаются.","Подтверждение принудительного прерывания",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  var result=await remote.ForceUpdate(row.Target,row.JobId,plan);Notify(result);MessageBox.Show(this,result,"Результат прерывания",MessageBoxButton.OK,MessageBoxImage.Information);
 }
}
