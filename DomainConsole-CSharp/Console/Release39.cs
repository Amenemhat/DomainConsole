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
 void SetUserHeader39(string label){int cut=label.LastIndexOf(" · ",StringComparison.Ordinal);string name=cut<0?label:label[..cut],time=cut<0?"":label[cut..];if(HeaderUser37.Tag as string==label)return;HeaderUser37.Tag=label;HeaderUser37.Inlines.Clear();HeaderUser37.Inlines.Add(new Run(name){FontWeight=FontWeights.Bold});HeaderUser37.Inlines.Add(new Run(time){FontWeight=FontWeights.Normal,Foreground=Theme.Brush("TextMuted")});}
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
 async Task LoadEvents39(string host){if(!loadingEvents39.Add(host))return;try{var data=await Task.Run(()=>ReadEvents31(host,10000));eventDiskReads39++;diskEvents39[host]=data;diskReadAt39[host]=DateTimeOffset.UtcNow;eventWriteErrors32.Remove(host);if(!closing&&CurrentHost(host)&&MainTabs.SelectedItem==WsusTab&&EventHistoryGroup32.IsExpanded)RefreshEventHistory32(host);}catch(Exception ex){diskReadAt39[host]=DateTimeOffset.UtcNow;eventWriteErrors32[host]="История с диска недоступна: "+ex.Message;}finally{loadingEvents39.Remove(host);}}
 async Task WriteEvents39(string host,List<ClientEvent31> entries,Task previous,int days){try{await previous;await Task.Run(()=>{var folder=EventFolder31(host);Directory.CreateDirectory(folder);foreach(var item in entries){var day=DateTimeOffset.Parse(item.At).ToLocalTime().ToString("yyyy-MM-dd");File.AppendAllText(Path.Combine(folder,day+".jsonl"),JsonSerializer.Serialize(item)+Environment.NewLine);}var cutoff=DateTime.Now.AddDays(-days).Date;foreach(var file in Directory.GetFiles(folder,"*.jsonl"))if(DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file),"yyyy-MM-dd",null,System.Globalization.DateTimeStyles.None,out var day)&&day<cutoff)File.Delete(file);});eventWriteErrors32.Remove(host);}catch(Exception ex){if(!pendingEvents32.TryGetValue(host,out var retry)){retry=new();pendingEvents32[host]=retry;}retry.InsertRange(0,entries);eventWriteErrors32[host]="Не удалось сохранить историю событий: "+ex.Message;}}
 readonly Dictionary<string,(List<ClientEvent31>? Disk,List<ClientEvent31>? Memory,List<ClientEvent31>? Recent,int Count,List<ClientEvent31> Result)> groupedEvents39=new(StringComparer.OrdinalIgnoreCase);
 void RestoreEventRetries39(){foreach(var pair in pendingEvents32.ToList()){var batch=pair.Value.ToList();memoryEvents32[pair.Key]=batch.OrderByDescending(x=>x.At).ToList();pair.Value.Clear();eventWrites39[pair.Key]=WriteEvents39(pair.Key,batch,Task.CompletedTask,Math.Clamp(Store.Read("event-days31.json",30),1,365));}}
 static void AwaitUi39(Task task){while(!task.IsCompleted){var frame=new DispatcherFrame();var pump=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(2)};pump.Tick+=(s,e)=>{pump.Stop();frame.Continue=false;};pump.Start();Dispatcher.PushFrame(frame);}task.GetAwaiter().GetResult();}
 bool arranging39;
 void QueueLayout39(){if(arranging39)return;arranging39=true;Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{arranging39=false;if(!closing)ArrangeIdentity34();}));}
}
