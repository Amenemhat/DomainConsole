using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyOperation60(){
  var selected=ComputerGrid.SelectedItem;var tab=MainTabs.SelectedItem;int theme=Theme.Current;
  var row=new ComputerRow(new(){Host="operation60.invalid",Name="OPERATION-60"});
  var now=DateTimeOffset.UtcNow;var state=new RemoteState{Status="Completed",Stage="Поиск завершён",Started=now.AddMinutes(-2).ToString("o"),Updated=now.ToString("o"),Ended=now.ToString("o")};
  var job=new RemoteJob{OutputChannel="WSUS",Manual56=true,Steps=new(){new(){Kind="UpdateScan"}},Targets=new(){new(){Host=row.Host,Name=row.Name,Status="Completed",State=state}}};
  Computers.Add(row);History.Insert(0,job);ComputerGrid.SelectedItem=row;MainTabs.SelectedItem=WsusTab;
  try{
   reportWaiting[row.Host]=new(){Id=job.Id,Server54=OverviewServer,Until=now.AddMinutes(10),Requested54=now,Next=now.AddMinutes(1)};
   UpdateWsusStage();RefreshActivity();UpdateLayout();
   if(!CurrentStage.Text.Contains("Ожидаем новый отчёт")||!CurrentProgress.IsIndeterminate||CurrentProgress.Visibility!=Visibility.Visible||!row.WorkBusy)throw new Exception("Completed scan lost end-to-end report waiting progress");
   CurrentProgress.ApplyTemplate();var busy=(Border)CurrentProgress.Template.FindName("BusyTrack60",CurrentProgress);
   await Task.Delay(150);double first=((TranslateTransform)busy.OpacityMask.RelativeTransform).X;await Task.Delay(350);double second=((TranslateTransform)busy.OpacityMask.RelativeTransform).X;
   if(Math.Abs(first-second)<0.01)throw new Exception("Progress template does not actually animate");
   row.SetService43("Чтение флагов и активности");manualLinkReads31.Add(row.Host);RefreshCollector44(row.Host);RenderWork60(row);UpdateLayout();
   if(!CollectorBusy53.IsIndeterminate||LinkBusy60.Visibility!=Visibility.Visible||!CollectorBusy53.IsDescendantOf(CollectorDetails54)||!LinkBusy60.IsDescendantOf(HeaderLink))throw new Exception("Mini progress is not inside its active button");
   bool paused60=TransportBudget41.Paused;try{TransportBudget41.Paused=true;RefreshCollector44(row.Host);if(!CollectorBusy53.IsIndeterminate)throw new Exception("Active manual read hidden by background pause");}finally{TransportBudget41.Paused=paused60;RefreshCollector44(row.Host);}
   foreach(int color in new[]{0,1,2}){Theme.Apply(color,false);UpdateWsusStage();foreach(var size in new[]{(1280d,720d),(1920d,1080d)})CaptureContent38("operation60-wait-"+color+"-"+size.Item1,size.Item1,size.Item2);}
   manualLinkReads31.Remove(row.Host);RenderWork60(row);if(LinkBusy60.Visibility==Visibility.Visible||!CollectorBusy53.IsIndeterminate)throw new Exception("Connection completion stopped collector progress");
   row.SetService43("");RefreshCollector44(row.Host);if(CollectorBusy53.Visibility==Visibility.Visible)throw new Exception("Idle collector still animates");
   reportWaiting[row.Host].Until=now.AddMinutes(-1);UpdateWsusStage();if(CurrentProgress.Visibility==Visibility.Visible||!CurrentStage.Text.Contains("задерживается"))throw new Exception("Expired report wait animates forever");
   reportWaiting.Remove(row.Host);UpdateWsusStage();if(CurrentProgress.Visibility==Visibility.Visible)throw new Exception("Completed operation still animates");
   job.OutputChannel="Commands";job.Steps[0].Kind="CMD";job.Targets[0].Status=state.Status="Running";state.Ended="";state.Stage="SFC: проверка";state.Progress=37;MainTabs.SelectedItem=CommandsTab;
   UpdateWsusStage();UpdateLayout();if(CommandActivity60.Visibility!=Visibility.Visible||CommandProgress60.Value!=37)throw new Exception("Commands have no process/progress banner");
   foreach(int color in new[]{0,1,2}){Theme.Apply(color,false);UpdateWsusStage();foreach(var size in new[]{(1280d,720d),(1920d,1080d)})CaptureContent38("operation60-command-"+color+"-"+size.Item1,size.Item1,size.Item2);}
   state.Progress=null;UpdateWsusStage();if(!CommandProgress60.IsIndeterminate)throw new Exception("Silent command lost running indication");
   job.Targets[0].Status=state.Status="Completed";state.Ended=now.ToString("o");UpdateWsusStage();if(CommandActivity60.Visibility!=Visibility.Collapsed)throw new Exception("Command indicator survived completion");
   var choice=new UpdateConflictChoice(this,"Modeless decision fixture",false);var deciding=choice.Decide60();await Task.Delay(30);if(deciding.IsCompleted||!IsEnabled)throw new Exception("Conflict decision blocked parent workspace");choice.Close();if(await deciding)throw new Exception("Closing conflict decision accepted launch");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"ui-operation60.txt"),"PASS actual moving animation; terminal scan awaiting report; timeout; command with and without percent; completion; modeless conflict decision; three themes and two sizes");
  }finally{manualLinkReads31.Remove(row.Host);reportWaiting.Remove(row.Host);History.Remove(job);Computers.Remove(row);ComputerGrid.SelectedItem=selected;MainTabs.SelectedItem=tab;Theme.Apply(theme,false);ShowCurrent();}
 }
}
