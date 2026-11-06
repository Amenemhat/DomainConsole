using System.IO;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRecentFixes61(){
  var branchRow61=new ComputerRow(new(){Host="branch61.invalid"});var branch61=new OuBranchState("fixture","fixture",new(){branchRow61});int branchChanges61=0;branch61.PropertyChanged+=(s,e)=>branchChanges61++;
  branch61.Refresh(new());branch61.Refresh(new());if(branchChanges61!=0)throw new Exception("Unchanged branch broadcasts repaint notifications");
  branchRow61.Selected=true;branch61.Refresh(new(){branchRow61.Host});if(branch61.Selected!=true||!branch61.Busy||branchChanges61==0)throw new Exception("Branch selection/activity change not announced");
  string waitId61=Guid.NewGuid().ToString();
  var parts=new List<(string Title,string Body)>{("Главное","Результат"),("Требуется клиенту","К установке: 0"),("Технические сведения · источник и актуальность","WSUS: server"),("Техническая статистика · все обновления","Установлено: 5"),("Актуальность снимка","Сохранено вчера"),("Последнее чтение WSUS","Клиент не найден")};
  foreach(bool omit in new[]{false,true}){
   var compact=LogViewer.CompactSections61(parts,omit);
   if(compact.Count(p=>p.Title=="Технические подробности")!=1||compact.Any(p=>p.Title=="Актуальность снимка")||compact.Any(p=>p.Title=="Главное")==omit||!compact.Last().Body.Contains("Клиент не найден"))throw new Exception("Compact metadata differs across viewers");
   var viewer=new LogViewer{OmitMain46=omit,Text=string.Join("\n\n",parts.Select(p=>"## "+p.Title+"\n"+p.Body))};viewer.SetExpanded("Технические подробности",true);
   if(!viewer.VisibleText().Contains("Клиент не найден")||!viewer.VisibleText().Contains("WSUS: server"))throw new Exception("Compacted metadata lost copyable content");
   var output=new List<(string Title,string Body)>{("Вывод команды","one line"),("Сбор диагностики","diagnostic"),("Технические подробности · вывод операции","raw"),("Предыдущее обслуживание","previous"),("Технические сведения",new string('x',5000))};
   if(!LogViewer.CompactSections61(output,omit).SequenceEqual(output))throw new Exception("Raw logs/previous operations were folded into metadata");
  }
  var separate=new List<(string Title,string Body)>{("Технические сведения","source A"),("Вывод команды","A"),("Технические сведения","source B")};
  if(!LogViewer.CompactSections61(separate,false).SequenceEqual(separate))throw new Exception("Independent sources were combined");
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
   stale.Targets[0].State!.Updated=DateTimeOffset.UtcNow.ToString("o");if(WorkState60(rows[7],false)?.Busy!=false||ActiveRecent61().Contains(rows[7].Host))throw new Exception("Unconfirmed restored state animates as live");
   var completed61=new RemoteJob{OutputChannel=stale.OutputChannel,Targets=new(){new(){Host=rows[7].Host,Status="Completed",State=new(){Status="Completed"}}}};History.Insert(0,completed61);try{if(WorkState60(rows[7],false)!=null)throw new Exception("Old unknown Running overrides newer completion");}finally{History.Remove(completed61);}
   workHosts.Add(rows[6].Host);RefreshRecent();if(!RecentComputers.Contains(rows[6])||rows[6].RecentReason61!="Закреплён вручную")throw new Exception("Pinned exception is not explicit");
   reportWaiting[rows[0].Host]=new(){Id=waitId61,Server54=OverviewServer,Next=DateTimeOffset.UtcNow.AddHours(1),Until=DateTimeOffset.UtcNow.AddSeconds(-1)};
   var criterion=new Criterion45("report","old","Success","Success",new());
   var expired=QueueCriterion52(rows[0],criterion,null);
   if(expired.Tone!="Warning"||!expired.Text.Contains("задерживается")||WorkState60(rows[0],true)?.Busy!=false)throw new Exception("Deadline needs a background tick to expire");
   bool paused=TransportBudget41.Paused;try{TransportBudget41.Paused=true;ExpireReportWaits61();ExpireReportWaits61();if(!reportWaiting[rows[0].Host].TimeoutRecorded61||AllEvents32(rows[0].Host).Count(e=>e.Operation36=="report-delay54|"+waitId61&&e.Text=="Отчёт WSUS задерживается")!=1)throw new Exception("Paused deadline missing or repeated in history");if(WorkState60(rows[0],true)?.Text.Contains("задерживается")!=true)throw new Exception("Pause hides expired deadline");}finally{TransportBudget41.Paused=paused;}
   if(AssessClientCore41(rows[0]).Kind!="Warning")throw new Exception("Expired wait did not reach shared status");
   reportWaiting[rows[0].Host].Requested54=DateTimeOffset.UtcNow.AddMinutes(-10);
   RecordReportReceipt54(rows[0].Host,new(){LastReportedUtc=DateTimeOffset.UtcNow.AddMinutes(-20).ToString("o")},OverviewServer);
   if(!reportWaiting.ContainsKey(rows[0].Host))throw new Exception("Reading an old report now completed waiting");
   RecordReportReceipt54(rows[0].Host,new(){LastReportedUtc=DateTimeOffset.UtcNow.ToString("o")},OverviewServer);
   if(reportWaiting.ContainsKey(rows[0].Host)||!AllEvents32(rows[0].Host).Any(e=>e.Operation36=="report-late61|"+waitId61&&e.Text=="Получен отчёт WSUS после срока ожидания"))throw new Exception("Late report did not close waiting with a distinct event");
   reportWaiting[rows[0].Host]=new(){Id=waitId61,Server54=OverviewServer,Next=DateTimeOffset.UtcNow.AddHours(1),Until=DateTimeOffset.UtcNow.AddMinutes(5)};
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
