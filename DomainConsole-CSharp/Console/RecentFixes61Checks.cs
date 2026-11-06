using System.IO;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRecentFixes61(){
  var parts=new List<(string Title,string Body)>{("Главное","Результат"),("Требуется клиенту","К установке: 0"),("Технические сведения · источник и актуальность","WSUS: server"),("Техническая статистика · все обновления","Установлено: 5"),("Актуальность снимка","Сохранено вчера"),("Последнее чтение WSUS","Клиент не найден")};
  foreach(bool omit in new[]{false,true}){
   var compact=LogViewer.CompactSections61(parts,omit);
   if(compact.Count(p=>p.Title=="Технические подробности")!=1||compact.Any(p=>p.Title=="Актуальность снимка")||compact.Any(p=>p.Title=="Главное")==omit||!compact.Last().Body.Contains("Клиент не найден"))throw new Exception("Compact metadata differs across viewers");
   var output=new List<(string Title,string Body)>{("Вывод команды","one line"),("Сбор диагностики","diagnostic"),("Технические подробности · вывод операции","raw"),("Предыдущее обслуживание","previous"),("Технические сведения",new string('x',5000))};
   if(!LogViewer.CompactSections61(output,omit).SequenceEqual(output))throw new Exception("Raw logs/previous operations were folded into metadata");
  }
  var cache=new CachedWsusReport{Found=true,Report=new(){LastSyncResult="Succeeded",NeededCount=0},LookupError53="Клиент не найден при последнем чтении",LookupUtc53=DateTimeOffset.UtcNow.ToString("o")};
  string report=CachedReportText(cache);
  if(report.IndexOf("Клиент не найден",StringComparison.Ordinal)>report.IndexOf("К установке:",StringComparison.Ordinal)||!report.Contains("## Последнее чтение WSUS"))throw new Exception("Cached zero hides latest lookup warning");
  var selected=ComputerGrid.SelectedItem;var tab=MainTabs.SelectedItem;var recent=recentHosts.ToList();var pinned=workHosts.ToList();int oldLimit=recentLimit;var overview=overviewRun61;bool running=overviewRunning;int theme=Theme.Current;
  var rows=Enumerable.Range(0,8).Select(i=>new ComputerRow(new(){Host="recent61-"+i+".invalid",Name="RECENT61-"+i})).ToList();
  var stale=new RemoteJob{Targets=new(){new(){Host=rows[7].Host,Status="Running",State=new(){Status="Running",Updated=DateTimeOffset.UtcNow.AddDays(-2).ToString("o")}}}};
  foreach(var row in rows)Computers.Add(row);History.Insert(0,stale);
  try{
   recentHosts.Clear();recentHosts.AddRange(rows.Take(6).Select(r=>r.Host));workHosts.Clear();recentLimit=5;
   RefreshRecent();if(RecentComputers.Count(r=>rows.Contains(r))!=5||RecentComputers.Contains(rows[7]))throw new Exception("Historical running target bypassed recent limit");
   workHosts.Add(rows[6].Host);RefreshRecent();if(!RecentComputers.Contains(rows[6])||rows[6].RecentReason61!="Закреплён вручную")throw new Exception("Pinned exception is not explicit");
   reportWaiting[rows[0].Host]=new(){Id="recent61",Server54=OverviewServer,Until=DateTimeOffset.UtcNow.AddSeconds(-1)};
   var criterion=new Criterion45("report","old","Success","Success",new());
   var expired=QueueCriterion52(rows[0],criterion,null);
   if(expired.Tone!="Warning"||!expired.Text.Contains("задерживается")||WorkState60(rows[0],true)?.Busy!=false)throw new Exception("Deadline needs a background tick to expire");
   bool paused=TransportBudget41.Paused;try{TransportBudget41.Paused=true;if(WorkState60(rows[0],true)?.Text.Contains("задерживается")!=true)throw new Exception("Pause hides expired deadline");}finally{TransportBudget41.Paused=paused;}
   if(AssessClientCore41(rows[0]).Kind!="Warning")throw new Exception("Expired wait did not reach shared status");
   reportWaiting[rows[0].Host].Until=DateTimeOffset.UtcNow.AddMinutes(5);
   if(AssessClientCore41(rows[0]).Kind!="ReportPending")throw new Exception("Report waiting is presented as full success");
   reportCache[rows[0].Host]=cache;cache.Server=OverviewServer;liveReports.Add(rows[0].Host);
   if(!CurrentReportText41(rows[0]).Contains("Клиент не найден при последнем чтении"))throw new Exception("Live cache hides failed later lookup");
   overviewRun61=new(){Server=OverviewServer,Phase="WSUS: уточнение одобрений",Total=400,Processed=120,Missing=3,Errors=2};overviewRunning=true;RenderOverview61();
   if(!OverviewBusy61.IsIndeterminate||OverviewBusy61.Visibility!=Visibility.Visible||!OverviewProgress.Text.Contains("120 / 400")||!OverviewDetailsText61().Contains("не найдено: 3"))throw new Exception("Overview progress/counts missing");
   ComputerGrid.SelectedItem=rows[0];MainTabs.SelectedItem=WsusTab;ShowCurrent();
   foreach(int color in new[]{0,1,2}){Theme.Apply(color,false);foreach(var size in new[]{(1000d,640d),(1920d,1080d)})CaptureContent38("recent61-"+color+"-"+size.Item1,size.Item1,size.Item2);}
   overviewRunning=false;RenderOverview61();if(OverviewBusy61.IsIndeterminate||OverviewBusy61.Visibility!=Visibility.Collapsed)throw new Exception("Completed overview still animates");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"ui-recent61.txt"),"PASS metadata in all viewers; preserve raw logs; failed last lookup; stale recent exclusion and pin; deadline without background tick; overview busy/completion; three themes/two sizes");
  }finally{
   reportWaiting.Remove(rows[0].Host);reportCache.Remove(rows[0].Host);liveReports.Remove(rows[0].Host);History.Remove(stale);foreach(var row in rows){Computers.Remove(row);criteria45.Remove(row.Host);}recentHosts.Clear();recentHosts.AddRange(recent);workHosts.Clear();workHosts.AddRange(pinned);recentLimit=oldLimit;overviewRun61=overview;overviewRunning=running;RenderOverview61();Theme.Apply(theme,false);ComputerGrid.SelectedItem=selected;MainTabs.SelectedItem=tab;RefreshRecent();ShowCurrent();
  }
 }
}
