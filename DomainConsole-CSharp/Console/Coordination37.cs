using DomainConsole.Shared;
using System.Text;
using System.Text.Json;
using System.Windows;
namespace DomainConsole;
public sealed partial class RemoteService {
 static readonly string installation37=Store.Read("console-id37.json","") is {Length:>0} saved?saved:NewConsole37();
 static string NewConsole37(){var id=Guid.NewGuid().ToString();Store.Write("console-id37.json",id);return id;}
 public static string ConsoleId37=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(installation37+"|"+Environment.MachineName+"|"+Environment.UserDomainName+"|"+Environment.UserName)));
 public static void StampConsole37(RemoteJob job){if(job.ConsoleId37.Length>0){if(job.ConsoleId37!=ConsoleId37)throw new InvalidOperationException("Задание создано другой консолью; доступен просмотр. Для нового запуска создайте отдельное задание.");return;}job.ConsoleId37=ConsoleId37;job.ConsoleLabel37=Environment.MachineName;}
 public static string GuardControlScript37(string body)=>"$dcGate37=New-Object Threading.Mutex($false,'Global\\DomainConsoleCSharp.Coordination37');$dcOwns37=$false;try{try{$dcOwns37=$dcGate37.WaitOne(10000)}catch [Threading.AbandonedMutexException]{$dcOwns37=$true};if(!$dcOwns37){throw 'Проверка владельца занята; действие не выполнялось'};$dcControlFile37=Join-Path $env:ProgramData 'DomainConsoleCSharp\\control37.json';if(Test-Path -LiteralPath $dcControlFile37){$dcOwner37=Get-Content -LiteralPath $dcControlFile37 -Raw -Encoding UTF8 -ErrorAction Stop|ConvertFrom-Json -ErrorAction Stop;if($dcOwner37.OwnerId -and $dcOwner37.OwnerId -ne "+PowerShellBridge.Literal(ConsoleId37)+"){throw 'Управление принадлежит другой консоли; действие не выполнялось'}};\n"+body+"\n}finally{if($dcOwns37){$dcGate37.ReleaseMutex()};$dcGate37.Dispose()}";
 public async Task<bool> Control37(TargetRecord target,string operation,string plan=""){
  if(operation is not ("release37" or "claim37"))throw new ArgumentException("Unknown control operation");
  var request=new RemoteJob{PlanId37=plan};StampConsole37(request);var payload=JsonSerializer.Serialize(request,Store.Options);
  string agent=System.IO.Path.Combine(AppContext.BaseDirectory,"Agent","DomainConsole.Agent.exe");string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(agent))).ToLowerInvariant();
  string code="$root=Join-Path $env:ProgramData 'DomainConsoleCSharp';$exe=Join-Path $root 'Agent\\"+hash+"\\DomainConsole.Agent.exe';if(!(Test-Path -LiteralPath $exe)){if("+(operation=="release37"?"$true":"$false")+"){return};throw 'На клиенте ещё нет агента 0.38 этой сборки. Сначала передайте диагностическое задание.'};$p=Join-Path $root "+PowerShellBridge.Literal(request.Id)+";New-Item $p -ItemType Directory -Force|Out-Null;try{[IO.File]::WriteAllText((Join-Path $p 'job.json'),"+PowerShellBridge.Literal(payload)+",(New-Object Text.UTF8Encoding($false)));$result=& $exe "+(operation=="claim37"?"claim38":operation)+" $p 2>&1;if($LASTEXITCODE -ne 0){throw 'Проверка управления завершилась без результата'};if(Test-Path (Join-Path $p 'admission38.json')){$admit=Get-Content (Join-Path $p 'admission38.json') -Raw -Encoding UTF8|ConvertFrom-Json;if(!$admit.Allowed){throw ('DC_ADMISSION38|'+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((Get-Content (Join-Path $p 'admission38.json') -Raw -Encoding UTF8))))}}}finally{Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue};$result";
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));try{var reply=await PowerShellBridge.Execute(Invoke(target,code),timeout.Token);return reply.Contains("released37",StringComparison.Ordinal);}catch(Exception ex) when(ex.Message.Contains("DC_ADMISSION38|",StringComparison.Ordinal)){throw new InvalidOperationException(MainWindow.ParseAdmission38(ex.Message).Message,ex);}
 }
 public const string ControlProbe37="""
$dcControl37=$null;$dcExternal37=@();$dcControlRead37=$false
try{
 $controlFile=Join-Path $env:ProgramData 'DomainConsoleCSharp\control37.json'
 if(Test-Path -LiteralPath $controlFile){$dcControl37=Get-Content -LiteralPath $controlFile -Raw -Encoding UTF8 -ErrorAction Stop|ConvertFrom-Json -ErrorAction Stop
  foreach($jobId in @($dcControl37.JobIds|Select-Object -Last 16)){
   $parsed=[Guid]::Empty;if(![Guid]::TryParse([string]$jobId,[ref]$parsed)){continue}
   $stateFile=Join-Path $env:ProgramData ('DomainConsoleCSharp\'+$jobId+'\status.json')
   if(Test-Path -LiteralPath $stateFile){try{$st=Get-Content -LiteralPath $stateFile -Raw -Encoding UTF8 -ErrorAction Stop|ConvertFrom-Json -ErrorAction Stop;$dcExternal37+=@{Id=$jobId;Status=$st.Status;Stage=$st.Stage;Updated=$st.Updated;Ended=$st.Ended;Progress=$st.Progress;StopMessage=$st.StopMessage}}catch{}}
  }
 }
 $dcControlRead37=$true
}catch{}
""";
}
public partial class MainWindow {
 bool ForeignControl37(string host)=>rebootSnapshots.TryGetValue(host,out var snap)&&snap.Control37 is {OwnerId.Length:>0} control&&control.OwnerId!=RemoteService.ConsoleId37&&(control.PlanId.Length>0||snap.ExternalJobs37.Any(x=>x.Ended.Length==0));
 string ControlText37(string host){if(!rebootSnapshots.TryGetValue(host,out var snap)||snap.Control37 is not {OwnerId.Length:>0} c)return "";if(c.PlanId.Length==0&&snap.ExternalJobs37.Count>0&&snap.ExternalJobs37.All(x=>x.Ended.Length>0))return "";return c.OwnerId==RemoteService.ConsoleId37?"Управление: эта консоль":"Управление: "+c.OwnerLabel;}
 void RefreshControl37(ComputerRow row){HeaderControl37.Text=ControlText37(row.Host);HeaderControl37.ToolTip=rebootSnapshots.TryGetValue(row.Host,out var snap)&&snap.Control37!=null?"План: "+snap.Control37.Operation+"\n"+string.Join("\n",snap.ExternalJobs37.Select(x=>ComputerRow.StatusText(x.Status)+" · "+x.Stage+" · "+Display36.Date(x.Updated,true)))+"\nПередача управления доступна в меню подключения после подтверждённого завершения заданий":"";}
 async void ClaimControl37_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{var row=Current();if(MessageBox.Show(this,"Принять управление клиентом "+row.Host+"?\nПередача разрешается только после подтверждённого завершения всех заданий. Продолжения прежнего плана будут отклонены клиентом.","Передача управления",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;await remote.Control37(row.Target,"claim37");await ReadClientStatus(row,force:true);RefreshOutcomes();ShowCurrent();Notify("Управление передано этой консоли");});
 async Task ReleaseFinishedPlans37(){if(TransportBudget41.Paused)return;using var read43=PollingControl43.Read();foreach(var p in maintenancePlans.Values.Where(p=>p.Done&&!p.Released37&&p.PlanId37.Length>0).ToList()){var row=Computers.FirstOrDefault(r=>r.Host.Equals(p.Host,StringComparison.OrdinalIgnoreCase));if(row==null)continue;try{if(await remote.Control37(ConnectionStore.Capture(row.Target),"release37",p.PlanId37)){p.Released37=true;SaveMaintenance();}}catch(Exception ex){statusLimitations[p.Host]="Освобождение плана не подтверждено: "+ConnectionBrief31(ex.Message);}}}
}
