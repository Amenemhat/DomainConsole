using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 bool closeReady39;bool refreshTick39;object? returnTab39;
 void RefreshMode39(){if(WsusActions39==null)return;bool commands=MainTabs.SelectedItem==CommandsTab,wsus=MainTabs.SelectedItem==WsusTab,history=MainTabs.SelectedItem==HistoryTab,settings=MainTabs.SelectedItem==SettingsTab;
  WsusActions39.Visibility=wsus?Visibility.Visible:Visibility.Collapsed;RunEditorButton.Visibility=CommandTools38.Visibility=commands?Visibility.Visible:Visibility.Collapsed;HistoryActions39.Visibility=history?Visibility.Visible:Visibility.Collapsed;BackSettings39.Visibility=settings?Visibility.Visible:Visibility.Collapsed;if(!settings)returnTab39=MainTabs.SelectedItem;
 }
 void BackSettings39_Click(object sender,RoutedEventArgs e)=>MainTabs.SelectedItem=returnTab39??WsusTab;
 void SetUserHeader39(string label){int cut=label.LastIndexOf(" · ",StringComparison.Ordinal);string name=cut<0?label:label[..cut],time=cut<0?"":label[cut..];string key=label+"|"+Theme.Current;if(HeaderUser37.Tag as string==key)return;HeaderUser37.Tag=key;HeaderUser37.Inlines.Clear();HeaderUser37.Inlines.Add(new Run(name){FontWeight=FontWeights.Bold});HeaderUser37.Inlines.Add(new Run(time){FontWeight=FontWeights.Normal,Foreground=Theme.Brush("TextMuted")});}
 void ApplyTerminalTone39(ComputerRow row,bool active,bool reboot,bool failed){
  if(active||reboot||failed)return;
  if(row.Target.Status=="RebootObserved"){
   CurrentStage.Text="Перезагрузка Windows подтверждена";CurrentStage.Foreground=Theme.Tone("Success");
   var proof=rebootSnapshots.GetValueOrDefault(row.Host);string at=proof?.CollectedUtc??row.Target.State?.RebootEvidence?.CollectedUtc??row.Target.State?.Updated??"";
   OperationDetail38.Text="Флаги перезагрузки не проверены · "+Display36.Date(at)+"\nИсточник: проверка клиента. Повторите проверку связи; подробности ограничения — в журнале.";
   OperationDetail38.Foreground=Theme.Tone("Warning");OperationDetail38.Visibility=Visibility.Visible;
  }else {OperationDetail38.Foreground=Theme.Brush("TextMuted");if(row.Target.Status is "Completed" or "VerifiedAfterReboot")CurrentStage.Foreground=Theme.Tone("Success");}
 }
 // Index exists only for one synchronous refresh, so changes to targets cannot leave a stale index.
 Dictionary<string,List<(RemoteJob Job,TargetRecord Target)>>? refreshJobs39;
 IEnumerable<(RemoteJob Job,TargetRecord Target)> HostJobs39(string host)=>refreshJobs39!=null?refreshJobs39.GetValueOrDefault(host)??Enumerable.Empty<(RemoteJob,TargetRecord)>():History.SelectMany(j=>j.Targets.Where(t=>t.Host.Equals(host,StringComparison.OrdinalIgnoreCase)).Select(t=>(j,t)));
 static Dictionary<string,List<(RemoteJob Job,TargetRecord Target)>> IndexJobs39(IEnumerable<RemoteJob> jobs)=>jobs.SelectMany(j=>j.Targets.Select(t=>(Job:j,Target:t))).GroupBy(x=>x.Target.Host,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,List<ClientEvent31>> diskEvents39=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,DateTimeOffset> diskReadAt39=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> loadingEvents39=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Task> eventWrites39=new(StringComparer.OrdinalIgnoreCase);
 long eventDiskReads39;double lastRefreshMs39;
 readonly Dictionary<string,string> eventReadErrors39=new(StringComparer.OrdinalIgnoreCase);
 string EventErrors39(string host)=>string.Join("\n",new[]{eventWriteErrors32.GetValueOrDefault(host,""),eventReadErrors39.GetValueOrDefault(host,"")}.Where(x=>x.Length>0));
 readonly Dictionary<string,Task> eventReads39=new(StringComparer.OrdinalIgnoreCase);
 Task LoadEvents39(string host){if(eventReads39.TryGetValue(host,out var pending)&&!pending.IsCompleted)return pending;return eventReads39[host]=LoadEventsCore39(host);}
 async Task LoadEventsCore39(string host){if(!loadingEvents39.Add(host))return;try{var data=await Task.Run(()=>ReadEvents31(host,10000));eventDiskReads39++;diskEvents39[host]=data;diskReadAt39[host]=DateTimeOffset.UtcNow;foreach(var evicted in diskEvents39.Keys.Where(k=>!k.Equals(host,StringComparison.OrdinalIgnoreCase)&&!CurrentHost(k)).OrderBy(k=>diskReadAt39.GetValueOrDefault(k)).Take(Math.Max(0,diskEvents39.Count-12)).ToList()){diskEvents39.Remove(evicted);diskReadAt39.Remove(evicted);groupedEvents39.Remove(evicted);}eventReadErrors39.Remove(host);if(!closing&&CurrentHost(host)&&MainTabs.SelectedItem==WsusTab&&EventHistoryGroup32.IsExpanded)RefreshEventHistory32(host);}catch(Exception ex){diskReadAt39[host]=DateTimeOffset.UtcNow;eventReadErrors39[host]="История с диска недоступна: "+ex.Message;}finally{loadingEvents39.Remove(host);}}
 readonly Dictionary<string,DateTimeOffset> historyCompacted58=new(StringComparer.OrdinalIgnoreCase);
 async Task WriteEvents39(string host,List<ClientEvent31> entries,Task previous,int days){
  bool written=false;
  try{
   await previous;
   if(pendingEvents32.TryGetValue(host,out var deferred)&&deferred.Count>0){entries.InsertRange(0,deferred);deferred.Clear();}
   var scopes=HostJobs39(host).Where(x=>ActiveTarget(x.Target)).Select(x=>x.Job.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
   bool protectIdentity=IdentityConflict58(host);
   var limits=Store.Read("history-limits58.json",new HistoryLimits58());limits.OrdinaryDays=days;
   bool compact=!historyCompacted58.TryGetValue(host,out var last)||DateTimeOffset.UtcNow-last>=TimeSpan.FromHours(1);
   await Task.Run(()=>{
    var folder=EventFolder31(host);Directory.CreateDirectory(folder);
    foreach(var group in entries.GroupBy(item=>DateTimeOffset.Parse(item.At).ToLocalTime().ToString("yyyy-MM-dd"))){
     var path=Path.Combine(folder,group.Key+".jsonl");
     File.AppendAllText(path,string.Join(Environment.NewLine,group.Select(item=>JsonSerializer.Serialize(item)))+Environment.NewLine);
    }
   });written=true;
   if(compact){await Task.Run(()=>HistoryRetention58.Compact(EventFolder31(host),limits,DateTimeOffset.UtcNow,scopes,protectIdentity));historyCompacted58[host]=DateTimeOffset.UtcNow;diskReadAt39.Remove(host);groupedEvents39.Remove(host);}
   eventWriteErrors32.Remove(host);
  }catch(Exception ex){
   if(!written){if(!pendingEvents32.TryGetValue(host,out var retry)){retry=new();pendingEvents32[host]=retry;}retry.InsertRange(0,entries);}
   eventWriteErrors32[host]=(written?"История сохранена, очистка отложена: ":"Не удалось сохранить историю событий: ")+ex.Message;
  }
 }

 readonly Dictionary<string,(List<ClientEvent31>? Disk,List<ClientEvent31>? Memory,List<ClientEvent31>? Recent,int Count,List<ClientEvent31> Result)> groupedEvents39=new(StringComparer.OrdinalIgnoreCase);
 void RestoreEventRetries39(){foreach(var pair in pendingEvents32.ToList()){var batch=pair.Value.ToList();memoryEvents32[pair.Key]=batch.OrderByDescending(x=>x.At).ToList();pair.Value.Clear();eventWrites39[pair.Key]=WriteEvents39(pair.Key,batch,Task.CompletedTask,Math.Clamp(Store.Read("event-days31.json",30),1,365));}}
 static void AwaitUi39(Task task){while(!task.IsCompleted){var frame=new DispatcherFrame();var pump=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(2)};pump.Tick+=(s,e)=>{pump.Stop();frame.Continue=false;};pump.Start();Dispatcher.PushFrame(frame);}task.GetAwaiter().GetResult();}
 bool arranging39;
 void QueueLayout39(){if(arranging39)return;arranging39=true;Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{arranging39=false;if(!closing)ArrangeIdentity34();}));}
}
