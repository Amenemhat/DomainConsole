using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DomainConsole.Shared;
namespace DomainConsole;

public sealed class OverviewSettings {public bool Automatic {get;set;}=true;public int Hours {get;set;}=24;}
public sealed class OverviewReply {public string Host {get;set;}="";public bool Found {get;set;}public WsusReport? Report {get;set;}public string Error {get;set;}="";}
public sealed record OverviewResult(string Kind,string Hint);
public partial class MainWindow {
 readonly List<string> workHosts=Store.Read("work-computers.json",new List<string>());
 readonly HashSet<string> statusFilter=new(Store.Read("status-filter.json",new List<string>()),StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,string> overviewErrors=new(StringComparer.OrdinalIgnoreCase);
 OverviewSettings overviewSettings=Store.Read("overview-settings.json",new OverviewSettings());
 CancellationTokenSource? overviewCancel;bool overviewRunning,overviewPaused;DateTimeOffset nextOverview=DateTimeOffset.UtcNow.AddSeconds(3);
 string treeAnchor="",recentAnchor="";
 static readonly (string Key,string Label)[] StatusCategories={
  ("Error","Ошибка WSUS"),("Warning","Требует внимания"),("Reboot","Нужна перезагрузка"),("Success","Успешно · свежие данные"),("Stale","Успешно · прежние данные"),("NoWsus","Нет в WSUS"),("Unreachable","Нет связи WinRM"),("Access","Нет доступа"),("Unknown","Нет данных")};
 string OverviewServer=>WsusHost.Text.Trim()+":"+WsusPort.Text.Trim();
 bool CacheMatches(CachedWsusReport cached)=>cached.Server.Equals(OverviewServer,StringComparison.OrdinalIgnoreCase);
 OverviewResult OverviewStatus(ComputerRow row){
  reportCache.TryGetValue(row.Host,out var cached);if(cached!=null&&!CacheMatches(cached))cached=null;
  var kind=cached==null?"Unknown":!cached.Found?"NoWsus":cached.Report.Severity("");
  if(kind=="Success"&&(!liveReports.Contains(row.Host)||!DateTimeOffset.TryParse(cached!.ReceivedUtc,out var received)||DateTimeOffset.UtcNow-received>TimeSpan.FromHours(overviewSettings.Hours)))kind="Stale";
  var label=StatusCategories.FirstOrDefault(x=>x.Key==kind).Label??"Нет данных";
  string hint="WSUS: "+label;
  if(cached!=null){hint+="\nДанные сервера получены: "+LocalDateConverter.Format(cached.ReceivedUtc);if(cached.Found)hint+="\nПоследний отчёт клиента: "+LocalDateConverter.Format(cached.Report.LastReportedUtc);if(cached.Report.FailedCount>0)hint+="\nОшибок установки по данным сервера: "+cached.Report.FailedCount;}
  var wsus=History.FirstOrDefault(j=>IsWsusJob(j)&&j.Targets.Any(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)))?.Targets.First(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));
  if(wsus?.Status is "Failed" or "CompletedWithErrors" or "InterventionRequired" or "SubmissionUnknown"){
   var stamp=wsus.State?.Ended??wsus.State?.Updated??"";
   hint+="\nПоследнее задание WSUS: "+ComputerRow.StatusText(wsus.Status)+" · "+LocalDateConverter.Format(stamp);
   if(cached==null||(DateTimeOffset.TryParse(stamp,out var ended)&&DateTimeOffset.TryParse(cached.Report.LastReportedUtc,out var reported)&&ended>reported))kind=wsus.Status is "InterventionRequired" or "SubmissionUnknown"?"Warning":"Error";
  }
  if(wsus?.State?.RebootRequired==true||wsus?.Status is "AwaitingReboot" or "RebootScheduled"){
   hint+="\n"+(wsus.Status=="RebootScheduled"?"Перезагрузка назначена заданием":"Клиент сообщил: нужна перезагрузка");if(kind is not "Error")kind="Reboot";
  }
  if(HeaderRebootJob(row)!=null){hint+="\nПерезагрузка назначена из шапки; доступна отмена.";if(kind!="Error")kind="Reboot";}
  if(clientConnections.TryGetValue(row.Host,out var connection)&&connection.Error.Length>0){var access=AccessError(connection.Error);hint+="\n"+(access?"Нет доступа WinRM":"Нет связи WinRM (это не доказательство выключения ПК)")+" · "+connection.LastAttempt.ToLocalTime().ToString("dd.MM HH:mm:ss")+"\n"+ShortConnectionError(connection.Error);if(kind is not ("Error" or "Reboot"))kind=access?"Access":"Unreachable";}
  if(overviewErrors.TryGetValue(row.Host,out var error))hint+="\nПоследнее чтение WSUS не удалось: "+ShortConnectionError(error)+"\nПредыдущий результат сохранён.";
  return new(kind,(StatusCategories.FirstOrDefault(x=>x.Key==kind).Label??"Нет данных")+"\n"+hint);
 }
 static bool AccessError(string error)=>error.Contains("0x80070005",StringComparison.OrdinalIgnoreCase)||error.Contains("Access is denied",StringComparison.OrdinalIgnoreCase)||error.Contains("отказано в доступе",StringComparison.OrdinalIgnoreCase)||error.Contains("доступ запрещ",StringComparison.OrdinalIgnoreCase)||error.Contains("от имени администратора",StringComparison.OrdinalIgnoreCase);
 IEnumerable<string> StatusTags(ComputerRow row){var primary=OverviewStatus(row).Kind;yield return primary;if(reportCache.TryGetValue(row.Host,out var cache)&&CacheMatches(cache)){if(!cache.Found&&primary!="NoWsus")yield return "NoWsus";if(cache.Report.InstalledPendingRebootCount>0&&primary!="Reboot")yield return "Reboot";}if(HeaderRebootJob(row)!=null&&primary!="Reboot")yield return "Reboot";if(clientConnections.TryGetValue(row.Host,out var c)&&c.Error.Length>0){var tag=AccessError(c.Error)?"Access":"Unreachable";if(tag!=primary)yield return tag;}}
 bool MatchesSearch(ComputerRow row)=>(row.Name+" "+row.Host+" "+row.OS).Contains(SearchBox.Text,StringComparison.OrdinalIgnoreCase);
 bool MatchesNavigation(ComputerRow row)=>MatchesSearch(row)&&(statusFilter.Count==0||StatusTags(row).Any(statusFilter.Contains));
 List<ComputerRow> ScopedStatusRows()=>Computers.Where(MatchesNavigation).DistinctBy(r=>r.Host,StringComparer.OrdinalIgnoreCase).ToList();
 void RefreshStatusCounts(){if(StatusFilterButton==null)return;StatusFilterButton.Content=statusFilter.Count==0?"Статусы: все · "+Computers.Count(MatchesSearch):"Статусы: "+statusFilter.Count+" · ПК: "+ScopedStatusRows().Count;ApplyStatusVisibility();}
 void ApplyStatusVisibility(){
  bool Apply(TreeViewItem item){if(item.Tag is ComputerRow r){item.Visibility=MatchesNavigation(r)?Visibility.Visible:Visibility.Collapsed;return item.Visibility==Visibility.Visible;}bool any=false;foreach(TreeViewItem child in item.Items)any=Apply(child)||any;item.Visibility=any||item==OuTree.Items.Cast<object>().FirstOrDefault()?Visibility.Visible:Visibility.Collapsed;return any;}
  foreach(TreeViewItem root in OuTree.Items)Apply(root);
 }
 void StatusFilter_Click(object sender,RoutedEventArgs e){
  var menu=new ContextMenu();var all=new MenuItem{Header="Все статусы · "+Computers.Count(MatchesSearch)};all.Click+=(s,a)=>{statusFilter.Clear();RefreshStatusCounts();RefreshRecent();};menu.Items.Add(all);menu.Items.Add(new Separator());
  foreach(var category in StatusCategories){var key=category.Key;var item=new MenuItem{Header=category.Label+" · "+Computers.Count(r=>MatchesSearch(r)&&StatusTags(r).Contains(key)),IsCheckable=true,IsChecked=statusFilter.Contains(key),StaysOpenOnClick=true};item.Click+=(s,a)=>{if(item.IsChecked)statusFilter.Add(key);else statusFilter.Remove(key);RefreshStatusCounts();RefreshRecent();};menu.Items.Add(item);}
  menu.Items.Add(new Separator());var add=new MenuItem{Header="Добавить показанные в работу…"};add.Click+=(s,a)=>AddStatusRows(false);menu.Items.Add(add);var check=new MenuItem{Header="Отметить показанные галочками…"};check.Click+=(s,a)=>AddStatusRows(true);menu.Items.Add(check);var problems=new MenuItem{Header="Добавить все проблемы в работу…"};problems.Click+=(s,a)=>AddStatusRows(false,true);menu.Items.Add(problems);
  menu.Items.Add(new MenuItem{Header="Счётчики: все загруженные ПК с учётом поиска. Категории могут пересекаться.",IsEnabled=false});menu.PlacementTarget=StatusFilterButton;menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;
 }
 void AddStatusRows(bool mark,bool problems=false){var rows=problems?Computers.Where(r=>MatchesSearch(r)&&StatusTags(r).Any(t=>t is "Error" or "Warning" or "Reboot" or "NoWsus" or "Unreachable" or "Access")).ToList():ScopedStatusRows();
  if(rows.Count==0){Notify("По выбранным статусам компьютеров нет.");return;}
  var window=new Window{Owner=this,Title=mark?"Отметить компьютеры":"Добавить в работу",Width=490,Height=450,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new DockPanel{Margin=new(16)};window.Content=panel;var info=new TextBlock{Text="Компьютеров: "+rows.Count+". Область: все загруженные ПК с учётом поиска"+(problems?"; проблемы":"; выбранные статусы")+". Подразделение не ограничивает этот список.\nЗадания не запускаются."+(mark?" Галочки остальных ПК сохранятся.":" Галочки не меняются."),TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,8)};DockPanel.SetDock(info,Dock.Top);panel.Children.Add(info);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);var yes=new Button{Content=mark?"Отметить":"Добавить",IsDefault=true};yes.Click+=(s,a)=>window.DialogResult=true;buttons.Children.Add(yes);buttons.Children.Add(new Button{Content="Отмена",IsCancel=true});panel.Children.Add(new ListBox{ItemsSource=rows.Select(r=>r.Host+" · "+OverviewStatus(r).Hint.Split('\n')[0]).ToList()});if(window.ShowDialog()!=true)return;
  if(mark){batchSelecting=true;try{foreach(var row in rows)row.Selected=true;}finally{batchSelecting=false;}UpdateSelectionCount();}else{var added=rows.Select(r=>r.Host).Distinct(StringComparer.OrdinalIgnoreCase).ToList();workHosts.RemoveAll(h=>added.Contains(h,StringComparer.OrdinalIgnoreCase));workHosts.InsertRange(0,added);Store.Write("work-computers.json",workHosts);statusFilter.Clear();RefreshStatusCounts();RecentGroup.IsExpanded=true;RefreshRecent();}Notify((mark?"Отмечено: ":"Добавлено в работу: ")+rows.Count);
 }
 void SetupOverview(){overviewSettings.Hours=Math.Clamp(overviewSettings.Hours,1,168);OverviewAutomatic.IsChecked=overviewSettings.Automatic;OverviewHours.Text=overviewSettings.Hours.ToString();RequestOverviewSoon();}
 void RequestOverviewSoon(){nextOverview=DateTimeOffset.UtcNow.AddSeconds(3);}
 void OverviewTick(){RefreshOutcomes();if(!closing&&overviewSettings.Automatic&&!overviewPaused&&!overviewRunning&&DateTimeOffset.UtcNow>=nextOverview&&!string.IsNullOrWhiteSpace(WsusHost.Text))_=RunOverview();}
 async void OverviewRefresh_Click(object sender,RoutedEventArgs e){if(overviewRunning){overviewPaused=true;overviewCancel?.Cancel();OverviewProgress.Text="Остановка обзора…";return;}overviewPaused=false;await RunOverview();}
 void SaveOverview_Click(object sender,RoutedEventArgs e)=>Guard(()=>{if(!int.TryParse(OverviewHours.Text,out var hours)||hours<1||hours>168)throw new Exception("Интервал: 1–168 часов.");overviewSettings=new(){Automatic=OverviewAutomatic.IsChecked==true,Hours=hours};Store.Write("overview-settings.json",overviewSettings);if(!overviewSettings.Automatic){overviewCancel?.Cancel();}else{overviewPaused=false;RequestOverviewSoon();}Notify("Настройки обзора WSUS сохранены.");});
 async Task RunOverview(){if(overviewRunning||closing)return;WsusSettings settings;try{settings=ReadWsus();}catch(Exception ex){OverviewProgress.Text=ex.Message;nextOverview=DateTimeOffset.UtcNow.AddMinutes(15);return;}
  var server=settings.Host+":"+settings.Port;var rows=Computers.OrderBy(r=>{int i=recentHosts.FindIndex(h=>h.Equals(r.Host,StringComparison.OrdinalIgnoreCase));return i<0?int.MaxValue:i;}).DistinctBy(r=>r.Host,StringComparer.OrdinalIgnoreCase).ToList();if(rows.Count==0)return;
  overviewRunning=true;using var cancellation=new CancellationTokenSource();overviewCancel=cancellation;OverviewRefresh.Content="Остановить обзор";int done=0,failed=0;bool cancelled=false;
  try{foreach(var chunk in rows.Chunk(10)){if(closing||cancellation.IsCancellationRequested){cancelled=true;break;}OverviewProgress.Text="Обзор WSUS: "+done+" / "+rows.Count+" · только чтение сервера";
    List<OverviewReply> replies;try{replies=await remote.WsusOverview(settings,chunk.Select(r=>r.Host).ToList(),cancellation.Token);}catch(Exception ex){if(cancellation.IsCancellationRequested){cancelled=true;break;}foreach(var row in chunk)overviewErrors[row.Host]=ex.Message;failed+=chunk.Length;done+=chunk.Length;OverviewProgress.Text="Сервер WSUS недоступен: "+ShortConnectionError(ex.Message)+". Сохранённые данные оставлены.";break;}
    if(cancellation.IsCancellationRequested||closing||server!=OverviewServer){cancelled=true;break;}
    foreach(var row in chunk){var reply=replies.FirstOrDefault(r=>r.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));if(reply==null||reply.Error.Length>0){failed++;overviewErrors[row.Host]=reply?.Error??"Нет результата в ответе сервера";continue;}overviewErrors.Remove(row.Host);if(reply.Found&&reply.Report!=null){var started=reportCache.GetValueOrDefault(row.Host)?.Started??"";SaveReportCache(row.Host,reply.Report,server,started,false);serverReports[row.Host]=reply.Report.Format(server,started);reportSeverity[row.Host]=reply.Report.Severity(started);}else if(!reply.Found){reportCache[row.Host]=new(){Found=false,Server=server,ReceivedUtc=DateTime.UtcNow.ToString("o")};liveReports.Add(row.Host);serverReports[row.Host]="Сервер WSUS: "+server+"\nКлиент: "+row.Host+"\nКомпьютер не найден в WSUS. Доступность клиента этим запросом не проверялась.";reportSeverity[row.Host]="Unknown";}}
    Store.Write("wsus-report-cache.json",reportCache);done+=chunk.Length;RefreshOutcomes();if(ComputerGrid.SelectedItem is ComputerRow focused&&chunk.Contains(focused))ShowCurrent();await Task.Delay(TimeSpan.FromSeconds(2),cancellation.Token);
   }
  }catch(OperationCanceledException){cancelled=true;}catch(Exception ex){failed++;OverviewProgress.Text="Обзор остановлен: "+ShortConnectionError(ex.Message);}
  finally{overviewRunning=false;overviewCancel=null;OverviewRefresh.Content="Обновить статусы WSUS";nextOverview=DateTimeOffset.UtcNow.Add(server!=OverviewServer?TimeSpan.FromSeconds(3):failed>0?TimeSpan.FromMinutes(15):TimeSpan.FromHours(overviewSettings.Hours));if(!closing){OverviewProgress.Text=(cancelled?"Обзор остановлен":failed>0?"Обзор неполный":"Обзор завершён")+" · "+done+" / "+rows.Count+(failed>0?"; ошибок чтения: "+failed:"");RefreshOutcomes();}}
 }
 static bool IsCheckSource(object source){for(var d=source as DependencyObject;d!=null;d=d is Visual?VisualTreeHelper.GetParent(d):LogicalTreeHelper.GetParent(d)){if(d is CheckBox)return true;if(d is TreeViewItem or ListBoxItem)break;}return false;}
 List<ComputerRow> VisibleTreeRows(){var result=new List<ComputerRow>();void Walk(ItemsControl parent){foreach(TreeViewItem item in parent.Items){if(item.Visibility!=Visibility.Visible)continue;if(item.Tag is ComputerRow row)result.Add(row);if(item.IsExpanded)Walk(item);}}Walk(OuTree);return result;}
 void MarkRange(ComputerRow row,List<ComputerRow> visible,bool value,bool shift,bool recent){var anchor=recent?recentAnchor:treeAnchor;int a=visible.FindIndex(r=>r.Host.Equals(anchor,StringComparison.OrdinalIgnoreCase)),b=visible.IndexOf(row);batchSelecting=true;try{if(shift&&a>=0&&b>=0){foreach(var item in visible.Skip(Math.Min(a,b)).Take(Math.Abs(b-a)+1))item.Selected=value;}else{row.Selected=value;if(recent)recentAnchor=row.Host;else treeAnchor=row.Host;}}finally{batchSelecting=false;}}
 void Recent_PreviewMouseDown(object sender,MouseButtonEventArgs e){if(IsCheckSource(e.OriginalSource)||!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))return;var item=ItemsControl.ContainerFromElement(RecentList,e.OriginalSource as DependencyObject) as ListBoxItem;if(item?.DataContext is not ComputerRow row)return;MarkRange(row,RecentComputers.ToList(),!row.Selected,false,true);ComputerGrid.SelectedItem=row;RecentList.SelectedItem=row;UpdateSelectionCount();ShowCurrent();e.Handled=true;}
 void RefreshRebootHeader(){if(HeaderReboot==null)return;if(ComputerGrid.SelectedItem is not ComputerRow row){HeaderReboot.IsEnabled=false;HeaderAbortReboot.Visibility=Visibility.Collapsed;HeaderWsusStatus.Text="";return;}HeaderReboot.IsEnabled=true;var status=OverviewStatus(row);HeaderWsusStatus.Text=status.Hint.Split('\n')[0];HeaderWsusStatus.ToolTip=status.Hint;HeaderWsusStatus.Foreground=status.Kind switch {"Error"=>Brushes.Firebrick,"Warning" or "Reboot"=>Brushes.DarkGoldenrod,"Success"=>Brushes.ForestGreen,"Stale"=>Brushes.SteelBlue,_=>Brushes.SlateGray};var scheduled=HeaderRebootJob(row);HeaderAbortReboot.Visibility=scheduled!=null?Visibility.Visible:Visibility.Collapsed;HeaderReboot.Content=scheduled!=null?"⟳ Перезагрузка назначена…":StatusTags(row).Contains("Reboot")?"⟳ Нужна перезагрузка…":"⟳ Перезагрузить…";HeaderReboot.ToolTip="Перезагрузка только текущего ПК: "+row.Host+". Задержка и сообщение выбираются в подтверждении; принудительное закрытие приложений выключено.";}
 async void HeaderReboot_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{var row=Current();if(HeaderRebootJob(row)!=null){Notify("Перезагрузка уже назначена. Для изменения сначала отмените её.");return;}var window=new Window{Owner=this,Title="Перезагрузка · "+row.Host,Width=540,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new(20)};window.Content=panel;panel.Children.Add(new TextBlock{Text="Только текущий компьютер: "+row.Host,FontWeight=FontWeights.SemiBold});panel.Children.Add(new TextBlock{Text="Остальные галочки не влияют. Сохраните документы на клиенте.",TextWrapping=TextWrapping.Wrap});panel.Children.Add(new TextBlock{Text="Задержка в секундах (0–86400)"});var delay=new TextBox{Text="60"};panel.Children.Add(delay);var message=new TextBox{Text="Плановая перезагрузка. Сохраните документы.",MaxLength=400};panel.Children.Add(message);var force=new CheckBox{Content="Принудительно закрыть приложения (возможна потеря данных)"};panel.Children.Add(force);var actions=new WrapPanel();panel.Children.Add(actions);var run=new Button{Content="Перезагрузить",IsDefault=true};actions.Children.Add(run);actions.Children.Add(new Button{Content="Отмена",IsCancel=true});run.Click+=(s,a)=>{if(!int.TryParse(delay.Text,out var seconds)||seconds<0||seconds>86400){MessageBox.Show(window,"Укажите задержку 0–86400 секунд.");return;}window.DialogResult=true;};if(window.ShowDialog()!=true)return;
  // Windows shutdown /t > 0 implies /f. Delay in the agent script keeps force-close opt-in.
  int wait=int.Parse(delay.Text);var code=HeaderRebootCode(wait,message.Text,force.IsChecked==true);
  // Direct independent control lane, exactly the captured host, no second target picker.
  await SubmitHeaderReboot(row,code);
 });
 public static string HeaderRebootCode(int wait,string message,bool force){
  if(wait<0||wait>86400)throw new ArgumentOutOfRangeException(nameof(wait));
  string text=message.Replace("\r"," ").Replace("\n"," ");
  return "Write-Output "+PowerShellBridge.Literal("Перезагрузка через "+wait+" сек: "+text)+";"+
   (wait>0?"try { & msg.exe * /time:"+Math.Min(wait,300)+" "+PowerShellBridge.Literal("Перезагрузка через "+wait+" секунд. "+text)+" 2>$null | Out-Null } catch { Write-Output 'Сообщение пользователю доставить не удалось.' };Start-Sleep -Seconds "+wait+";":"")+
   "& shutdown.exe /r /t 0 "+(force?"/f ":"")+"/c "+PowerShellBridge.Literal(text)+";if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}";
 }
 RemoteJob? HeaderRebootJob(ComputerRow row)=>History.FirstOrDefault(j=>j.Name=="Перезагрузка текущего компьютера"&&j.Targets.Any(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)&&t.Status is "Queued" or "Sending" or "Submitted" or "Running"));
 async void HeaderAbortReboot_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{var row=Current();var job=HeaderRebootJob(row);if(job==null){Notify("Нет назначенной этой кнопкой перезагрузки.");return;}if(MessageBox.Show(this,"Отменить перезагрузку "+row.Host+"?", "Перезагрузка",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;var target=job.Targets.First(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));if(target.Status=="Queued"&&target.State==null){var keep=queue.Where(x=>x.Job!=job||x.Target!=target).ToList();queue.Clear();foreach(var item in keep)queue.Enqueue(item);target.Status="Cancelled";Save();RefreshOutcomes();}else{await remote.Cancel(target,job.Id,"now");await Poll(new ComputerRow(target,job.Id));}Notify("Запрошена отмена назначенной перезагрузки. Проверьте результат задания.");});
 async Task SubmitHeaderReboot(ComputerRow row,string code){historyViewId="";var job=new RemoteJob{Name="Перезагрузка текущего компьютера",OutputChannel="Commands",IndependentControl=true,Steps=new(){new(){Name="Перезагрузка текущего компьютера",Kind="PowerShell",Code=code}},Targets=new(){new(){Name=row.Name,Host=row.Host,OU=row.OU,OS=row.OS,Status="Queued"}}};History.Insert(0,job);queue.Enqueue((job,job.Targets[0]));confirmedJobs.Add(job.Id+row.Host);RememberComputer(row.Host);Save();Sync(job);ShowCurrent();await SendQueue();}
}
