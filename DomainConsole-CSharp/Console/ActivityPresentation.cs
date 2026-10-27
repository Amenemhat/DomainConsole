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
  var row=OperationRow38(selected);var state=row.Target.State;bool active=IsCurrentExecution(row.Target.Status);bool fresh=ConnectionFresh(row.Host)&&StateFresh(state);bool terminal=state!=null&&!active;
  RefreshControl37(selected);RefreshUser37(selected);CurrentComputer.Text=row.Name;CurrentDetails.Text=row.OS;CurrentDetails.ToolTip=row.OS+"\n"+row.Host+ConnectionStore.Description(row.Host);

  CurrentStage.Text=StageText34(row,state,active,fresh);if(readFailures34.TryGetValue(row.Host+"|результат задания",out var readFailure))CurrentStage.Text+="\nСтатус не обновлён · повтор после "+readFailure.Next.ToLocalTime().ToString("HH:mm:ss");
  if(!active&&rebootSnapshots.TryGetValue(row.Host,out var observed37)&&DateTimeOffset.TryParse(observed37.CollectedUtc,out var observedAt40)&&DateTimeOffset.UtcNow-observedAt40<RebootFresh40&&observed37.ControlRead37&&observed37.ExternalJobs37.Any(x=>x.Ended.Length==0)){var external=observed37.ExternalJobs37.LastOrDefault(x=>x.Ended.Length==0);CurrentStage.Text="Наблюдение · "+ControlText37(row.Host)+(external!=null?"\n"+external.Stage+(external.Progress.HasValue?" · "+external.Progress+"%":"")+"\nСнимок: "+Display36.Date(external.Updated,true):"\n"+observed37.Control37?.Operation);}
  bool reboot35=!active&&RebootReason(selected).Length>0;
  if(reboot35)CurrentStage.Text="Нужна перезагрузка"+(row.Target.Status is "Failed" or "CompletedWithErrors"?" · неудачная попытка сохранена в журнале":"");
  bool failed=row.Target.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired";CurrentStage.Foreground=reboot35?Theme.Tone("Warning"):failed?Theme.Tone("Error"):active?Theme.Tone("Info"):Theme.Brush("TextPrimary");CurrentStage.ToolTip=state?.Stage.Length>0?state.Stage+"\n"+CurrentStage.Text:CurrentStage.Text;

  if(RebootReason(selected).Length>0)RecommendationButton.Visibility=Visibility.Collapsed;
  CurrentProgress.Visibility=active?Visibility.Visible:Visibility.Collapsed;CurrentProgress.IsIndeterminate=active&&(fresh||deploying.Contains(row.JobId+row.Host))&&state?.Progress==null;CurrentProgress.Value=state?.Progress??0;WsusProgress.IsIndeterminate=CurrentProgress.IsIndeterminate;WsusStage.Text=CurrentStage.Text;
  RecommendationButton.Visibility=RecommendationRelevant(state,row.Target.Status)&&RebootReason(selected).Length==0?Visibility.Visible:Visibility.Collapsed;RecommendationButton.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty,"ButtonSurface");RecommendationButton.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty,"WarningText");RecommendationButton.ToolTip=state?.Recommendation;
  DiagnosticStage.Text=state==null?"":"Диагностика: "+DiagnosticLabel(state);DiagnosticStage.Visibility=state!=null&&(state.DiagnosticStatus is "Pending" or "Running" or "LegacyRunning")?Visibility.Visible:Visibility.Collapsed;
  BackgroundGroup.Visibility=Visibility.Collapsed;BackgroundProgress.Visibility=Visibility.Collapsed;DownloadPriorityButton.Visibility=Visibility.Collapsed;
  if(backgroundUpdates.TryGetValue(row.Host,out var live)&&(live.Downloads.Count>0||live.InstallerBusy||live.Limitation.Length>0)){
   BackgroundGroup.Visibility=Visibility.Visible;BackgroundSummary.Text="Фоновая активность клиента";BackgroundSummary.ToolTip=BackgroundLabel(live);BackgroundRelation.Text="Это отдельный снимок клиента, не результат выбранного задания. Связь записей BITS с этим запуском не установлена.";
   BackgroundDetails.Text=BackgroundText(row.Host,live)+"\n\n"+string.Join("\n\n",live.Downloads.Take(8).Select(d=>"Задание: "+d.Id+"\nФайл: "+d.LocalName+"\nИсточник: "+d.RemoteName+"\n"+DownloadSource(d,live)+"\nИзменено: "+LocalDateConverter.Format(d.ModifiedUtc)))+(live.Limitation.Length>0?"\nОшибка дополнительного опроса не меняет итог основного задания.":"");
   var download=live.Downloads.FirstOrDefault(d=>d.State=="Transferring");
   if(FreshActivity(live)&&download!=null){BackgroundProgress.Visibility=Visibility.Visible;BackgroundProgress.IsIndeterminate=!download.Total.HasValue||download.Total<=0||download.Received>download.Total;BackgroundProgress.Value=download.Total>0?Math.Clamp((double)download.Received*100/download.Total.Value,0,100):0;}
   DownloadPriorityButton.Visibility=FreshActivity(live)&&live.Downloads.Any(d=>d.Accelerated||(d.State is "Queued" or "Connecting" or "Transferring"))?Visibility.Visible:Visibility.Collapsed;
  }
  var pieces38=CurrentStage.Text.Split(new[]{'\n'},2);CurrentStage.Text=pieces38[0];OperationDetail38.Text=pieces38.Length>1?pieces38[1]:"";OperationDetail38.Visibility=OperationDetail38.Text.Length>0?Visibility.Visible:Visibility.Collapsed;
  ApplyTerminalTone39(row,active,reboot35,failed);
  OperationGlyph38.Kind=active?"Working":failed?"Error":reboot35?"Reboot":row.Target.Status=="RebootObserved"?"Warning":row.Target.Status is "Completed" or "VerifiedAfterReboot"?"Success":"Info";OperationGlyph38.Tint=row.Target.Status=="RebootObserved"?Theme.Tone("Warning"):CurrentStage.Foreground;ApplyStatus40(selected,active);
  if(active&&(TransportBudget41.Paused||(DateTimeOffset.TryParse(state?.Updated,out var observed46)&&DateTimeOffset.UtcNow-observed46>TimeSpan.FromSeconds(90))||clientConnections.GetValueOrDefault(row.Host)?.Lost38==true)){
   string observation=TransportBudget41.Paused?"Опрос выключен · сохранённый результат":row.Target.Status=="SubmissionUnknown"?"Передача задания не подтверждена":clientConnections.GetValueOrDefault(row.Host)?.Lost38==true?"Связь потеряна · состояние установки неизвестно":"Нет свежего статуса задания";
   CurrentStage.Text=observation;OperationDetail38.Text="Последний этап: "+(state?.Stage??row.Target.Status)+" · "+Display36.Date(state?.Updated??"");OperationDetail38.Visibility=Visibility.Visible;CurrentProgress.IsIndeterminate=false;
  }
  if(active&&CurrentStage.Text.StartsWith("Установка"))OperationGlyph38.Kind="Install";
  else if(active&&CurrentStage.Text.StartsWith("Скачивание"))OperationGlyph38.Kind="Download";
  else if(active&&CurrentStage.Text.Contains("поиск",StringComparison.OrdinalIgnoreCase))OperationGlyph38.Kind="Search";
  RefreshMode39();
  RefreshHeader31(selected);
 }
}

