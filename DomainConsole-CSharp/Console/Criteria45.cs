using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed record Check45(string Text,string Tone,string Glyph);
public sealed record Criterion45(string Key,string Text,string Tone,string Glyph,List<Check45> Checks,bool Action=false);
public static class Criteria45 {
 public static List<Criterion45> Confirmed(ReadyProof43 proof)=>new(){
  new("updates","Обновления · необходимых нет · "+Display36.Date(proof.Scan),"Success","Success",new(){new("При полной проверке: поиск успешен, применимость определена, необходимых обновлений 0","Success","Success"),new("Актуальных неразрешённых ошибок на момент проверки не было","Success","Success")}),
  new("report","Отчёт WSUS · подтверждён "+Display36.Date(proof.Report),"Success","Success",new(){new("При полной проверке: клиент найден на выбранном сервере, данные прочитаны и согласованы","Success","Success"),new("Отчёт и успешный обмен с WSUS подтверждены при проверке "+Display36.Date(proof.At),"Success","Success")}),
  new("reboot","Перезагрузка · не требовалась · "+Display36.Date(proof.Reboot),"Success","Success",new(){new("Полная отрицательная проверка флагов на момент "+Display36.Date(proof.Reboot),"Success","Success"),new("Последующие подтверждённые изменения отменяют этот результат; одна давность — нет","Info","Info")})
 };
 public static bool Newer(string a,string b)=>DateTimeOffset.TryParse(a,out var x)&&DateTimeOffset.TryParse(b,out var y)&&x>y;
 public static bool Resolved45(WsusReport report,RemoteState? state){
  if(state==null||!Newer(state.Ended,report.LastReportedUtc)||report.FailedCount<=0||report.FailedUpdates.Count!=report.FailedCount)return false;
  return report.FailedUpdates.All(f=>Guid.TryParse(f.Id,out _)&&f.Revision>0&&state.InstalledUpdates45.Any(i=>i.Id.Equals(f.Id,StringComparison.OrdinalIgnoreCase)&&i.Revision==f.Revision&&Newer(i.At,report.LastReportedUtc)));
 }
 public static List<Criterion45> Build(ClientAssessment41 status,WsusReport? r,RebootView40 reboot,RemoteState? state,bool active,string scan,int? remaining,bool saved,string error,string boot){
  bool ready=status.Kind=="Success";string install=state?.Results.Where(x=>x.Kind=="Updates"&&x.ExitCode==0).OrderByDescending(x=>x.Ended).FirstOrDefault()?.Ended??"";
  bool afterInstall=r!=null&&Newer(install,r.LastReportedUtc),installed=install.Length>0&&state?.InstalledCount38>0;
  bool failed=state?.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired" or "SubmissionUnknown";
  bool reportFailure=r?.FailedCount>0, resolved=r!=null&&Resolved45(r,state);
  bool searchOk=DateTimeOffset.TryParse(scan,out var searched45)&&searched45<=DateTimeOffset.UtcNow.AddMinutes(1)&&(ready||DateTimeOffset.UtcNow-searched45<=TimeSpan.FromHours(ReportPresentation35.FreshHours))&&(!DateTimeOffset.TryParse(boot,out var boot45)||searched45>=boot45)&&(remaining==0||ready);bool countOk=r?.NeededCount==0&&r.RequiredComplete&&r.ApprovedUnknownCount==0&&r.RequiredError.Length==0;
  string ut=active?"Выполняется операция":installed&&reboot.Required==true?"Установка успешна · завершение после перезагрузки":ready?"Необходимых обновлений нет":r?.NeededCount>0?"К установке по отчёту: "+r.NeededCount:failed?"Ошибка задания":"Нужна проверка результата";
  bool beforeBoot45=r!=null&&DateTimeOffset.TryParse(boot,out var bootReport45)&&DateTimeOffset.TryParse(r.LastReportedUtc,out var reported45)&&reported45<bootReport45;
  string updateTone=active?"Info":failed?"Error":installed&&reboot.Required==true?"Success":ready?"Success":reportFailure&&!resolved&&!afterInstall?"Error":r?.NeededCount>0?"Info":"Warning";
  var updates=new List<Check45>{new(scan.Length>0?"Поиск клиента: "+Display36.Date(scan):"Успешный поиск клиента не подтверждён",searchOk?"Success":"Warning",searchOk?"Success":"Warning"),new(countOk?"Применимость одобренных обновлений определена; по отчёту к установке 0":r==null?"Нет данных о применимости":r.RequiredComplete?"По отчёту к установке: "+r.NeededCount:"Подсчёт применимости неполный",countOk?"Success":"Warning",countOk?"Success":"Warning"),new(failed?"Ошибка задания: "+ResultPresentation36.BriefFailure(state!.Error):active?"Операция ещё выполняется":installed?"Установка завершена успешно: "+Display36.Date(install):ready?"Установка не требуется · необходимых обновлений нет":"Успешная установка не требовалась либо не подтверждена",failed?"Error":installed||ready?"Success":"Info",failed?"Error":installed||ready?"Success":"Info")};
  if(scan.Length>0&&DateTimeOffset.TryParse(boot,out var b)&&DateTimeOffset.TryParse(scan,out var s)&&s<b)updates.Add(new("Поиск выполнен до последней загрузки","Warning","Warning"));
  if(reportFailure)updates.Add(new(resolved?"Прежние ошибки сопоставлены с успешной установкой тех же ID и ревизий":afterInstall?"В старом отчёте есть ошибки: "+r!.FailedCount+" · их снятие сервером ещё не подтверждено":"Ошибок по отчёту WSUS: "+r!.FailedCount,resolved||afterInstall?"Warning":"Error",resolved||afterInstall?"Warning":"Error"));
  if(!active&&!ready){if(updates.Any(c=>c.Tone=="Error"))updateTone="Error";else if(updates.Any(c=>c.Tone=="Warning"))updateTone="Warning";}
  string rt=r==null?"Не получен":afterInstall?"До повторной установки · ожидается новый отчёт":Display36.Date(r.LastReportedUtc)+" · "+ReportPresentation35.Age(r.LastReportedUtc);
  string reportTone=r?.FailedCount>0&&!resolved&&!afterInstall||(r?.LastSyncResult is "Failed" or "FailedWithErrors")&&!afterInstall?"Error":r==null||afterInstall||beforeBoot45||r.LastSyncResult!="Succeeded"||error.Length>0||saved&&!ready?"Warning":ready?"Success":ReportPresentation35.Tone(r.LastReportedUtc,DateTimeOffset.UtcNow,ReportPresentation35.FreshHours);
  var reports=new List<Check45>{new(r==null?"Клиент на сервере не подтверждён":"Запись клиента на выбранном сервере найдена",r==null?"Warning":"Success",r==null?"Warning":"Success"),new(error.Length>0?"Чтение сервера не удалось · сохранены прежние данные":saved?"Сохранённый снимок сервера":"Данные сервера прочитаны",error.Length>0||saved&&!ready?"Warning":"Success",error.Length>0||saved&&!ready?"Warning":"Success")};
  if(beforeBoot45)reports.Add(new("Отчёт получен до последней загрузки · нужен новый отчёт","Warning","Warning"));
  if(r!=null){reports.Add(new("Отчёт: "+Display36.Date(r.LastReportedUtc)+(afterInstall?" · предшествует установке":""),afterInstall?"Warning":ready?"Success":ReportPresentation35.Tone(r.LastReportedUtc,DateTimeOffset.UtcNow,ReportPresentation35.FreshHours),afterInstall?"Warning":"Info"));reports.Add(new("Последнее соединение с WSUS: "+Display36.Date(r.LastSyncUtc)+" · "+WsusReport.SyncLabel(r.LastSyncResult),r.LastSyncResult=="Succeeded"?"Success":"Warning",r.LastSyncResult=="Succeeded"?"Success":"Warning"));}
  string bt=reboot.Required==true?"Подтверждена · "+reboot.Sources.Length+" источн. · "+string.Join(", ",reboot.Sources):reboot.Required==false?(ready||reboot.Fresh?"Не требуется":"По последней проверке не требовалась"):"Результат не подтверждён";
  bt+=" · "+Display36.Date(reboot.Stamp);
  string rebootTone=reboot.Required==true?"Warning":reboot.Required==false&&(ready||reboot.Fresh)?"Success":"Stale";
  var reboots=new List<Check45>{new(reboot.Required==true?"Положительные источники: "+string.Join(", ",reboot.Sources):reboot.Required==false?"Полная проверка WUA / Windows Update / CBS — флаги отсутствуют":"Полная отрицательная проверка не получена",rebootTone,reboot.Required==true?"Reboot":rebootTone=="Success"?"Success":"Info"),new("Проверено: "+Display36.Date(reboot.Stamp),"Info","Info")};
  if(reboot.Limitation.Length>0)reboots.Add(new(reboot.Limitation,"Warning","Warning"));
  bool action=!active&&!reboot.Fresh&&(reboot.Required==true||reboot.Limitation.Contains("Перезагрузка выполнена")||status.Kind=="Warning"&&status.Title.Contains("флаги перезагрузки"));
  return new(){new("updates","Обновления · "+ut,updateTone,active?"Working":updateTone=="Success"?"Success":updateTone=="Error"?"Error":"Info",updates),new("report","Отчёт WSUS · "+rt,reportTone,reportTone=="Success"?"Success":reportTone=="Error"?"Error":"Warning",reports),new("reboot","Перезагрузка · "+bt,rebootTone,reboot.Required==true?"Reboot":rebootTone=="Success"?"Success":"Info",reboots,action)};
 }
}
public partial class MainWindow {
 readonly Dictionary<string,List<Criterion45>> criteria45=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,bool> expansion45=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> seenProblems45=new(StringComparer.OrdinalIgnoreCase);
 string rendered45="";bool rendering45;
 ClientAssessment41 ExplainAssessment45(ComputerRow row,ClientAssessment41 a,CachedWsusReport? cache,RebootView40 reboot,RemoteState? state,bool active,string scan,int? remaining){
  if(!IsCurrent(row))return a;
  if(a.Kind=="Success"&&readyProofs43.TryGetValue(row.Host,out var ready45)){if(scan.Length==0){scan=ready45.Scan;remaining=0;}}
  var list=a.Kind=="Success"&&readyProofs43.TryGetValue(row.Host,out var complete45)?Criteria45.Confirmed(complete45):Criteria45.Build(a,cache?.Found==true?cache.Report:null,reboot,state,active,scan,remaining,cache!=null&&!liveReports.Contains(row.Host),overviewErrors.GetValueOrDefault(row.Host,""),rebootSnapshots.GetValueOrDefault(row.Host)?.BootUtc??"");criteria45[row.Host]=list;
  if(a.Kind=="Warning"&&list.Count(g=>g.Tone is "Warning" or "Error")>1)a=a with{Title="Готовность не подтверждена · несколько условий не выполнены"};
  return a with{Details=string.Join("\n",list.Select(g=>(g.Tone=="Success"?"✓ ":g.Tone=="Error"?"✕ ":g.Tone is "Warning" or "Stale" or "Unknown"?"⚠ ":"• ")+g.Text))+"\n"+(a.Kind=="Success"?a.Details:reboot.Limitation)};
 }
 void RenderCriteria45(ComputerRow row,ClientAssessment41 a,bool active){
  if(!criteria45.TryGetValue(row.Host,out var groups))return;
  ReadinessTree45.Visibility=Visibility.Visible;string signature=Theme.Current+"|"+row.Host+"|"+active+"|"+string.Join("|",groups.Select(g=>g.Text+g.Tone+g.Action+string.Join(";",g.Checks.Select(c=>c.Text+c.Tone))));if(rendered45==signature)return;rendered45=signature;
  rendering45=true;ReadinessTree45.Children.Clear();bool expandedProblem=false;
  foreach(var group in groups){string key=row.Host+"|"+group.Key;var exp=new Expander{Margin=new(0,1,0,1)};exp.SetResourceReference(StyleProperty,"ReadinessDisclosure46");var head=new DockPanel{LastChildFill=true};
   if(group.Action){var button=new Button{Content="Проверить сейчас",Margin=new(8,0,0,0),Padding=new(4,0,4,0),VerticalAlignment=VerticalAlignment.Center,IsEnabled=!TransportBudget41.Paused&&!rebootChecks40.Contains(row.Host)};button.SetResourceReference(StyleProperty,"GhostButton");button.Click+=RebootCheck40_Click;DockPanel.SetDock(button,Dock.Right);head.Children.Add(button);}
   var icon=new StatusGlyph{Kind=group.Glyph,Tint=Theme.Tone(group.Tone),Width=16,Height=16,Margin=new(0,0,5,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(icon,Dock.Left);head.Children.Add(icon);var label=new TextBlock{Text=group.Text,FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=Theme.Tone(group.Tone),TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=group.Text};head.Children.Add(label);exp.Header=head;exp.HorizontalContentAlignment=HorizontalAlignment.Stretch;
   var body=new StackPanel{Margin=new(18,1,0,2)};var problems=group.Checks.Where(c=>c.Tone is "Warning" or "Error").ToList();bool auto=!active&&!expandedProblem&&problems.Count>0&&!expansion45.ContainsKey(key)&&seenProblems45.Add(key+"|"+string.Join(";",problems.Select(x=>x.Text.Split('·')[0])));
   if(auto)expandedProblem=true;bool open=expansion45.GetValueOrDefault(key,auto);exp.IsExpanded=open;
   void Fill(bool all){body.Children.Clear();foreach(var check in all?group.Checks:problems){body.Children.Add(new TextBlock{Text=(check.Tone=="Success"?"✓ ":check.Tone=="Error"?"✕ ":"• ")+check.Text,FontSize=11,Foreground=Theme.Tone(check.Tone),TextWrapping=TextWrapping.Wrap,Margin=new(0,1,0,1)});}}
   Fill(!auto);exp.Content=body;exp.Expanded+=(s,e)=>{if(rendering45)return;expansion45[key]=true;Fill(true);};exp.Collapsed+=(s,e)=>{if(!rendering45)expansion45[key]=false;};ReadinessTree45.Children.Add(exp);
  }
  rendering45=false;
 }
}
