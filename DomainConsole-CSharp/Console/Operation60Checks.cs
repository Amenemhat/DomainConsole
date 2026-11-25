using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyOperation60(){
  var manual=new RemoteJob{Manual56=true};
  var automatic=new RemoteJob();
  var a=new TargetRecord{Host="manual-a.invalid"};
  var b=new TargetRecord{Host="manual-b.invalid"};
  var candidates=new (RemoteJob Job,TargetRecord Target)[]{(automatic,a),(automatic,b),(manual,a)};
  bool Pending(string host)=>host==a.Host;
  if(SelectReadySubmission(candidates,automatic.Id,Pending).Job!=manual)
   throw new Exception("Automatic dispatch bypassed a ready manual command.");
  if(SelectReadySubmission(candidates.Take(2),"",Pending).Target!=b)
   throw new Exception("A manual preflight stalled work on an unrelated peer or admitted automatic work on the same client.");
  if(SelectReadySubmission(candidates.Take(1),"",_=>false).Target!=a)
   throw new Exception("Automatic dispatch did not resume after manual preparation.");
  foreach(var unresolved in new[]{"Sending","Submitted","SubmissionUnknown"}){
   var target=new TargetRecord{Status=unresolved};
   if(!SubmissionCanStillRun(target,false))
    throw new Exception("A repeat click could resubmit an unconfirmed command without a cached remote status.");
  }
  if(SubmissionCanStillRun(new TargetRecord{Status="Cancelled"},false))
   throw new Exception("A known cancelled submission prevents an explicit new attempt.");
  int reads=0;
  var recovered=await ReadIdentityWithRetry(a,(_,_)=>++reads==1
   ?Task.FromException<ClientIdentity58>(new TimeoutException())
   :Task.FromResult(new ClientIdentity58{Name="identity-recovered"}),CancellationToken.None);
  if(reads!=2||recovered.Name!="identity-recovered")throw new Exception("Transient identity read was not retried exactly once");
  reads=0;
  try{await ReadIdentityWithRetry(a,(_,_)=>{reads++;return Task.FromException<ClientIdentity58>(new TimeoutException());},CancellationToken.None);
   throw new InvalidOperationException("Identity read hid a persistent timeout");}
  catch(TimeoutException) when(reads==2){}
  if(!DeliveryMayHaveStarted(new IOException("DC_SUBMISSION_UNKNOWN|WinRM connection lost"))||
   DeliveryMayHaveStarted(new IOException("WinRM connection failed before dispatch")))
   throw new Exception("Pre-dispatch and post-dispatch transport failures were conflated");
  await VerifyManualPartialDispatch60(a,b);
  var selected=ComputerGrid.SelectedItem;var tab=MainTabs.SelectedItem;int theme=Theme.Current;
  var row=new ComputerRow(new(){Host="operation60.invalid",Name="OPERATION-60"});
  var now=DateTimeOffset.UtcNow;var state=new RemoteState{Status="Completed",Stage="Поиск завершён",Started=now.AddMinutes(-2).ToString("o"),Updated=now.ToString("o"),Ended=now.ToString("o")};
  var job=new RemoteJob{OutputChannel="WSUS",Manual56=true,Steps=new(){new(){Kind="UpdateScan"}},Targets=new(){new(){Host=row.Host,Name=row.Name,Status="Completed",State=state}}};
  Computers.Add(row);History.Insert(0,job);ComputerGrid.SelectedItem=row;MainTabs.SelectedItem=WsusTab;
  try{
   preparing60[PreparationKey60(row.Host,"WSUS")]=("WSUS","WSUS decision",now);
   if(WorkState60(row,false)!=null||WorkState60(row,true)?.Text.StartsWith("WSUS decision")!=true)throw new Exception("WSUS preparation leaked into command lane");
   preparing60[PreparationKey60(row.Host,"Commands")]=("Commands","Command prepare",now);
   if(WorkState60(row,false)?.Text.StartsWith("Command prepare")!=true)throw new Exception("Independent command preparation missing");
   preparing60.Remove(PreparationKey60(row.Host,"Commands"));if(WorkState60(row,true)?.Text.StartsWith("WSUS decision")!=true)throw new Exception("Command completion cleared WSUS preparation");preparing60.Remove(PreparationKey60(row.Host,"WSUS"));
   reportWaiting[row.Host]=new(){Id=job.Id,Server54=OverviewServer,Until=now.AddMinutes(10),Requested54=now,Next=now.AddMinutes(1)};
   UpdateWsusStage();RefreshActivity();UpdateLayout();
   if(!CurrentStage.Text.Contains("Ожидаем новый отчёт")||!CurrentProgressMotion.IsIndeterminate||!CurrentProgressMotion.IsVisible||!Equals(CurrentProgressMotion.Tag,"slow")||!row.WorkBusy||!WsusStopButton.IsEnabled)throw new Exception("Completed scan lost end-to-end report waiting progress");
   CurrentProgressMotion.ApplyTemplate();var busy=(Border)CurrentProgressMotion.Template.FindName("SlowTrack",CurrentProgressMotion);
   await Task.Delay(150);double first=((TranslateTransform)busy.OpacityMask.RelativeTransform).X;await Task.Delay(350);double second=((TranslateTransform)busy.OpacityMask.RelativeTransform).X;
   if(Math.Abs(first-second)<0.01)throw new Exception("Progress template does not actually animate");
   row.SetService43("Чтение флагов и активности");manualLinkReads31.Add(row.Host);RefreshCollector44(row.Host);RenderWork60(row);UpdateLayout();
   if(!CollectorBusy53.IsIndeterminate||LinkBusy60.Visibility!=Visibility.Visible||!CollectorBusy53.IsDescendantOf(CollectorDetails54)||!LinkBusy60.IsDescendantOf(HeaderLink))throw new Exception("Mini progress is not inside its active button");
   bool paused60=TransportBudget41.Paused;try{TransportBudget41.Paused=true;RefreshCollector44(row.Host);if(!CollectorBusy53.IsIndeterminate)throw new Exception("Active manual read hidden by background pause");}finally{TransportBudget41.Paused=paused60;RefreshCollector44(row.Host);}
   foreach(int color in new[]{0,1,2}){Theme.Apply(color,false);UpdateWsusStage();foreach(var size in new[]{(1280d,720d),(1920d,1080d)})CaptureContent38("operation60-wait-"+color+"-"+size.Item1,size.Item1,size.Item2);}
   manualLinkReads31.Remove(row.Host);RenderWork60(row);if(LinkBusy60.Visibility==Visibility.Visible||!CollectorBusy53.IsIndeterminate)throw new Exception("Connection completion stopped collector progress");
   row.SetService43("");RefreshCollector44(row.Host);if(CollectorBusy53.Visibility==Visibility.Visible)throw new Exception("Idle collector still animates");
   reportWaiting[row.Host].Until=now.AddMinutes(-1);UpdateWsusStage();if(CurrentProgressMotion.Visibility==Visibility.Visible||!CurrentStage.Text.Contains("не получен за 10 минут"))throw new Exception("Expired report wait animates forever");
   reportWaiting.Remove(row.Host);UpdateWsusStage();if(CurrentProgressMotion.Visibility==Visibility.Visible)throw new Exception("Completed operation still animates");
   confirmedJobs.Add(job.Id+row.Host);liveConnections.Add(row.Host);clientConnections[row.Host]=new(){LastSuccess=DateTimeOffset.UtcNow};
   job.OutputChannel="Commands";job.Steps[0].Kind="CMD";job.Targets[0].Status=state.Status="Running";state.Ended="";state.Stage="SFC: проверка";state.Progress=37;MainTabs.SelectedItem=CommandsTab;
   UpdateWsusStage();UpdateLayout();if(CommandActivity60.Visibility!=Visibility.Visible||CommandProgress60.Value!=37)throw new Exception("Commands have no process/progress banner");
   foreach(int color in new[]{0,1,2}){Theme.Apply(color,false);UpdateWsusStage();foreach(var size in new[]{(1280d,720d),(1920d,1080d)})CaptureContent38("operation60-command-"+color+"-"+size.Item1,size.Item1,size.Item2);}
   state.Progress=null;UpdateWsusStage();if(!CommandProgress60.IsIndeterminate)throw new Exception("Silent command lost running indication");
   job.Targets[0].Status=state.Status="Completed";state.Ended=now.ToString("o");UpdateWsusStage();if(CommandActivity60.Visibility!=Visibility.Collapsed)throw new Exception("Command indicator survived completion");
   var choice=new UpdateConflictChoice(this,"Modeless decision fixture",false);var deciding=choice.Decide60();await Task.Delay(30);if(deciding.IsCompleted||!IsEnabled)throw new Exception("Conflict decision blocked parent workspace");choice.Close();if(await deciding)throw new Exception("Closing conflict decision accepted launch");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"ui-operation60.txt"),"PASS actual moving animation; terminal scan awaiting report; timeout; command with and without percent; completion; modeless conflict decision; three themes and two sizes");
  }finally{confirmedJobs.Remove(job.Id+row.Host);liveConnections.Remove(row.Host);clientConnections.Remove(row.Host);preparing60.Remove(PreparationKey60(row.Host,"Commands"));preparing60.Remove(PreparationKey60(row.Host,"WSUS"));manualLinkReads31.Remove(row.Host);reportWaiting.Remove(row.Host);History.Remove(job);Computers.Remove(row);ComputerGrid.SelectedItem=selected;MainTabs.SelectedItem=tab;Theme.Apply(theme,false);ShowCurrent();}
 }
 async Task VerifyManualPartialDispatch60(TargetRecord a,TargetRecord b){
  var first=new RemoteJob{Manual56=true,OutputChannel="Commands",Targets=new(){a}};
  var second=new RemoteJob{Manual56=true,OutputChannel="Commands",Targets=new(){b}};
  a.Status=b.Status="Sending";
  var receipt=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  int firstCalls=0,secondCalls=0;
  History.Insert(0,first);History.Insert(0,second);
  try{
   var uncertain=SendOne(first,a,deploy:(_,_)=>{firstCalls++;return receipt.Task;});
   await SendOne(second,b,deploy:(_,_)=>{secondCalls++;return Task.CompletedTask;});
   if(uncertain.IsCompleted||b.Status!="Submitted")throw new Exception("Ready peer was blocked by another host's receipt");
   receipt.SetException(new IOException("DC_SUBMISSION_UNKNOWN|WinRM disconnected after dispatch"));
   await uncertain;
   if(a.Status!="SubmissionUnknown"||!SubmissionCanStillRun(a,false)||firstCalls!=1||secondCalls!=1||b.Status!="Submitted")
    throw new Exception("Partial manual dispatch lost or replayed an indeterminate result");
  }finally{
   History.Remove(first);History.Remove(second);
   confirmedJobs.Remove(first.Id+a.Host);confirmedJobs.Remove(second.Id+b.Host);
   Save();
  }
 }
}
