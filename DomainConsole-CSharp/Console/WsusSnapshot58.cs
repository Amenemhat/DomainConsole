using System.Text.Json;
namespace DomainConsole;

// Server observations are deliberately separate from endpoint reachability and job results.
public sealed class WsusServerRecord58 {
 public string Id {get;set;}="";
 public string Name {get;set;}="";
 public string Address {get;set;}="";
 public string OS {get;set;}="";
 public WsusReport? Summary {get;set;}
 public string Error {get;set;}="";
}
public sealed class WsusServerSnapshot58 {
 public string Server {get;set;}="";
 public string CollectedUtc {get;set;}="";
 public string ReceivedUtc {get;set;}="";
 public bool Complete {get;set;}
 public List<WsusServerRecord58> Records {get;set;}=new();
 public string LastError {get;set;}="";
 public string ErrorUtc {get;set;}="";
 public List<WsusServerRecord58> Find(string name) {
  string key=name.Trim().TrimEnd('.');
  if(key.Length==0)return new();
  var exact=Records.Where(r=>r.Name.TrimEnd('.').Equals(key,StringComparison.OrdinalIgnoreCase)).ToList();
  return exact.Count>0||key.Contains('.')?exact:Records.Where(r=>r.Name.Split('.')[0].Equals(key,StringComparison.OrdinalIgnoreCase)).ToList();
 }
}
public sealed partial class RemoteService {
 public async Task<WsusServerSnapshot58> ReadWsusSnapshot58(WsusSettings settings,CancellationToken token) {
  using var read=PollingControl43.Read();
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
  timeout.CancelAfter(TimeSpan.FromMinutes(3));
  var script="$port="+settings.Port+";$ssl="+(settings.Ssl?"$true":"$false")+";"+SnapshotScript58;
  var json=await PowerShellBridge.Execute(Invoke(settings.Host,script),timeout.Token);
  var result=JsonSerializer.Deserialize<WsusServerSnapshot58>(json,Store.Options)??throw new Exception("Пустой серверный снимок WSUS");
  if(!result.Complete||result.CollectedUtc.Length==0)throw new Exception("Серверный снимок WSUS неполон; прежние данные сохранены");
  result.Server=settings.Host+":"+settings.Port+(settings.Ssl?"|https":"|http");
  result.ReceivedUtc=DateTimeOffset.UtcNow.ToString("o");return result;
 }
 public const string SnapshotScript58="""
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration')
$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,$ssl,$port)
$scope=New-Object Microsoft.UpdateServices.Administration.ComputerTargetScope
$known=@($s.GetComputerTargets($scope))
$summaryById=@{}
foreach($summary in $s.GetSummariesPerComputerTarget((New-Object Microsoft.UpdateServices.Administration.UpdateScope),$scope)){$summaryById[[string]$summary.ComputerTargetId]=$summary}
$records=@(foreach($t in $known){
 $summary=$summaryById[[string]$t.Id]
 $report=$null;$errorText=''
 if($null -eq $summary){$errorText='Сводка клиента отсутствует в пакетном ответе'}else{
  $report=@{FullDomainName=$t.FullDomainName;LastReportedUtc=$t.LastReportedStatusTime.ToUniversalTime().ToString('o');LastSyncUtc=$t.LastSyncTime.ToUniversalTime().ToString('o');LastSyncResult=[string]$t.LastSyncResult;UnknownCount=$summary.UnknownCount;NotApplicableCount=$summary.NotApplicableCount;NotInstalledCount=$summary.NotInstalledCount;DownloadedCount=$summary.DownloadedCount;InstalledCount=$summary.InstalledCount;InstalledPendingRebootCount=$summary.InstalledPendingRebootCount;FailedCount=$summary.FailedCount;RequiredComplete=$false;RequiredListComplete=$false;RequiredError='Эффективные одобрения уточняются отдельно'}
 }
 @{Id=[string]$t.Id;Name=$t.FullDomainName;Address=[string]$t.IPAddress;OS=[string]$t.OSDescription;Summary=$report;Error=$errorText}
})
@{Complete=$true;CollectedUtc=[DateTime]::UtcNow.ToString('o');Records=@($records)}|ConvertTo-Json -Depth 8 -Compress
""";
}
public partial class MainWindow {
 WsusServerSnapshot58 serverSnapshot58=Store.Read("wsus-server-snapshot58.json",new WsusServerSnapshot58());
 bool SnapshotMatches58=>serverSnapshot58.Server.Equals(OverviewServer+(WsusSsl.IsChecked==true?"|https":"|http"),StringComparison.OrdinalIgnoreCase);
 bool DuplicateRecords58(string host)=>SnapshotMatches58&&serverSnapshot58.Find(ConnectionStore.WsusName(host)).Count>1;
}
