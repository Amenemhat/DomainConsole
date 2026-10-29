using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 static readonly List<string> acceptanceEvidence51=new();
 static void Assert51(bool value,string name){if(!value)throw new Exception("Acceptance51: "+name);acceptanceEvidence51.Add(name+": PASS");}
 async Task VerifyAcceptance51(){
  acceptanceEvidence51.Clear();
  var today=DateTime.Today;
  Assert51(Display36.Date(today.AddMinutes(5))=="сегодня, 00:05","local today boundary");
  var priorYear=new DateTime(today.Year-1,12,31,23,59,0);
  Assert51(Display36.Date(priorYear)==priorYear.ToString("dd.MM.yyyy, HH:mm"),"previous year retains year");
  var anotherDay=today.DayOfYear>1?new DateTime(today.Year,1,1):today.AddDays(1);
  Assert51(Display36.Date(anotherDay)==anotherDay.ToString("dd.MM, HH:mm"),"same year different day");
  foreach(int offset in new[]{-12,-5,0,3,14}){
   var original=new DateTimeOffset(today.Year,1,1,0,15,0,TimeSpan.FromHours(offset));var text=original.ToString("o");
   Assert51(Display36.Date(text)==Display36.Date(original.LocalDateTime),"offset conversion "+offset);
   Assert51(Display36.Date(text,true)==original.LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss"),"full local timestamp "+offset);
  }
  string script="Write-Output '2026-01-01T23:59:00+03:00'; # C:\\2026-01-01T23:59:00";
  var step=Store.Clone(new CommandStep{Kind="PS",Code=script});NormalizeCommandKinds34(new[]{step});
  Assert51(step.Kind=="PowerShell"&&step.Code==script,"import/roundtrip PS normalization preserves script timestamps and paths");
  string raw="## Вывод команды\n"+script+"\n2026-01-01T23:59:00+03:00\n";
  Assert51(Display36.Text(raw)==raw,"raw command output timestamps are not rewritten");
  foreach(string cause in new[]{"WUA: HRESULT 0x80070020",@"Запись C:\fixture51\status.json: 0x80070020"}){
   var card=FailureCard31(new(){State=new(){Status="Failed",Error=cause}});
   Assert51(card.Contains(cause)&&card.Contains("0x80070020")&&(cause.Contains("status.json")||!card.Contains("status.json")),"sharing violation preserves actual source: "+cause);
  }
  VerifyOutputMatrix51();
  await VerifyLogDownload51();
  await VerifyRebootFlow51();
  await VerifyHistoryInput51();
  VerifyScale51();
  File.WriteAllLines(Path.Combine(Path.GetTempPath(),"ui-acceptance51.txt"),acceptanceEvidence51);
 }
 void VerifyOutputMatrix51(){
  foreach(var operation in new[]{("CMD","Команда CMD"),("PowerShell","Команда PowerShell"),("PowerShell","Перезапуск служб"),("CacheCleanup","Очистка кэша"),("Diagnostics","Диагностика"),("UpdateScan","Поиск обновлений"),("Updates","Установка обновлений")})foreach(string status in new[]{"Completed","Failed","CompletedWithErrors","InterventionRequired"}){
   bool failed=status!="Completed";
   var state=new RemoteState{Status=status,Started=DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o"),Ended=DateTimeOffset.UtcNow.ToString("o"),Error=failed?"fixture primary cause 0x80070020":"",Results=new(){new(){Kind=operation.Item1,Name=operation.Item2,ExitCode=failed?1:0}},RebootEvidence=new(){Required=false,CollectedUtc=DateTimeOffset.UtcNow.ToString("o")}};
   var result=PresentResult("matrix51.fixture.invalid",new(){State=state,Files=new(){new(){Name="step-1.out",Body="FIRST51\nrepeat\nrepeat\nrepeat\nrepeat\n2026-01-01T23:59:00+03:00\nLAST51"}}});
   Assert51(result.Contains("FIRST51")&&result.Contains("LAST51")&&result.Contains("repeat\nrepeat"),"output preserved "+operation.Item2+"/"+status);
   Assert51(!failed||result.Contains("0x80070020"),"primary code retained "+operation.Item2+"/"+status);
   foreach(bool wsus in new[]{false,true}){var visible=Display36.Text(ResultPresentation36.Format(CommandOutput(result,wsus)));Assert51(visible.Contains("repeat\nrepeat\nrepeat\nrepeat")&&visible.Contains("2026-01-01T23:59:00+03:00"),"channel preserves literal output "+operation.Item2+"/"+status+"/"+wsus);}
   var formatted=ResultPresentation36.Format(result);var parts=LogViewer.Sections(formatted);
   Assert51(parts.Count>0&&parts[0].Title=="Главное"&&parts[^1].Title=="Сведения о задании","summary/details order "+operation.Item2+"/"+status);
   var viewer=new LogViewer{Text=formatted};viewer.SetExpanded("Вывод команды",true);viewer.SetExpanded("Технические подробности · вывод операции",true);
   Assert51(viewer.VisibleText().Contains("LAST51"),"copy includes output "+operation.Item2+"/"+status);
  }
  var sourceCases=new[]{"WinRM timeout","Не удалось разрешить имя сервера","Access is denied","Отказано в доступе","#< CLIXML <Objs/>"};
  foreach(string cause in sourceCases){var e=new ClientEvent31{Operation36="link",Kind="Error",Text=cause,Details=cause};Assert51(e.Body38=="Ошибка WinRM/аутентификации"&&!EventCopyText36(new[]{e}).Contains(cause)&&Store.Clone(e).Details==cause,"transport visible/raw separation "+cause);}
 }
 async Task VerifyLogDownload51(){
  var target=new TargetRecord{Host="logs51.fixture.invalid",ConnectionSnapshot=true,ConnectionAddress="192.0.2.51"};var row=new ComputerRow(target,"job51");
  var gate=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);TargetRecord? received=null;string id="",opened="";int opens=0;
  var pending=DownloadJobLogs51(row,(t,j)=>{received=t;id=j;return gate.Task;},folder=>{opens++;opened=folder;});
  target.ConnectionAddress="192.0.2.52";gate.SetResult(Path.GetTempPath());await pending;
  Assert51(received!=target&&received?.ConnectionAddress=="192.0.2.51"&&id=="job51"&&opens==1&&opened==Path.GetTempPath(),"log download pins endpoint/job and opens saved folder once");
  bool failed=false;try{await DownloadJobLogs51(row,(_,_)=>Task.FromException<string>(new IOException("fixture download failure")),_=>opens++);}catch(IOException){failed=true;}
  Assert51(failed&&opens==1,"failed download never opens folder or reports success");
 }
 async Task VerifyRebootFlow51(){
  const string host="reboot-flow51.fixture.invalid";
  var savedPlans=maintenancePlans.ToList();var savedJobs=History.ToList();var savedQueue=queue.ToList();var savedWatches=rebootWatches.ToList();
  bool auto=AutoServiceEnabled,paused=TransportBudget41.Paused;var row=new ComputerRow(new(){Host=host,Name="Reboot flow fixture"});Computers.Add(row);
  try{
   maintenancePlans.Clear();History.Clear();queue.Clear();rebootWatches.Clear();AutoServiceEnabled=false;PollingControl43.Resume();
   var now=DateTimeOffset.UtcNow;string before=now.AddDays(-1).ToString("o");
   var target=ConnectionStore.Capture(row.Target);target.Status="Completed";
   var install=new RemoteJob{OutputChannel="WSUS",Targets=new(){Store.Clone(target)},Steps=new(){new(){Kind="Updates"}}};
   install.Targets[0].State=new(){Id=install.Id,Status="Completed",Ended=now.AddMinutes(-20).ToString("o"),RebootRequired=true,UpdatesRemaining=0};History.Add(install);
   var reboot=new RemoteJob{Name="Перезагрузка текущего компьютера",ScanAfterReboot=true,ExplicitRebootScan51=true,Targets=new(){target}};History.Insert(0,reboot);
   var receipt=new RebootPlan{RequestedUtc=now.AddMinutes(-15).ToString("o"),DueUtc=now.AddMinutes(-14).ToString("o"),BeforeBootUtc=before,DispatchExitCode=0,DispatchUtc=now.AddMinutes(-15).ToString("o")};
   RegisterRebootPlan(reboot,new(target,reboot.Id),new(){RebootPlan=receipt,ClientUtc=now.ToString("o")});
   var watch=rebootWatches[host];Assert51(watch.Confirmed&&watch.ExplicitScan51&&!watch.Done,"receipt persisted explicit scan with Auto OFF");
   ApplyClientSnapshot(host,new(){BootUtc=before,CollectedUtc=now.ToString("o"),Required=true});
   Assert51(!RebootReturned(watch,rebootSnapshots[host]),"reachable old boot does not confirm reboot");
   int resets=0;await TryResetRebootPoll51(host,watch,target,_=>{resets++;return Task.FromResult("reset requested");});
   Assert51(resets==1&&!watch.Done,"collector failure/reset does not complete reboot");
   var fresh=new RebootSnapshot{BootUtc=now.AddMinutes(-10).ToString("o"),CollectedUtc=DateTimeOffset.UtcNow.ToString("o"),Required=null,Error="fixture partial CBS"};
   await CompleteCollectorRepair47(target,PollingControl43.Token,Task.FromResult("repaired"),()=>{ApplyClientSnapshot(host,fresh);return Task.CompletedTask;},_=>{});
   Assert51(watch.ReadyAfter!=default&&RebootViewFor40(row).Required!=false,"collector recovery advances checkpoint but partial flags remain unknown");
   fresh=new(){BootUtc=fresh.BootUtc,CollectedUtc=DateTimeOffset.UtcNow.AddMilliseconds(1).ToString("o"),Required=false};ApplyClientSnapshot(host,fresh);
   await ContinueAfterReboot(host,watch,target,_=>Task.FromResult(new LiveUpdateActivity{InstallerBusy=true}));
   Assert51(queue.Count==0&&!watch.Done,"busy WUA delays post-boot scan");
   await ContinueAfterReboot(host,watch,target,_=>Task.FromResult(new LiveUpdateActivity()));
   Assert51(queue.Count==1&&queue.Peek().Job.Steps.Single().Kind=="UpdateScan"&&!queue.Peek().Job.AutomaticChild31,"one explicit post-boot scan with Auto OFF and no installation");
   var follow=queue.Peek().Job;watch=Store.Clone(watch);rebootWatches[host]=watch;RestoreFollowupQueues();RestoreFollowupQueues();
   Assert51(queue.Count==1&&History.Count(j=>j.Id==follow.Id)==1,"restart restores the same follow-up without duplicates");
   queue.Clear();follow.Targets[0].Status="Completed";follow.Targets[0].State=new(){Id=follow.Id,Status="Completed",Started=DateTimeOffset.UtcNow.AddSeconds(-1).ToString("o"),Ended=DateTimeOffset.UtcNow.ToString("o"),UpdatesRemaining=0,ReportRequested=true,Results=new(){new(){Kind="UpdateScan",ExitCode=0,Ended=DateTimeOffset.UtcNow.ToString("o")}}};
   ApplyClientSnapshot(host,new(){BootUtc=fresh.BootUtc,CollectedUtc=DateTimeOffset.UtcNow.AddSeconds(1).ToString("o"),Required=false});
   var report=new WsusReport{NeededCount=0,RequiredComplete=true,LastSyncResult="Succeeded",LastReportedUtc=DateTimeOffset.UtcNow.AddSeconds(1).ToString("o")};
   reportCache[host]=new(){Found=true,Server=OverviewServer,ReceivedUtc=DateTimeOffset.UtcNow.ToString("o"),Report=report};liveReports.Add(host);
   Assert51(watch.Done&&RebootViewFor40(row).Required==false&&!PostRebootText(host).Contains("Ожидается"),"completed post-boot flow does not restore old wait");
   Assert51(OverviewStatus(row).Kind=="Success","fresh scan and report complete ready state");
  }finally{
   Computers.Remove(row);rebootSnapshots.Remove(host);reportCache.Remove(host);liveReports.Remove(host);clientConnections.Remove(host);readyProofs43.Remove(host);
   maintenancePlans.Clear();foreach(var x in savedPlans)maintenancePlans[x.Key]=x.Value;
   History.Clear();foreach(var x in savedJobs)History.Add(x);queue.Clear();foreach(var x in savedQueue)queue.Enqueue(x);
   rebootWatches.Clear();foreach(var x in savedWatches)rebootWatches[x.Key]=x.Value;
   AutoServiceEnabled=auto;if(paused)PollingControl43.Pause();else PollingControl43.Resume();SaveMaintenance();Save();Store.Write("reboot-watches.json",rebootWatches);
  }
 }
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 static void Key51(byte key,bool up=false)=>keybd_event(key,0,up?2u:0u,UIntPtr.Zero);
 async Task VerifyHistoryInput51(){
  var row=Current();string host=row.Host;var mem=memoryEvents32.GetValueOrDefault(host);var recent=recentEvents31.GetValueOrDefault(host);var tab=MainTabs.SelectedItem;bool expanded=EventHistoryGroup32.IsExpanded;int filter=EventFilter47.SelectedIndex;double width=Width,height=Height;
  try{
   Width=1480;Height=940;MainTabs.SelectedItem=WsusTab;ShowCurrent();EventHistoryGroup32.IsExpanded=true;EventFilter47.SelectedIndex=3;
   memoryEvents32[host]=Enumerable.Range(0,12).Select(i=>new ClientEvent31{At=DateTimeOffset.UtcNow.AddMinutes(-i).ToString("o"),Text="Input fixture "+i,Details="Details "+i,Kind="Info",Operation36="input51"}).ToList();recentEvents31[host]=memoryEvents32[host].ToList();historyReset35=true;historyEventsKey32="";RefreshEventHistory32(host);UpdateLayout();
   Activate();SetForegroundWindow(new WindowInteropHelper(this).Handle);await Task.Delay(100);
   async Task Click(int index){EventList32.ScrollIntoView(EventList32.Items[index]);UpdateLayout();var item=(ListBoxItem)EventList32.ItemContainerGenerator.ContainerFromIndex(index);var point=item.PointToScreen(new Point(8,item.ActualHeight/2));Assert51(SetCursorPos((int)point.X,(int)point.Y),"move mouse "+index);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);await Task.Delay(100);}
   await Click(0);Assert51(EventList32.SelectedIndex==0&&EventList32.SelectedItems.Count==1,"real mouse selects first event");
   Key51(0x10);await Click(2);Key51(0x10,true);await Task.Delay(100);
   Assert51(EventList32.SelectedItems.Count==3,"real Shift click selects range");
   Key51(0x11);await Click(1);Key51(0x11,true);await Task.Delay(100);
   Assert51(EventList32.SelectedItems.Count==2&&!EventList32.SelectedItems.Contains(EventList32.Items[1]),"real Ctrl click toggles one event");
   EventList32.Focus();Key51(0x11);Key51(0x43);Key51(0x43,true);Key51(0x11,true);await Task.Delay(100);
   Assert51(Clipboard.GetText().Contains("Details 0")&&Clipboard.GetText().Contains("Details 2")&&!Clipboard.GetText().Contains("Details 1"),"real Ctrl C respects selected rows");
   Key51(0x11);Key51(0x41);Key51(0x41,true);Key51(0x11,true);await Task.Delay(100);
   Assert51(EventList32.SelectedItems.Count==EventList32.Items.Count,"real Ctrl A selects visible history");
  }finally{
   Key51(0x10,true);Key51(0x11,true);if(mem==null)memoryEvents32.Remove(host);else memoryEvents32[host]=mem;if(recent==null)recentEvents31.Remove(host);else recentEvents31[host]=recent;EventFilter47.SelectedIndex=filter;EventHistoryGroup32.IsExpanded=expanded;Width=width;Height=height;MainTabs.SelectedItem=tab;historyEventsKey32="";ShowCurrent();
  }
 }
 void VerifyScale51(){
  var root=(FrameworkElement)Content;int theme=Theme.Current;var tab=MainTabs.SelectedItem;var old=new Size(root.ActualWidth,root.ActualHeight);
  try{
   foreach(double scale in new[]{1.25,1.5})foreach(int palette in new[]{0,1,2})foreach(var mode in new[]{WsusTab,CommandsTab,HistoryTab,SettingsTab}){
    Theme.Apply(palette,false);MainTabs.SelectedItem=mode;ShowCurrent();UpdateLayout();HideToast(null,EventArgs.Empty);
    double width=1480/scale,height=940/scale;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));
    Assert51(CompactHeader32.ActualWidth>0&&CompactHeader32.TranslatePoint(new Point(CompactHeader32.ActualWidth,CompactHeader32.ActualHeight),root).X<=width+2,"scaled header within viewport "+scale+"/"+palette+"/"+mode.Name);
    Assert51(HeaderActions34.TranslatePoint(new Point(HeaderActions34.ActualWidth,0),root).X<=width+2,"scaled actions within viewport "+scale+"/"+palette+"/"+mode.Name);
    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1480,940,96*scale,96*scale,PixelFormats.Pbgra32);var visual=new DrawingVisual();
    using(var context=visual.RenderOpen()){context.DrawRectangle(Theme.Brush("AppSurface"),null,new Rect(0,0,width,height));context.DrawRectangle(new VisualBrush(root){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(0,0,width,height));}
    bitmap.Render(visual);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
    using var file=File.Create(Path.Combine(Path.GetTempPath(),"ui-acceptance51-scale-"+scale.ToString(CultureInfo.InvariantCulture)+"-"+palette+"-"+mode.Name+".png"));encoder.Save(file);
   }
  }finally{Theme.Apply(theme,false);MainTabs.SelectedItem=tab;root.Measure(old);root.Arrange(new Rect(old));ShowCurrent();}
 }
}
