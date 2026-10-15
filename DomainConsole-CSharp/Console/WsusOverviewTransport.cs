using System.Text.Json;
namespace DomainConsole;
public sealed partial class RemoteService {
 // One authenticated remote call per batch; the same WSUS object serves every target in it.
 public async Task<List<OverviewReply>> WsusOverview(WsusSettings settings,List<string> hosts,CancellationToken token){
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(90));
  string body=OverviewScript(settings,hosts);
  var json=await PowerShellBridge.Execute(Invoke(settings.Host,body),timeout.Token);
  return JsonSerializer.Deserialize<List<OverviewReply>>(json,Store.Options)??throw new Exception("Пустой ответ обзора WSUS");
 }
 public static string OverviewScript(WsusSettings settings,List<string> hosts)=>
  "$wanted=@("+string.Join(",",hosts.Select(PowerShellBridge.Literal))+");$port="+settings.Port+";$ssl="+(settings.Ssl?"$true":"$false")+";"+OverviewBody;
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
   $reply.Report=@{FullDomainName=$t.FullDomainName;LastReportedUtc=$t.LastReportedStatusTime.ToUniversalTime().ToString('o');LastSyncUtc=$t.LastSyncTime.ToUniversalTime().ToString('o');LastSyncResult=[string]$t.LastSyncResult;UnknownCount=$summary.UnknownCount;NotApplicableCount=$summary.NotApplicableCount;NotInstalledCount=$summary.NotInstalledCount;DownloadedCount=$summary.DownloadedCount;InstalledCount=$summary.InstalledCount;InstalledPendingRebootCount=$summary.InstalledPendingRebootCount;FailedCount=$summary.FailedCount}
  }
 }catch{$reply.Error=$_.Exception.Message}
 $reply
})
ConvertTo-Json -InputObject @($rows) -Depth 6 -Compress
""";
}
