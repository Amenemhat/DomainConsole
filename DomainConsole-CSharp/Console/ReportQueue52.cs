using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;

public sealed class QueueEvidence52 {
 public string Endpoint {get;set;}=""; public string Server {get;set;}=""; public string ReportUtc {get;set;}="";
 public ClientReportProbe Snapshot {get;set;}=new();
}
public partial class MainWindow {
 readonly Dictionary<string,QueueEvidence52> queueEvidence52=new(Store.Read("report-queue52.json",new Dictionary<string,QueueEvidence52>()),StringComparer.OrdinalIgnoreCase);
 static bool CompleteQueue52(ClientReportProbe p)=>p.QueueError.Length==0&&p.Queues.Count==2&&p.Queues.All(q=>q.Files>=0&&q.Bytes>=0)&&p.Queues.Select(q=>q.Name).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(new[]{"EventCache","EventCache.v2"});
 static string QueueHint52(ClientReportProbe p)=>"Очередь отчётности · "+(CompleteQueue52(p)?p.Queues.Sum(q=>(long)q.Files)+" файлов · "+p.Queues.Sum(q=>q.Bytes)+" байт":"размер не определён: проверка неполная")+"\nПроверено: "+Display36.Date(p.CollectedUtc)+(p.QueueError.Length>0?"\n"+p.QueueError:"");
 void RememberQueue52(TargetRecord target,WsusReport report,ClientReportProbe probe){queueEvidence52[target.Host]=new(){Endpoint=Identity40.EndpointKey(target),Server=OverviewServer,ReportUtc=report.LastReportedUtc,Snapshot=probe};Store.Write("report-queue52.json",queueEvidence52);}
 QueueEvidence52? CurrentQueue52(ComputerRow row)=>queueEvidence52.TryGetValue(row.Host,out var q)&&q.Endpoint==Identity40.EndpointKey(ConnectionStore.Capture(row.Target))&&q.Server==OverviewServer?q:null;
 Criterion45 QueueCriterion52(ComputerRow row,Criterion45 criterion,WsusReport? report){
  var evidence=CurrentQueue52(row);var recovery=reportRecoveries.GetValueOrDefault(row.Host);
  string expectedAfter=recovery!=null&&recovery.JobId.Length>0?History.FirstOrDefault(j=>j.Id==recovery.JobId)?.Targets.FirstOrDefault(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase))?.State?.Started??recovery.CreatedUtc:evidence?.ReportUtc??"";
  bool fresh=report!=null&&expectedAfter.Length>0&&Criteria45.Newer(report.LastReportedUtc,expectedAfter);
  string hint=evidence==null?"Размер очереди не определён · диагностика не получена":QueueHint52(evidence.Snapshot);
  if(reportWaiting.TryGetValue(row.Host,out var waiting))hint+="\nОжидание нового отчёта: "+Math.Max(0,(int)(DateTimeOffset.UtcNow-(waiting.Until-TimeSpan.FromMinutes(15))).TotalMinutes)+" мин";
  if(fresh)return criterion with{Hint52=hint};
  bool recommended=report!=null&&evidence!=null&&ReportRecoveryPolicy30.Recommended(evidence.Snapshot,report,ReportAgeHours(),DateTimeOffset.UtcNow);
  bool recovering=recovery!=null&&!recovery.Done;
  string phase=recovering?recovery!.Phase:recommended?"Очередь отчётности требует восстановления":reportWaiting.ContainsKey(row.Host)?"Ожидается новый отчёт":recovery is {Done:true}&&recovery.Phase=="Очередь проверена; ожидается новый отчёт"?recovery.Phase:evidence!=null&&(!CompleteQueue52(evidence.Snapshot)||evidence.Snapshot.QueueError.Length>0)?"Причина задержки отчёта не установлена":"";
  if(phase.Length==0)return criterion with{Hint52=hint};
  var checks=criterion.Checks.ToList();checks.Add(new(hint,"Info","Info"));
  if(recovering&&recovery!.Detail.Length>0)checks.Add(new(recovery.Detail,"Info","Info"));
  bool busy=History.Any(j=>IsWsusJob(j)&&j.Targets.Any(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)&&ActiveTarget(t)));
  return criterion with{Text="Отчёт WSUS · "+phase,Tone=recommended?"Warning":criterion.Tone,Checks=checks,Hint52=hint,Repair52=recommended&&!recovering&&!busy&&!LostObservation51(row.Host)&&RebootReason(row).Length==0};
 }
 static CommandStep QueueVerifyStep52()=>new(){Kind="ReportQueueVerify",Name="Проверка очереди после восстановления",Code="$ErrorActionPreference='Stop';$raw=& {\n"+RemoteService.ReportQueueScript+"\n};$q=$raw|ConvertFrom-Json -ErrorAction Stop;if($q.QueueError -or @($q.Queues).Count -ne 2 -or @($q.Queues|Where-Object {$_.Name -eq 'EventCache'}).Count -ne 1 -or @($q.Queues|Where-Object {$_.Name -eq 'EventCache.v2'}).Count -ne 1){throw 'Очередь после восстановления прочитана не полностью.'};$q.Queues|ForEach-Object {if($_.Files -lt 0 -or $_.Bytes -lt 0){throw 'Размер очереди не подтверждён.'};$_.Name+': '+$_.Files+' файлов, '+$_.Bytes+' байт'};'Очередь проверена; получение отчёта подтверждается отдельно сервером.'"};
 async void RecoverReport52_Click(object sender,RoutedEventArgs e)=>await Guard(()=>RecoverReport52(true));
 async Task RecoverReport52(bool focusOnly){ReadWsus();MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;await Submit(new(){new(){Name="Восстановление очереди отчётности",Kind="ReportRecovery",Code=DeliveryScripts.Recovery},QueueVerifyStep52(),new(){Name="WSUS · проверка и запрос свежего отчёта",Kind="UpdateScan"}},"WSUS",focusOnly:focusOnly);}
}
