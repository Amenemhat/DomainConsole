using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 sealed record Work60(string Text,bool Busy,int? Percent=null);
 readonly Dictionary<string,(string Channel,string Stage,DateTimeOffset At)> preparing60=new(StringComparer.OrdinalIgnoreCase);
 Work60? WorkState60(ComputerRow selected,bool wsus){
  var now=DateTimeOffset.UtcNow;
  if(preparing60.TryGetValue(selected.Host,out var prep)&&prep.Channel==(wsus?"WSUS":"Commands"))return new(prep.Stage+" · "+Age(now-prep.At),true);
  var row=ExecutionRow(selected,wsus);var state=row.Target.State;
  if(ActiveTarget(row.Target)){
   if(ObservationStopped51(row))return new("Наблюдение остановлено · результат задания не подтверждён",false);
   if(LostObservation51(row.Host))return new("Связь потеряна · ожидаем восстановления, результат не подтверждён",false);
   if(TransportBudget41.Paused&&!ManualJob56(row.JobId))return new("Опрос приостановлен · результат задания не подтверждён",false);
   string stage=row.Target.Status=="Queued"?"Ожидает отправки":row.Target.Status=="Sending"?"Передаём задание":row.Target.Status=="SubmissionUnknown"?"Проверяем передачу задания":StateFresh(state)?state?.Stage??"Ожидаем результат":"Получаем результат задания · последнее состояние устарело";
   string since=state?.Started??History.FirstOrDefault(j=>j.Id==row.JobId)?.Created??"";
   return new(stage+(DateTimeOffset.TryParse(since,out var at)?" · "+Age(now-at):""),true,StateFresh(state)?state?.Progress:null);
  }
  if(!wsus)return null;
  if(reportWaiting.TryGetValue(selected.Host,out var wait)&&(wait.Server54.Length==0||wait.Server54==OverviewServer)){
   if(TransportBudget41.Paused)return new("Ожидание отчёта WSUS · опрос приостановлен",false);
   if(wait.Delayed54||now>=wait.Until)return new("Отчёт WSUS задерживается · срок ожидания истёк",false);
   int sec=Math.Max(0,(int)Math.Ceiling((wait.Until-now).TotalSeconds));
   return new((reportReading.Contains(selected.Host)?"Получаем отчёт WSUS":"Ожидаем новый отчёт WSUS")+" · до срока ожидания "+sec/60+" мин "+sec%60+" с",true);
  }
  if(maintenancePlans.TryGetValue(selected.Host,out var plan)&&!plan.Done){
   bool waiting=plan.Phase=="Ожидается перезагрузка"||plan.Phase=="Требуется действие";
   return new(TransportBudget41.Paused?"Наблюдение за обслуживанием приостановлено":waiting?plan.Phase:"Обслуживание · "+plan.Phase, !waiting&&!TransportBudget41.Paused&&!LostObservation51(selected.Host));
  }
  if(reportReading.Contains(selected.Host)&&!TransportBudget41.Paused)return new("Получаем данные WSUS и сопоставляем результат",true);
  return null;
 }
 void RenderWork60(ComputerRow selected){
  var work=WorkState60(selected,true);
  if(work!=null){
   var row=ExecutionRow(selected,true);bool error=row.Target.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired";
   if(error){OperationDetail38.Text=work.Text;OperationDetail38.Visibility=Visibility.Visible;}
   else {CurrentStage.Text=work.Text;CurrentStage.Foreground=Theme.Tone(work.Busy?"Info":"Warning");OperationGlyph38.Kind=work.Busy?StatusGlyph.ForOperation50(work.Text):"Warning";OperationGlyph38.Tint=CurrentStage.Foreground;}
   CurrentProgress.Visibility=work.Busy?Visibility.Visible:Visibility.Collapsed;CurrentProgress.IsIndeterminate=work.Busy&&!work.Percent.HasValue;CurrentProgress.Value=work.Percent??0;CurrentProgress.ToolTip="Процесс операции; ожидание результата не является процентом установки";
  }
  var command=WorkState60(selected,false);
  CommandActivity60.Visibility=command==null?Visibility.Collapsed:Visibility.Visible;
  CommandStage60.Text=command?.Text??"";CommandProgress60.Visibility=command?.Busy==true?Visibility.Visible:Visibility.Collapsed;CommandProgress60.IsIndeterminate=command?.Busy==true&&!command.Percent.HasValue;CommandProgress60.Value=command?.Percent??0;
  LinkBusy60.Visibility=manualLinkReads31.Contains(selected.Host)?Visibility.Visible:Visibility.Hidden;
 }
 async Task SubmitTargets60(RemoteJob template,List<ComputerRow> selected,List<ComputerRow> frozen,bool beginPlan){
  // Each target owns its decision and submission. A modeless decision cannot hold ready peers.
  var admitted=new List<(ComputerRow Selected,ComputerRow Frozen)>();
  foreach(var frozenRow in frozen){if(preparing60.ContainsKey(frozenRow.Host)){Notify(frozenRow.Name+": подготовка уже выполняется; повтор не создан.");continue;}preparing60[frozenRow.Host]=(template.OutputChannel,"Подготовка · проверяем возможность запуска",DateTimeOffset.UtcNow);admitted.Add((selected.Single(r=>r.Host==frozenRow.Host),frozenRow));}
  RefreshActivity();UpdateWsusStage();
  using var slots=new SemaphoreSlim(3);
  await Task.WhenAll(admitted.Select(async pair=>{
   var job=Store.Clone(template);job.Id=Guid.NewGuid().ToString();job.Targets.Clear();
   try{
    async Task<(bool Accepted,bool Force,bool Wait)> Prepare(){if(template.OutputChannel=="WSUS"){await slots.WaitAsync();try{var repaired=await remote.InspectControl56(pair.Frozen.Target,true);using var repairDoc=System.Text.Json.JsonDocument.Parse(repaired);if(repairDoc.RootElement.GetProperty("Removed").GetArrayLength()>0)Event31(pair.Frozen.Host,"reconcile60",job.Id,"Освобождены блокировки прерванных заданий","Warning",repairDoc.RootElement.GetProperty("Message").GetString()??"");}finally{slots.Release();}}return await PrepareUpdateAction(new(){pair.Frozen},job.Steps,job.MicrosoftSource,slots);}
    if(await CompletePreparedSubmission47(job,new(){pair.Selected},new(){pair.Frozen},Prepare,beginPlan))await SendQueue(job.Id);
   }catch(Exception ex){Event31(pair.Frozen.Host,"prepare60",job.Id,"Запуск не выполнен","Warning",ex.Message);Notify(pair.Frozen.Name+": "+ShortConnectionError(ex.Message));}
   finally{preparing60.Remove(pair.Frozen.Host);RefreshActivity();if(CurrentHost(pair.Frozen.Host))UpdateWsusStage();}
  }));
 }
}
