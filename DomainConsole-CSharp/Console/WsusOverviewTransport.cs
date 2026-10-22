using System.Text.Json;
namespace DomainConsole;
public sealed partial class RemoteService {
 public async Task RequestRebootCancellation(DomainConsole.Shared.TargetRecord target,string id){
  if(!Guid.TryParse(id,out _))throw new ArgumentException("Некорректное задание");
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
  string body="$p=Join-Path (Join-Path $env:ProgramData 'DomainConsoleCSharp') "+PowerShellBridge.Literal(id)+";$job=Get-Content (Join-Path $p 'job.json') -Raw -Encoding UTF8|ConvertFrom-Json;if(!$job.IndependentControl -or $job.Name -ne 'Перезагрузка текущего компьютера'){throw 'Это не задание перезагрузки из шапки'};New-Item (Join-Path $p 'cancel.flag') -ItemType File -Force|Out-Null;New-Item (Join-Path $p 'reboot-cancel.flag') -ItemType File -Force|Out-Null;";
  await PowerShellBridge.Execute(Invoke(target,body),timeout.Token);
 }
 // One authenticated remote call per batch; the same WSUS object serves every target in it.
 public async Task<List<OverviewReply>> WsusOverview(WsusSettings settings,List<string> hosts,CancellationToken token){
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(90));
  var names=hosts.ToDictionary(h=>h,ConnectionStore.WsusName,StringComparer.OrdinalIgnoreCase);string body=OverviewScript(settings,names.Values.Where(n=>n.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
  var json=await PowerShellBridge.Execute(Invoke(settings.Host,body),timeout.Token);
  var replies=JsonSerializer.Deserialize<List<OverviewReply>>(json,Store.Options)??throw new Exception("Пустой ответ обзора WSUS");return hosts.Select(h=>{if(names[h].Length==0)return new OverviewReply{Host=h,Error="Имя клиента ещё не подтверждено для WSUS"};var found=replies.FirstOrDefault(r=>r.Host.Equals(names[h],StringComparison.OrdinalIgnoreCase));if(found==null)return new OverviewReply{Host=h,Error="Нет ответа WSUS для клиента"};var copy=Store.Clone(found);copy.Host=h;return copy;}).ToList();
 }
 public static string OverviewScript(WsusSettings settings,List<string> hosts)=>
  "$wanted=@("+string.Join(",",hosts.Select(PowerShellBridge.Literal))+");$port="+settings.Port+";$ssl="+(settings.Ssl?"$true":"$false")+";"+RequiredUpdatesScript31+OverviewBody;
 public const string OverviewBody="""
[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration')
$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,$ssl,$port)
$known=@($s.GetComputerTargets((New-Object Microsoft.UpdateServices.Administration.ComputerTargetScope)))
$rows=@(foreach($hostName in $wanted){
 $reply=@{Host=$hostName;Found=$false;Report=$null;Error=''}
 try {
  $targets=@($known|Where-Object {$_.FullDomainName.TrimEnd('.') -eq $hostName.TrimEnd('.')})
  if($targets.Count -eq 0 -and $hostName -notmatch '\.'){$targets=@($known|Where-Object {$_.FullDomainName.Split('.')[0] -eq $hostName})}
  if($targets.Count -gt 1){throw 'Неоднозначная идентичность клиента в WSUS'}
  if($targets.Count -eq 1){
   $t=$targets[0];$reply.Found=$true;$summary=$t.GetUpdateInstallationSummary()
   $need31=Read-RequiredUpdates31 $t
   $reply.Report=@{RequiredUpdates=@($need31.RequiredUpdates);RequiredListComplete=$need31.RequiredListComplete;RequiredListError=$need31.RequiredListError;NeededCount=$need31.NeededCount;ApprovedUnknownCount=$need31.ApprovedUnknownCount;ApprovedDownloadedCount=$need31.ApprovedDownloadedCount;ApprovedFailedCount=$need31.ApprovedFailedCount;RequiredComplete=$need31.RequiredComplete;RequiredError=$need31.RequiredError;FullDomainName=$t.FullDomainName;LastReportedUtc=$t.LastReportedStatusTime.ToUniversalTime().ToString('o');LastSyncUtc=$t.LastSyncTime.ToUniversalTime().ToString('o');LastSyncResult=[string]$t.LastSyncResult;UnknownCount=$summary.UnknownCount;NotApplicableCount=$summary.NotApplicableCount;NotInstalledCount=$summary.NotInstalledCount;DownloadedCount=$summary.DownloadedCount;InstalledCount=$summary.InstalledCount;InstalledPendingRebootCount=$summary.InstalledPendingRebootCount;FailedCount=$summary.FailedCount}
  }
 }catch{$reply.Error=$_.Exception.Message}
 $reply
})
ConvertTo-Json -InputObject @($rows) -Depth 6 -Compress
""";
}

