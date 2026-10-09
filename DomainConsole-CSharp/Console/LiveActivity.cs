using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class LiveUpdateActivity {public bool Busy {get;set;}public List<string> Jobs {get;set;}=new();}
public sealed partial class RemoteService {
 public const string LiveUpdateScript="""
$root=Join-Path $env:ProgramData 'DomainConsoleCSharp'
$jobs=@()
foreach($proc in @(Get-CimInstance Win32_Process -Filter "Name='DomainConsole.Agent.exe'")){
 if(!$proc.CommandLine -or $proc.CommandLine -notmatch '\brun\s+"([^\"]+)"'){continue}
 $folder=$matches[1];$id=Split-Path $folder -Leaf
 $guid=[Guid]::Empty;if(![Guid]::TryParse($id,[ref]$guid) -or !(Test-Path (Join-Path $folder 'job.json'))){continue}
 $job=Get-Content (Join-Path $folder 'job.json') -Raw -Encoding UTF8|ConvertFrom-Json
 $state=$null;if(Test-Path (Join-Path $folder 'status.json')){$state=Get-Content (Join-Path $folder 'status.json') -Raw -Encoding UTF8|ConvertFrom-Json}
 if($job.OutputChannel -eq 'WSUS' -and (!$state -or !$state.Ended -or $state.DiagnosticStatus -in @('Pending','Running','LegacyRunning'))){$jobs+= $id}
}
$busy=[bool](New-Object -ComObject Microsoft.Update.Installer).IsBusy
@{Busy=$busy;Jobs=@($jobs)}|ConvertTo-Json -Compress
""";
 public async Task<LiveUpdateActivity> LiveUpdates(TargetRecord target){if(IsLocal(target.Host))RequireLocalAdmin();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40));return JsonSerializer.Deserialize<LiveUpdateActivity>(await PowerShellBridge.Execute(Invoke(target.Host,LiveUpdateScript),timeout.Token),Store.Options)??throw new Exception("Не получено состояние WSUS.");}
}
public partial class MainWindow {
 async Task<bool> HasLiveWsus(List<ComputerRow> selected){bool active=false;
  foreach(var row in selected){var live=await remote.LiveUpdates(row.Target);active|=live.Busy||live.Jobs.Count>0||queue.Any(q=>q.Target.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)&&IsWsusJob(q.Job));
   foreach(var job in History.Where(IsWsusJob)){var target=job.Targets.FirstOrDefault(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));if(target==null||target.Status is not ("Queued" or "Submitted" or "Running")||live.Jobs.Contains(job.Id)||queue.Any(q=>q.Job.Id==job.Id&&q.Target.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)))continue;
    // Read the specific task before reconciling: a scheduled executor may not have appeared in the process list yet.
    var data=await remote.Poll(target,job.Id);if(data.AgentAlive||data.TaskState=="Running"){active=true;continue;}
    if(data.State!=null&&!string.IsNullOrEmpty(data.State.Ended)){target.State=data.State;target.Status=data.State.Status;}
    else {target.Status="InterventionRequired";target.State??=new();target.State.Status="InterventionRequired";target.State.Error="Исполнитель отсутствует; сохранённый активный статус устарел. Исход задания неизвестен. Очистка проверяется отдельно через WUA.";}
    Sync(job);
   }
  }
  Save();return active;
 }
}
