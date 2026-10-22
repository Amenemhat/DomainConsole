using System.Windows.Media;
namespace DomainConsole;
public sealed partial class RemoteService {
 public const string RequiredUpdatesScript31="""
function Read-RequiredUpdates31($target){
 $result=@{NeededCount=$null;ApprovedUnknownCount=0;ApprovedDownloadedCount=0;ApprovedFailedCount=0;RequiredComplete=$false;RequiredError='';RequiredUpdates=@();RequiredListComplete=$false;RequiredListError=''}
 try {
  $scope=New-Object Microsoft.UpdateServices.Administration.UpdateScope
  $scope.ApprovedStates=[Microsoft.UpdateServices.Administration.ApprovedStates]::LatestRevisionApproved
  $scope.UpdateApprovalActions=[Microsoft.UpdateServices.Administration.UpdateApprovalActions]::Install
  $scope.IncludedInstallationStates=[Microsoft.UpdateServices.Administration.UpdateInstallationStates]::NotInstalled -bor [Microsoft.UpdateServices.Administration.UpdateInstallationStates]::Downloaded -bor [Microsoft.UpdateServices.Administration.UpdateInstallationStates]::Failed -bor [Microsoft.UpdateServices.Administration.UpdateInstallationStates]::Unknown
  $infos=@($target.GetUpdateInstallationInfoPerUpdate($scope)|Where-Object {[string]$_.UpdateApprovalAction -eq 'Install'}|Group-Object {[string]$_.UpdateId}|ForEach-Object {$_.Group[0]})
  $result.ApprovedUnknownCount=@($infos|Where-Object {[string]$_.UpdateInstallationState -eq 'Unknown'}).Count
  $needed=@($infos|Where-Object {[string]$_.UpdateInstallationState -in @('NotInstalled','Downloaded','Failed')})
  $result.NeededCount=$needed.Count;$result.ApprovedDownloadedCount=@($needed|Where-Object {[string]$_.UpdateInstallationState -eq 'Downloaded'}).Count
  $result.ApprovedFailedCount=@($needed|Where-Object {[string]$_.UpdateInstallationState -eq 'Failed'}).Count
  $result.RequiredComplete=$result.ApprovedUnknownCount -eq 0
  $names=@();$nameErrors=@()
  foreach($info in @($needed|Select-Object -First 200)){
   try{$update=$info.GetUpdate();$names+=@{Id=[string]$info.UpdateId;Revision=[int]$update.Id.RevisionNumber;Title=[string]$update.Title;KB=($update.KnowledgebaseArticles -join ', ');State=[string]$info.UpdateInstallationState}}
   catch{$nameErrors+=$_.Exception.Message}
  }
  $result.RequiredUpdates=@($names);$result.RequiredListComplete=($names.Count -eq $needed.Count)
  if($nameErrors.Count){$result.RequiredListError='Часть названий недоступна: '+$nameErrors[0]}
  if($needed.Count -gt 200){$result.RequiredListError='Показаны первые 200 обновлений'}
 }catch{$result.RequiredError=$_.Exception.Message}
 return $result
}
""";
}
public partial class MainWindow {
 void AddClientSearch31(string host,WsusReport report){report.ClientSearchCount31=null;report.ClientSearchUtc31="";var job=History.FirstOrDefault(j=>IsWsusJob(j)&&j.Targets.Any(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase)));var target=job?.Targets.First(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase));if(target?.State==null||job==null||!CurrentIdentity(target))return;var step=target.State.Results.LastOrDefault(r=>r.Kind=="UpdateScan"&&r.ExitCode==0);if(step==null||!DateTimeOffset.TryParse(step.Ended,out var ended))return;if(DateTimeOffset.TryParse(report.LastReportedUtc,out var serverAt)&&serverAt>=ended)return;report.ClientSearchCount31=target.State.UpdatesRemaining;report.ClientSearchUtc31=step.Ended;}
 string UpdateCountText31(string host){if(!reportCache.TryGetValue(host,out var cached)||!cached.Found||!CacheMatches(cached))return "К установке: —";var r=cached.Report;if(!r.NeededCount.HasValue)return "К установке: —";return "К установке: "+(r.RequiredComplete?"":"≥ ")+r.NeededCount+(!DateTimeOffset.TryParse(r.LastReportedUtc,out var at)||DateTimeOffset.UtcNow-at>TimeSpan.FromDays(1)?" · снимок":"");}
 Brush UpdateCountBrush31(string host){if(!reportCache.TryGetValue(host,out var cached)||!CacheMatches(cached)||!cached.Report.NeededCount.HasValue)return Theme.Brush("TextMuted");var r=cached.Report;return !r.RequiredComplete?Theme.Tone("Warning"):r.NeededCount>0?Theme.Tone("Info"):DateTimeOffset.TryParse(r.LastReportedUtc,out var at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromDays(1)?Theme.Tone("Success"):Theme.Brush("TextMuted");}
}
