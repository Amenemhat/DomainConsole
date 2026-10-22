using System.Windows;
using System.Windows.Media;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 public static bool RecommendationRelevant(RemoteState? state,string status)=>state!=null&&state.Recommendation.Length>0&&(status is "Failed" or "CompletedWithErrors" or "InterventionRequired");
 public static bool IsCurrentExecution(string status)=>status is "Sending" or "SubmissionUnknown" or "Submitted" or "Running";
 public static string BackgroundLabel(LiveUpdateActivity live){if(!FreshActivity(live))return "Фоновая очередь · текущая активность не подтверждена · подробности";int moving=live.Downloads.Count(d=>d.State=="Transferring");int waiting=live.Downloads.Count(d=>d.State is "Queued" or "Connecting");int paused=live.Downloads.Count(d=>d.State=="Suspended");int errors=live.Downloads.Count(d=>d.State is "Error" or "TransientError");return "Отдельная активность клиента · "+(live.InstallerBusy?"установщик занят; ":"")+(moving>0?"BITS сообщает передачу: "+moving:"передача BITS не подтверждена")+" · в очереди: "+waiting+"; приостановлено: "+paused+"; ошибок: "+errors;}
 public static string DownloadSource(BackgroundDownload download,LiveUpdateActivity live)=>Uri.TryCreate(download.RemoteName,UriKind.Absolute,out var source)&&Uri.TryCreate(live.PolicySource,UriKind.Absolute,out var policy)&&source.Host.Equals(policy.Host,StringComparison.OrdinalIgnoreCase)&&source.Port==policy.Port&&source.AbsolutePath.StartsWith("/Content/",StringComparison.OrdinalIgnoreCase)?"Источник совпадает с WSUS из политики клиента":"Связь источника с настроенным WSUS не установлена";
 void UpdateWsusStage(){if(ComputerGrid.SelectedItem is not ComputerRow selected){CurrentStage.Text="Выберите компьютер.";CurrentProgress.Visibility=Visibility.Collapsed;BackgroundGroup.Visibility=Visibility.Collapsed;RecommendationButton.Visibility=Visibility.Collapsed;return;}
  var row=ExecutionRow(selected,MainTabs.SelectedItem==WsusTab);var state=row.Target.State;bool active=IsCurrentExecution(row.Target.Status);bool fresh=ConnectionFresh(row.Host)&&StateFresh(state);bool terminal=state!=null&&!active;
  RefreshControl37(selected);RefreshUser37(selected);CurrentComputer.Text=row.Name;CurrentDetails.Text=row.OS+" · "+row.Host+ConnectionStore.Description(row.Host)+" · "+ConnectionDescription(row.Host)+" · "+UptimeText(row.Host);CurrentDetails.ToolTip=UptimeHint(row.Host);
  int count=History.SelectMany(j=>j.Targets).Count(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)&&(t.Status is "Queued" or "Sending" or "SubmissionUnknown" or "Submitted" or "Running"));if(count>0)CurrentDetails.Text+=" · активных заданий: "+count;
  CurrentStage.Text=StageText34(row,state,active,fresh);if(readFailures34.TryGetValue(row.Host+"|результат задания",out var readFailure))CurrentStage.Text+="\nСтатус не обновлён · повтор после "+readFailure.Next.ToLocalTime().ToString("HH:mm:ss");
  if(!active&&ForeignControl37(row.Host)&&rebootSnapshots.TryGetValue(row.Host,out var observed37)){var external=observed37.ExternalJobs37.LastOrDefault(x=>x.Ended.Length==0);CurrentStage.Text="Наблюдение · "+ControlText37(row.Host)+(external!=null?"\n"+external.Stage+(external.Progress.HasValue?" · "+external.Progress+"%":"")+"\nСнимок: "+Display36.Date(external.Updated,true):"\n"+observed37.Control37?.Operation);}
  bool reboot35=!active&&RebootReason(selected).Length>0;
  if(reboot35)CurrentStage.Text="Нужна перезагрузка"+(row.Target.Status is "Failed" or "CompletedWithErrors"?" · неудачная попытка сохранена в журнале":"");
  bool failed=row.Target.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired";CurrentStage.Foreground=reboot35?Theme.Tone("Warning"):failed?Theme.Tone("Error"):Theme.Brush("TextPrimary");CurrentStage.ToolTip=state?.Stage.Length>0?state.Stage+"\n"+CurrentStage.Text:CurrentStage.Text;

  if(RebootReason(selected).Length>0)RecommendationButton.Visibility=Visibility.Collapsed;
  CurrentProgress.Visibility=active?Visibility.Visible:Visibility.Collapsed;CurrentProgress.IsIndeterminate=active&&(fresh||deploying.Contains(row.JobId+row.Host))&&state?.Progress==null;CurrentProgress.Value=state?.Progress??0;WsusProgress.IsIndeterminate=CurrentProgress.IsIndeterminate;WsusStage.Text=CurrentStage.Text;
  RecommendationButton.Visibility=RecommendationRelevant(state,row.Target.Status)&&RebootReason(selected).Length==0?Visibility.Visible:Visibility.Collapsed;RecommendationButton.Background=Brushes.Moccasin;RecommendationButton.Foreground=Brushes.DarkRed;RecommendationButton.ToolTip=state?.Recommendation;
  DiagnosticStage.Text=state==null?"":"Диагностика: "+DiagnosticLabel(state);DiagnosticStage.Visibility=state!=null&&(state.DiagnosticStatus is "Pending" or "Running" or "LegacyRunning")?Visibility.Visible:Visibility.Collapsed;
  BackgroundGroup.Visibility=Visibility.Collapsed;BackgroundProgress.Visibility=Visibility.Collapsed;DownloadPriorityButton.Visibility=Visibility.Collapsed;
  if(backgroundUpdates.TryGetValue(row.Host,out var live)&&(live.Downloads.Count>0||live.InstallerBusy||live.Limitation.Length>0)){
   BackgroundGroup.Visibility=Visibility.Visible;BackgroundSummary.Text=BackgroundLabel(live);BackgroundRelation.Text="Это отдельный снимок клиента, не результат выбранного задания. Связь записей BITS с этим запуском не установлена.";
   BackgroundDetails.Text=BackgroundText(row.Host,live)+"\n\n"+string.Join("\n\n",live.Downloads.Take(8).Select(d=>"Задание: "+d.Id+"\nФайл: "+d.LocalName+"\nИсточник: "+d.RemoteName+"\n"+DownloadSource(d,live)+"\nИзменено: "+LocalDateConverter.Format(d.ModifiedUtc)))+(live.Limitation.Length>0?"\nОшибка дополнительного опроса не меняет итог основного задания.":"");
   var download=live.Downloads.FirstOrDefault(d=>d.State=="Transferring");
   if(FreshActivity(live)&&download!=null){BackgroundProgress.Visibility=Visibility.Visible;BackgroundProgress.IsIndeterminate=!download.Total.HasValue||download.Total<=0||download.Received>download.Total;BackgroundProgress.Value=download.Total>0?Math.Clamp((double)download.Received*100/download.Total.Value,0,100):0;}
   DownloadPriorityButton.Visibility=FreshActivity(live)&&live.Downloads.Any(d=>d.Accelerated||(d.State is "Queued" or "Connecting" or "Transferring"))?Visibility.Visible:Visibility.Collapsed;
  }
  RefreshHeader31(selected);
 }
}

