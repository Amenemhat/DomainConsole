using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ClientEvent31 {
 [System.Text.Json.Serialization.JsonIgnore] public List<ClientEvent31> Occurrences36 {get;set;}=new();
 public string Operation36 {get;set;}="";
 [System.Text.Json.Serialization.JsonIgnore] public bool Selected36 {get;set;}
 [System.Text.Json.Serialization.JsonIgnore] public string OccurrenceText36=>Repeats<=1?"":string.Join(Environment.NewLine,Occurrences36.Select(e=>Display36.Date(e.At,true)+" · "+e.VisibleTitle40));
 [System.Text.Json.Serialization.JsonIgnore] public string Time38=>DateTimeOffset.TryParse(At,out var time)?time.ToLocalTime().Date==DateTime.Now.Date?time.ToLocalTime().ToString("HH:mm"):time.ToLocalTime().ToString("dd.MM HH:mm"):"—";
 [System.Text.Json.Serialization.JsonIgnore] public Brush GlyphBrush39=>Kind=="Warning"?Theme.Brush("AttentionIcon39"):Brush;
 [System.Text.Json.Serialization.JsonIgnore] public string Glyph38=>Kind switch {"Success"=>"Success","Warning"=>"Warning","Error"=>"Error",_=>"Info"};
 [System.Text.Json.Serialization.JsonIgnore] public bool PausedProbe41=>Kind is "Error" or "Warning"&&Details.Contains("DC_PROBE_PAUSED:",StringComparison.Ordinal);
 [System.Text.Json.Serialization.JsonIgnore] public bool ShortTransport40=>PausedProbe41||Operation36.StartsWith("read-",StringComparison.Ordinal)&&(Kind is "Error" or "Warning")&&(Details.Contains("WinRM",StringComparison.OrdinalIgnoreCase)||Details.Contains("WSMan",StringComparison.OrdinalIgnoreCase))||Operation36=="link"&&Kind is "Error" or "Warning"||Text.StartsWith("Этап WinRM/аутентификации:")||Text.StartsWith("Ошибка WinRM/аутентификации");
 [System.Text.Json.Serialization.JsonIgnore] public string VisibleTitle40=>PausedProbe41?"Служебный опрос приостановлен":ShortTransport40?"Ошибка WinRM/аутентификации":Text;
 [System.Text.Json.Serialization.JsonIgnore] public string Body38=>Display36.Text(VisibleTitle40)+(!ShortTransport40&&Details.Length>0&&Details!=Text?"\n"+Display36.Text(Details):"")+(Repeats>1?" · повторов: "+Repeats+" · с "+Display36.Date(FirstAt):"");
 public int Repeats {get;set;}=1;public string FirstAt {get;set;}="";public string At {get;set;}=DateTime.UtcNow.ToString("o");public string Kind {get;set;}="Info";public string Text {get;set;}="";public string Details {get;set;}="";
 [System.Text.Json.Serialization.JsonIgnore] public string Display=>Display36.Date(At)+"  "+VisibleTitle40+(Repeats>1?" · повторов: "+Repeats+" · с "+Display36.Date(FirstAt):"");
 [System.Text.Json.Serialization.JsonIgnore] public Brush Brush=>Theme.Tone(Kind);
}
public partial class MainWindow {
 bool AutoServiceEnabled=Store.Read("auto-service31.json",true);
 readonly Dictionary<string,string> eventSignatures31=Store.Read("event-signatures31.json",new Dictionary<string,string>());
 readonly Dictionary<string,List<ClientEvent31>> recentEvents31=new(StringComparer.OrdinalIgnoreCase);
 bool checkingLinks31;readonly HashSet<string> manualLinkReads31=new(StringComparer.OrdinalIgnoreCase);
 int LinkMinutes31=>Math.Clamp(Store.Read("link-interval31.json",10),1,1440);
 static string EventFolder31(string host)=>Path.Combine(Store.Root,"Events",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(host.ToUpperInvariant()))));
 void Event31(string host,string category,string signature,string text,string kind="Info",string details="",string at=""){
  var key=host.ToUpperInvariant()+"|"+category;if(eventSignatures31.GetValueOrDefault(key)==signature)return;eventSignatures31[key]=signature;
  var entry=new ClientEvent31{Operation36=category,Kind=kind,Text=text,Details=details==text?"":details,At=DateTimeOffset.TryParse(at,out var observed)?observed.ToUniversalTime().ToString("o"):DateTime.UtcNow.ToString("o")};if(!recentEvents31.TryGetValue(host,out var list)){list=SafeEvents32(host,3);recentEvents31[host]=list;}list=CompactEvents34(list.Concat(memoryEvents32.GetValueOrDefault(host)??new()).Append(entry));recentEvents31[host]=list;list.Sort((a,b)=>string.CompareOrdinal(b.At,a.At));if(list.Count>3)list.RemoveRange(3,list.Count-3);
  if(!memoryEvents32.TryGetValue(host,out var memory)){memory=new();memoryEvents32[host]=memory;}groupedEvents39.Remove(host);memory.Insert(0,entry);if(memory.Count>500)memory.RemoveRange(500,memory.Count-500);
  if(!pendingEvents32.TryGetValue(host,out var pending)){pending=new();pendingEvents32[host]=pending;}pending.Add(entry);
  var batch=pending.ToList();pending.Clear();eventWrites39[host]=WriteEvents39(host,batch,eventWrites39.GetValueOrDefault(host)??Task.CompletedTask,Math.Clamp(Store.Read("event-days31.json",30),1,365));

 }
 static List<ClientEvent31> ReadEvents31(string host,int limit){var result=new List<ClientEvent31>();var folder=EventFolder31(host);if(!Directory.Exists(folder))return result;foreach(var file in Directory.GetFiles(folder,"*.jsonl").OrderByDescending(f=>Path.GetFileName(f))){foreach(var line in File.ReadLines(file).TakeLast(limit).Reverse()){try{var e=JsonSerializer.Deserialize<ClientEvent31>(line);if(e!=null){if(e.Details==e.Text)e.Details="";result.Add(e);}}catch(JsonException){}if(result.Count>=limit)return result;}}return result;}

 public static string ConnectionBrief31(string error){if(error.Contains("WinRM",StringComparison.OrdinalIgnoreCase)||error.Contains("WSMan",StringComparison.OrdinalIgnoreCase))return "Ошибка WinRM/аутентификации";if(string.IsNullOrWhiteSpace(error))return "Причина не получена";if(AccessError(error))return "Отказано в доступе WinRM; проверьте права и учётную запись";if(error.Contains("отмен",StringComparison.OrdinalIgnoreCase)||error.Contains("canceled",StringComparison.OrdinalIgnoreCase)||error.Contains("вовремя",StringComparison.OrdinalIgnoreCase))return "Нет ответа за время проверки; выполнение задания этим не отменено";var text=error.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"Нет ответа";return text.Length>140?text[..140]+"…":text;}
 async void HeaderLink_Click(object sender,RoutedEventArgs e){if(ComputerGrid.SelectedItem is not ComputerRow row||!manualLinkReads31.Add(row.Host))return;var started38=DateTimeOffset.UtcNow;HeaderLink.IsEnabled=false;HeaderLinkText.Text="Проверка…";try{if(!await EnsureEndpointCredentials(new(){row}))return;await remote.CheckConnection(ConnectionStore.Capture(row.Target));ConnectionSucceeded(row.Host,started38);await ReadClientStatus(row,force:true);}catch(Exception ex){ConnectionFailed(row.Host,ex.Message,started38);}finally{manualLinkReads31.Remove(row.Host);HeaderLink.IsEnabled=true;RefreshOutcomes();if(IsCurrent(row))UpdateWsusStage();}}
 async void HeaderAddress_Click(object sender,RoutedEventArgs e){if(ComputerGrid.SelectedItem is ComputerRow row)await EditConnection(row,"");}
 async Task PollBackgroundLinks31(){if(checkingLinks31||closing||overviewRunning||!Store.Read("link-automatic31.json",true))return;checkingLinks31=true;try{foreach(var row in Computers.Where(r=>!r.Target.AdDisabled).Where(r=>!clientConnections.TryGetValue(r.Host,out var c)||DateTimeOffset.UtcNow-c.LastAttempt>(c.Error.Length>0||c.Lost38?TimeSpan.FromSeconds(15):TimeSpan.FromMinutes(LinkMinutes31))).OrderBy(r=>clientConnections.GetValueOrDefault(r.Host)?.LastAttempt??DateTimeOffset.MinValue).Take(2).ToList()){if(closing)return;var started38=DateTimeOffset.UtcNow;try{await remote.CheckConnection(ConnectionStore.Capture(row.Target));ConnectionSucceeded(row.Host,started38);}catch(Exception ex){ConnectionFailed(row.Host,ex.Message,started38);}RefreshOutcomes();}}finally{checkingLinks31=false;}}
 void AutoService_Changed(object sender,RoutedEventArgs e){if(!IsLoaded)return;AutoServiceEnabled=AutoServiceToggle.IsChecked==true;Store.Write("auto-service31.json",AutoServiceEnabled);if(!AutoServiceEnabled){foreach(var p in maintenancePlans.Values.Where(p=>!p.Done)){p.Done=true;p.Phase="Автоматическое продолжение выключено";foreach(var id in new[]{p.JobId,p.NextJobId}){var job=History.FirstOrDefault(j=>j.Id==id&&j.AutomaticChild31);if(job==null)continue;foreach(var t in job.Targets.Where(t=>t.Status=="Queued"&&t.State==null))t.Status="Cancelled";}}var keep=queue.Where(q=>q.Target.Status!="Cancelled").ToList();queue.Clear();foreach(var q in keep)queue.Enqueue(q);foreach(var w in rebootWatches.Values.Where(w=>!w.Done))w.Scan=false;SaveMaintenance();Save();}RefreshAttentionButton();Notify(AutoServiceEnabled?"Автообслуживание включено для новых действий WSUS":"Будущие автоматические шаги выключены. Начатые операции продолжаются.");}
 void SaveStatus31_Click(object sender,RoutedEventArgs e)=>Guard(()=>{if(!int.TryParse(LinkInterval31.Text,out var minutes)||minutes<1||minutes>1440||!int.TryParse(EventDays31.Text,out var days)||days<1||days>365)throw new Exception("Опрос: 1–1440 минут; история: 1–365 дней");Store.Write("link-interval31.json",minutes);Store.Write("event-days31.json",days);Store.Write("link-automatic31.json",LinkAutomatic31.IsChecked==true);Notify("Параметры связи и истории сохранены.");});
 void RestoreAutoMaintenance31(){if(!AutoServiceEnabled)return;foreach(var entry in rebootWatches.Where(x=>x.Value.Done&&x.Value.Scan&&x.Value.FollowupId.Length>0)){if(maintenancePlans.ContainsKey(entry.Key))continue;var job=History.FirstOrDefault(j=>j.Id==entry.Value.FollowupId);var target=job?.Targets.FirstOrDefault(t=>t.Host.Equals(entry.Key,StringComparison.OrdinalIgnoreCase));if(target?.State?.UpdatesRemaining>0&&target.Status=="Completed"&&DateTimeOffset.TryParse(target.State.Ended,out var ended)&&DateTimeOffset.UtcNow-ended<TimeSpan.FromDays(2)&&History.FirstOrDefault(j=>IsWsusJob(j)&&j.Targets.Any(t=>t.Host.Equals(entry.Key,StringComparison.OrdinalIgnoreCase)))?.Id==job!.Id)BeginMaintenance(job);}}
 void RefreshHeader31(ComputerRow row){
  RefreshRebootHeader();RefreshAddress40(row);HeaderAddress40.Text=row.Address40;HeaderAddress40.ToolTip=row.AddressHint40;HeaderAddress40.Visibility=row.AddressVisibility40;
  CurrentDetails.Text=row.OS;CurrentDetails.ToolTip=row.OS+"\n"+row.Host+ConnectionStore.Description(row.Host);
  HeaderLinkGlyph.Kind=LinkKind(row.Host);HeaderLinkGlyph.Tint=LinkKind(row.Host)=="Connected"?Theme.Tone("Success"):LinkKind(row.Host) is "Access" or "Delayed"?Theme.Tone("Warning"):Theme.Brush("TextMuted");
  HeaderLinkText.Text=LinkKind(row.Host) switch {"Connected"=>"Связь есть","SavedConnected"=>"Был доступен","Delayed"=>"Ответ задерживается","Access"=>"Нет доступа","Unreachable"=>"Нет ответа",_=>"Связь не проверена"};HeaderLink.ToolTip=LinkHint(row);
  HeaderTimes31.Text=(clientConnections.TryGetValue(row.Host,out var link)&&link.LastSuccess!=default?"Ответ: "+Age(DateTimeOffset.UtcNow-link.LastSuccess)+(DateTimeOffset.UtcNow-link.LastSuccess<TimeSpan.FromMinutes(1)?"  ·  ":" назад  ·  "):"")+UptimeText(row.Host);HeaderTimes31.ToolTip=UptimeHint(row.Host);
  RefreshClientReport35(row);
  HeaderUpdateCount31.Text=UpdateCountText31(row.Host);HeaderUpdateCount31.Foreground=UpdateCountBrush31(row.Host);
  var job=ChannelJob(row,MainTabs.SelectedItem==WsusTab);var t=job?.Targets.FirstOrDefault(t=>t.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase));
  RefreshServiceBadge34(row);QueueLayout39();
  if(!recentEvents31.TryGetValue(row.Host,out var list)){list=SafeEvents32(row.Host,3);recentEvents31[row.Host]=list;}var key=string.Join("|",list.Select(x=>x.At+x.Text));if(headerEventsKey32!=row.Host+key){bool animate=headerEventsHost32==row.Host;headerEventsHost32=row.Host;headerEventsKey32=row.Host+key;HeaderRecent31.ItemsSource=list.ToList();if(animate){HeaderRecent31.RenderTransform=new TranslateTransform();HeaderRecent31.RenderTransform.BeginAnimation(TranslateTransform.YProperty,new System.Windows.Media.Animation.DoubleAnimation(-5,0,TimeSpan.FromMilliseconds(160)));HeaderRecent31.BeginAnimation(OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0.5,1,TimeSpan.FromMilliseconds(160)));}}
  if(MainTabs.SelectedItem==WsusTab&&EventHistoryGroup32.IsExpanded)RefreshEventHistory32(row.Host);else EventHistoryGroup32.Header="История событий";HeaderRecent31.Visibility=Visibility.Collapsed;
  EventStorageError32.Text=EventErrors39(row.Host);EventStorageError32.Visibility=EventStorageError32.Text.Length>0?Visibility.Visible:Visibility.Collapsed;if(EventStorageError32.Text.Length>0){EventHistoryGroup32.Header="История событий · ошибка сохранения";EventHistoryGroup32.ToolTip=EventStorageError32.Text;}else EventHistoryGroup32.ToolTip="Показать предыдущие события выбранного компьютера";
 }
 readonly Dictionary<string,List<ClientEvent31>> memoryEvents32=new(StringComparer.OrdinalIgnoreCase),pendingEvents32=new(Store.Read("event-retry39.json",new Dictionary<string,List<ClientEvent31>>()),StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,string> eventWriteErrors32=new(StringComparer.OrdinalIgnoreCase);
 string headerEventsKey32="",headerEventsHost32="",historyEventsKey32="";
 List<ClientEvent31> SafeEvents32(string host,int limit){if(!diskReadAt39.TryGetValue(host,out var at)||DateTimeOffset.UtcNow-at>TimeSpan.FromMinutes(1))_=LoadEvents39(host);return diskEvents39.GetValueOrDefault(host)?.Take(limit).ToList()??new();}
 static string EventKey32(ClientEvent31 item)=>item.At+"|"+item.Kind+"|"+item.Text+"|"+item.Details;
 List<ClientEvent31> AllEvents32(string host){if(!diskReadAt39.TryGetValue(host,out var read39)||DateTimeOffset.UtcNow-read39>TimeSpan.FromMinutes(1))_=LoadEvents39(host);var disk=diskEvents39.GetValueOrDefault(host);var memory=memoryEvents32.GetValueOrDefault(host);var recent=recentEvents31.GetValueOrDefault(host);int count=memory?.Count??0;if(groupedEvents39.TryGetValue(host,out var old)&&ReferenceEquals(old.Disk,disk)&&ReferenceEquals(old.Memory,memory)&&ReferenceEquals(old.Recent,recent)&&old.Count==count)return old.Result;var result=CompactEvents34((disk??new()).Concat(memory??new()).Concat(recent??new()).DistinctBy(e=>EventKey32(e)+"|"+e.Operation36));groupedEvents39[host]=(disk,memory,recent,count,result);return result;}
 static string EventGroupKey36(ClientEvent31 e)=>e.Kind+"|"+e.Operation36+"|"+e.Text+"|"+e.Details;
 bool historyReset35=true;string historyHost35="";List<ClientEvent31>? historyPending35;
 void RefreshEventHistory32(string host){
  var all=AllEvents32(host);EventHistoryGroup32.Header="История событий · "+all.Count;
  var selected36=EventList32.SelectedItems.Cast<ClientEvent31>().Select(EventGroupKey36).ToHashSet();
  var items=all.Where(x=>EventErrors32.IsChecked!=true||x.Kind is "Error" or "Warning").ToList();
  foreach(var item in items)item.Selected36=selected36.Contains(EventGroupKey36(item));
  var key=host+EventErrors32.IsChecked+string.Join("|",items.Select(x=>EventKey32(x)+"|"+x.Repeats));bool reset=historyReset35||historyHost35!=host;if(key==historyEventsKey32&&!reset)return;
 var viewer=Visual33<ScrollViewer>(EventList32).FirstOrDefault();
  if(!reset&&viewer?.VerticalOffset>1){historyPending35=items;var previous=EventList32.Items.Cast<ClientEvent31>().Select(EventKey32).ToHashSet();int count=items.Count(x=>!previous.Contains(EventKey32(x)));NewEvents35.Content=count>0?"Новых событий: "+count+" ↑":"События обновлены ↑";NewEvents35.Visibility=Visibility.Visible;return;}
  historyEventsKey32=key;historyHost35=host;historyReset35=false;historyPending35=null;NewEvents35.Visibility=Visibility.Collapsed;EventList32.ItemsSource=items;foreach(var item in items.Where(x=>x.Selected36))EventList32.SelectedItems.Add(item);EventSelection36_Changed(this,new RoutedEventArgs());
  Dispatcher.BeginInvoke(new Action(()=>Visual33<ScrollViewer>(EventList32).FirstOrDefault()?.ScrollToTop()),System.Windows.Threading.DispatcherPriority.Loaded);
 }
 void NewEvents35_Click(object sender,RoutedEventArgs e){historyReset35=true;if(ComputerGrid.SelectedItem is ComputerRow row)RefreshEventHistory32(row.Host);}
 void EventScroll35_Changed(object sender,ScrollChangedEventArgs e){if(e.VerticalOffset<=1&&historyPending35!=null&&IsLoaded&&ComputerGrid.SelectedItem is ComputerRow row){historyReset35=true;RefreshEventHistory32(row.Host);}}
 void EventCopy35_KeyDown(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.A&&System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)){EventList32.SelectAll();e.Handled=true;return;}if(e.Key==System.Windows.Input.Key.C&&System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)&&System.Windows.Input.Keyboard.FocusedElement is not TextBox){CopyEvents32_Click(sender,new RoutedEventArgs());e.Handled=true;}}
 void EventHistory_Click(object sender,RoutedEventArgs e)=>EventHistoryGroup32.IsExpanded=!EventHistoryGroup32.IsExpanded;
 void EventHistory32_Changed(object sender,RoutedEventArgs e){if(!IsLoaded||e.OriginalSource!=EventHistoryGroup32)return;Store.Write("event-history-expanded38.json",EventHistoryGroup32.IsExpanded);ArrangeIdentity34();HeaderRecent31.Visibility=Visibility.Collapsed;historyReset35=true;if(ComputerGrid.SelectedItem is ComputerRow row)RefreshEventHistory32(row.Host);}
 void EventFilter32_Changed(object sender,RoutedEventArgs e){if(IsLoaded&&ComputerGrid.SelectedItem is ComputerRow row){historyEventsKey32="";historyReset35=true;RefreshEventHistory32(row.Host);}}
 static string EventCopyText36(IEnumerable<ClientEvent31> items)=>string.Join(Environment.NewLine+Environment.NewLine,items.Select(x=>Display36.Text(x.Display)+(!x.ShortTransport40&&x.Details.Length>0?"\n"+Display36.Text(x.Details):"")+(x.Repeats>1?"\nПовторения:\n"+x.OccurrenceText36:"")));
 void EventSelection36_Changed(object sender,RoutedEventArgs e){if(EventCopySelected36==null)return;int count=EventList32.SelectedItems.Count;EventCopySelected36.Content="Выбранные ("+count+")";EventCopySelected36.IsEnabled=count>0;}

 void EventSelect36_Mouse(object sender,System.Windows.Input.MouseButtonEventArgs e){var element=e.OriginalSource as DependencyObject;while(element!=null&&element!=EventList32){if(element is FrameworkElement f&&f.DataContext is ClientEvent31 item){EventList32.SelectedItem=item;break;}element=VisualTreeHelper.GetParent(element);}}
 void EventCurrent36_Changed(object sender,SelectionChangedEventArgs e){EventSelection36_Changed(sender,e);if(EventCopyOne36!=null)EventCopyOne36.IsEnabled=EventList32.SelectedItem!=null;}
 void CopyEventOne36_Click(object sender,RoutedEventArgs e){if(EventList32.SelectedItem is ClientEvent31 item)Clipboard.SetText(EventCopyText36(new[]{item}));}
 void CopyEventSelected36_Click(object sender,RoutedEventArgs e){var items=EventList32.SelectedItems.Cast<ClientEvent31>().ToList();if(items.Count>0)Clipboard.SetText(EventCopyText36(items));}
 async void CopyEventAll36_Click(object sender,RoutedEventArgs e)=>await Guard(CopyAllEvents39);
 async Task CopyAllEvents39(){if(ComputerGrid.SelectedItem is ComputerRow row){bool errors=EventErrors32.IsChecked==true;if(eventWrites39.TryGetValue(row.Host,out var write))await write;await LoadEvents39(row.Host);Clipboard.SetText(EventCopyText36(AllEvents32(row.Host).Where(x=>!errors||x.Kind is "Error" or "Warning")));}}
 void CopyEvents32_Click(object sender,RoutedEventArgs e){var selected=EventList32.SelectedItems.Cast<ClientEvent31>().ToList();if(selected.Count>0)Clipboard.SetText(EventCopyText36(selected));else CopyEventAll36_Click(sender,e);}
}
