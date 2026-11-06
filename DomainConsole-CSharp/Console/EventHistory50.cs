using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 static bool ServiceEvent50(ClientEvent31 e){
  string category=e.Operation36.Split('|')[0];
  if(category is "job-service50" or "link" or "poll42"||category.StartsWith("read-"))return true;
  if(category=="maintenance"&&e.Text=="Ожидается перезагрузка"&&(e.Details.Contains("Клиент пока не ответил")||e.Details.Contains("повторный опрос")))return true;
  if(category=="maintenance")return e.Text is not ("Ожидается перезагрузка" or "Обслуживание завершено" or "Клиентская часть обслуживания завершена" or "Обслуживание остановлено" or "Автоматическое продолжение выключено" or "Требуется действие")||e.Details.Contains("См. результат задания.");
  // Existing history is retained, with its old final-report labels treated as service information.
  return category=="job"&&(e.Text.Contains("запрос отправки отчёта")||e.Text.StartsWith("Выполняется"));
 }
 static (string Title,string Kind,string Detail) StepEvent50(StepResult step){
  if(step.ExitCode!=0)return ("Ошибка · "+step.Name,"Error",step.Error);
  if(step.Kind=="CacheCleanup"){
   var c=step.Cache50;
   if(c==null)return ("Очистка кэша завершена","Success","Шаг выполнен успешно; подробная сводка изоляции отсутствует в этой версии агента.");
   if(c.Isolated==0)return ("Кэш для очистки отсутствует","Info","Каталоги не переносились; службы запущены.");
   string name=step.Name.Contains("Download")?"Кэш скачивания очищен":step.Name.Contains("Queue")?"Очередь отчётности очищена":"Кэш обновлений очищен";
   string detail="Службы запущены.";
   if(c.Backups>0)detail+=" Резервная копия сохранена.";
   if(c.PendingDelete>0)detail+=c.CleanupStarted?" Старые файлы переданы на фоновое удаление.":" Старые файлы изолированы; запуск фонового удаления не подтверждён.";
   return (name,c.PendingDelete>0&&!c.CleanupStarted?"Warning":"Success",detail);
  }
  if(step.Kind=="Updates"&&step.UpdatesFound50==0)return ("Поиск завершён · найдено обновлений: 0","Info","Установка не выполнялась. Результат WSUS сопоставляется отдельно.");
  if(step.Kind=="UpdateScan")return (step.UpdatesFound50.HasValue?"Поиск завершён · найдено обновлений: "+step.UpdatesFound50:"Поиск обновлений завершён","Success","Установка в этом шаге не выполнялась.");
  return ("Завершено · "+step.Name,"Success","");
 }
 void RecordJobEvents50(string host,string id,PollResult data){
  var state=data.State!;
  foreach(var update in state.InstalledUpdates45)Event31(host,"update-installed54",update.Id+"|"+update.Revision+"|"+update.At,"Установлено · "+update.Title,"Success","Результат установки подтверждён агентом; задание: "+id,update.At,scope:id+"|"+update.Id+"|"+update.Revision);
  foreach(var update in data.Updates.Where(u=>u.ResultCode.HasValue&&u.ResultCode!=2))Event31(host,"update-result54",update.Title+"|"+update.ResultCode+"|"+update.HResult,"Установка не завершена · "+update.Title,"Warning","ResultCode: "+update.ResultCode+"; HRESULT: "+update.HResult, state.Updated,scope:id+"|"+update.Title);
  bool cache=state.Results.Any(r=>r.Kind=="CacheCleanup");
  for(int i=0;i<state.Results.Count;i++){
   var step=state.Results[i];if(string.IsNullOrEmpty(step.Ended))continue;
   // WUA failure already has a precise per-update title (KB, download vs installation).
   if(step.ExitCode!=0&&step.Kind is "Updates" or "UpdateScan")continue;
   var entry=StepEvent50(step);if(step.Kind=="UpdateScan"&&state.InstalledCount38>0)entry=("Поиск и автоматическая установка завершены","Success","Найдено: "+step.UpdatesFound50+"; установлено: "+state.InstalledCount38+". Результаты каждого пакета сохранены отдельно.");string title=entry.Title;
   if(step.Name=="Перезагрузка текущего компьютера"&&step.ExitCode==0){
    var receipt=data.RebootPlan;
    entry=receipt?.Cancelled==true?("Перезагрузка отменена","Info","Команда отменена до перезапуска."):receipt?.DispatchExitCode==0?("Команда перезагрузки принята Windows","Info","Новая загрузка и флаги проверяются отдельно."):("Команда назначения перезагрузки завершена","Info","Отправка shutdown и новая загрузка ещё требуют подтверждения.");title=entry.Title;
   }

   if(cache&&step.Kind=="UpdateScan"&&step.ExitCode==0)title=step.UpdatesFound50.HasValue?"После очистки найдено обновлений: "+step.UpdatesFound50:"Поиск после очистки завершён";
   Event31(host,step.Name.Contains("запрос отправки отчёта")&&step.Kind!="UpdateScan"&&step.ExitCode==0?"job-service50":"job-result50",step.ExitCode+"|"+step.Ended,title,entry.Kind,entry.Detail,step.Ended,scope:id+"|step-"+(i+1));
  }
  bool failed=Failed35(state.Status);bool represented=state.Results.Any(r=>r.ExitCode!=0);
  if(failed&&(!represented||state.Results.Any(r=>r.ExitCode!=0&&r.Kind is "Updates" or "UpdateScan")))
   Event31(host,"job-result50",state.Status,JobEvent35(data),"Error",state.Error,state.Ended.Length>0?state.Ended:state.Updated,scope:id+"|failure");
  else if(state.Status=="Cancelled")Event31(host,"job-result50","cancelled","Задание отменено","Warning","Уже полученные результаты сохранены.",state.Ended,scope:id+"|cancel");
  else if(!IsCurrentExecution(state.Status)&&!failed&&state.Results.Count==0)
   Event31(host,"job-result50",state.Status,JobEvent35(data),"Info",state.Error,state.Updated,scope:id+"|terminal");
  if(IsCurrentExecution(state.Status))Event31(host,"job-service50",state.Stage,"Выполняется · "+state.Stage,"Info","",state.Updated,scope:id);
 }
 void RecordConnectionTransition50(string host,bool wasLost,bool wasAccess,ClientConnectionState value){
  bool access=value.Kind38(DateTimeOffset.UtcNow)=="Access";
  if(access&&!wasAccess)Event31(host,"link-state50","access","Ошибка WinRM/аутентификации","Warning");
  else if(!wasLost&&value.Lost38)Event31(host,"link-state50","lost","Потеря связи подтверждена","Warning","Клиентские задания этим не остановлены.");
  else if((wasLost||wasAccess)&&!value.Lost38&&!access&&value.Error.Length==0)Event31(host,"link-state50","restored","Связь восстановлена","Success");
 }
}
