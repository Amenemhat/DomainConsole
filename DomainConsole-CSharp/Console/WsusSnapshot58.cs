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
 Dictionary<string,List<WsusServerRecord58>>? names58,shortNames58;
 public List<WsusServerRecord58> Find(string name) {
  string key=name.Trim().TrimEnd('.');
  if(key.Length==0)return new();
  names58??=Records.GroupBy(r=>r.Name.TrimEnd('.'),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.OrdinalIgnoreCase);
  shortNames58??=Records.GroupBy(r=>r.Name.Split('.')[0],StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.OrdinalIgnoreCase);
  if(names58.TryGetValue(key,out var exact))return exact;
  return key.Contains('.')?new():shortNames58.GetValueOrDefault(key)??new();
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
 public async Task<List<OverviewReply>> ReadWsusDetails58(WsusSettings settings,List<ComputerRow> rows,WsusServerSnapshot58 snapshot,CancellationToken token){
  using var read=PollingControl43.Read();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(90));
  var wanted=rows.Select(r=>new{Host=r.Host,Name=ConnectionStore.WsusName(r.Host),Id=snapshot.Find(ConnectionStore.WsusName(r.Host)).Single().Id}).ToList();
  string script="$wanted58=ConvertFrom-Json -InputObject "+PowerShellBridge.Literal(JsonSerializer.Serialize(wanted))+";$port="+settings.Port+";$ssl="+(settings.Ssl?"$true":"$false")+";"+RequiredUpdatesScript31+DetailsScript58;
  var json=await PowerShellBridge.Execute(Invoke(settings.Host,script),timeout.Token);
  var replies=JsonSerializer.Deserialize<List<OverviewReply>>(json,Store.Options)??throw new Exception("Пустой ответ одобрений WSUS");
  foreach(var reply in replies.Where(r=>r.Error.Length==0&&r.Report!=null)){
   var wantedRow=wanted.Single(r=>r.Host.Equals(reply.Host,StringComparison.OrdinalIgnoreCase));
   var summary=snapshot.Records.Single(r=>r.Id==wantedRow.Id).Summary!;var report=reply.Report!;
   report.FullDomainName=summary.FullDomainName;report.LastReportedUtc=summary.LastReportedUtc;report.LastSyncUtc=summary.LastSyncUtc;report.LastSyncResult=summary.LastSyncResult;
   report.UnknownCount=summary.UnknownCount;report.NotApplicableCount=summary.NotApplicableCount;report.NotInstalledCount=summary.NotInstalledCount;report.DownloadedCount=summary.DownloadedCount;report.InstalledCount=summary.InstalledCount;report.InstalledPendingRebootCount=summary.InstalledPendingRebootCount;report.FailedCount=summary.FailedCount;
  }
  return replies;
 }
 public const string DetailsScript58="""
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration')
$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,$ssl,$port)
$rows58=@(foreach($wantedRow58 in @($wanted58)){
 $reply58=@{Host=$wantedRow58.Host;Found=$true;Error='';Report=$null}
 try{
  $target58=$s.GetComputerTarget([string]$wantedRow58.Id)
  if($target58.FullDomainName.TrimEnd('.') -ne $wantedRow58.Name.TrimEnd('.') -and $target58.FullDomainName.Split('.')[0] -ne $wantedRow58.Name){throw 'Имя серверной записи изменилось; требуется новый обзор'}
  $need58=Read-RequiredUpdates31 $target58
  $reply58.Report=@{RequiredUpdates=@($need58.RequiredUpdates);RequiredListComplete=$need58.RequiredListComplete;RequiredListError=$need58.RequiredListError;NeededCount=$need58.NeededCount;ApprovedUnknownCount=$need58.ApprovedUnknownCount;ApprovedDownloadedCount=$need58.ApprovedDownloadedCount;ApprovedFailedCount=$need58.ApprovedFailedCount;RequiredComplete=$need58.RequiredComplete;RequiredError=$need58.RequiredError}
 }catch{$reply58.Error=$_.Exception.Message}
 $reply58
})
ConvertTo-Json -InputObject @($rows58) -Depth 8 -Compress
""";
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
