using System.Windows;
using System.IO;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyManual56(){
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"collector56-control.ps1"),RemoteService.ControlScript56(true));
  double originalWidth=Width;bool paused=TransportBudget41.Paused;var target=new TargetRecord{Host="manual56.fixture.invalid"};string code="# DC-ENDPOINT41:"+TransportBudget41.Key(target)+"\nfixture";
  var backgroundRelease=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);Task<string>? background=null;
  try{
   PollingControl43.Resume();
   using(PollingControl43.Read())background=TransportBudget41.Run(code,CancellationToken.None,_=>backgroundRelease.Task);
   PollingControl43.Pause();
   using(PollingControl43.Manual56()){
    using var read=PollingControl43.Read();
    if(PollingControl43.Token.IsCancellationRequested||PollingControl43.BackgroundPaused56)throw new Exception("Manual work inherited paused generation");
    var result=await TransportBudget41.Run(code,CancellationToken.None,_=>Task.FromResult("manual delivered"));
    if(result!="manual delivered"||background.IsCompleted)throw new Exception("Manual deployment blocked by/background work cancelled");
    var hold=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var first=TransportBudget41.Run(code,CancellationToken.None,_=>hold.Task);
    try{
     string waiting="";void Activity(string key,string text){if(key==TransportBudget41.Key(target))waiting=text;}
     PollingControl43.Activity+=Activity;
     try{using var deadline=new CancellationTokenSource(100);bool cancelled=false;try{await TransportBudget41.Run(code,deadline.Token,_=>throw new Exception("Cancelled wait started a second executor"));}catch(OperationCanceledException){cancelled=true;}if(!cancelled||waiting!="")throw new Exception("Cancelled manual wait retained activity or entered transport");}finally{PollingControl43.Activity-=Activity;}
     using var control=PollingControl43.Manual56("control");
     if(await TransportBudget41.Run(code,CancellationToken.None,_=>Task.FromResult("cancel available"))!="cancel available")throw new Exception("Control blocked behind manual work");
    }finally{hold.SetResult("done");await first;}
   }
   bool blocked=false;try{using var read=PollingControl43.Read();}catch(OperationCanceledException){blocked=true;}if(!blocked)throw new Exception("Manual scope leaked to background reads");
   var job=new RemoteJob{Manual56=true,OutputChannel="Commands",Name="manual paused fixture",Targets=new(){new(){Host=target.Host,Status="Submitted"}}};History.Insert(0,job);var row=new ComputerRow(job.Targets[0],job.Id);var key=job.Id+target.Host;archivedJobs.Add(key);
   try{
    await Poll(row,true,(_,id,output)=>Task.FromResult(new PollResult{State=new(){Id=id,Status="Completed",Started=DateTime.UtcNow.AddSeconds(-1).ToString("o"),Updated=DateTime.UtcNow.ToString("o"),Ended=DateTime.UtcNow.ToString("o")},Output="manual result while background OFF"}));
    if(job.Targets[0].Status!="Completed")throw new Exception("Manual result discarded while polling OFF");
   }finally{History.Remove(job);archivedJobs.Remove(key);}

   // A manual request survives a background pause, but never a changed endpoint.
   foreach(bool changed in new[]{false,true}){
    var host="manual-endpoint56.fixture.invalid";ConnectionStore.Profiles.TryGetValue(host,out var prior);
    ConnectionStore.Profiles[host]=new(){Address="192.0.2.81",AuthMode="Current",Identity="fixture"};
    var t=ConnectionStore.Capture(new TargetRecord{Host=host});t.Status="Running";
    var j=new RemoteJob{Manual56=true,OutputChannel="Commands",Targets=new(){t}};var r=new ComputerRow(t,j.Id);History.Insert(0,j);archivedJobs.Add(j.Id+host);
    try{
     var reply=new TaskCompletionSource<PollResult>(TaskCreationOptions.RunContinuationsAsynchronously);
     var pending=Poll(r,true,(_,_,_)=>reply.Task);
     PollingControl43.Resume();PollingControl43.Pause();
     if(changed)ConnectionStore.Profiles[host].Address="192.0.2.82";
     reply.SetResult(new(){State=new(){Id=j.Id,Status="Completed",Ended=DateTime.UtcNow.ToString("o")}});await pending;
     if(t.Status!=(changed?"Running":"Completed"))throw new Exception("Manual result lifetime/endpoint guard failed");
    }finally{History.Remove(j);archivedJobs.Remove(j.Id+host);clientConnections.Remove(host);liveConnections.Remove(host);if(prior==null)ConnectionStore.Profiles.Remove(host);else ConnectionStore.Profiles[host]=prior;}
   }
   // Explicit refresh replaces a stuck background observation, not the remote job.
   PollingControl43.Resume();
   var pendingTarget=new TargetRecord{Host="replace-observation56.fixture.invalid",Status="Running"};
   var pendingJob=new RemoteJob{Targets=new(){pendingTarget}};var pendingRow=new ComputerRow(pendingTarget,pendingJob.Id);History.Insert(0,pendingJob);archivedJobs.Add(pendingJob.Id+pendingTarget.Host);
   try{
    var delayed=new TaskCompletionSource<PollResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    var old=Poll(pendingRow,false,(_,_,_)=>delayed.Task);
    await Poll(pendingRow,true,(_,_,_)=>Task.FromResult(new PollResult{State=new(){Id=pendingJob.Id,Status="Completed",Ended=DateTime.UtcNow.ToString("o")}}));
    if(pendingTarget.Status!="Completed")throw new Exception("Manual refresh blocked by previous observation");
    delayed.SetResult(new(){State=new(){Id=pendingJob.Id,Status="Running"}});await old;
    if(pendingTarget.Status!="Completed")throw new Exception("Late replaced observation overwrote manual result");
   }finally{History.Remove(pendingJob);archivedJobs.Remove(pendingJob.Id+pendingTarget.Host);clientConnections.Remove(pendingTarget.Host);liveConnections.Remove(pendingTarget.Host);}
   int theme56=Theme.Current;
   try{foreach(int theme in new[]{0,1,2}){
    Theme.Apply(theme,false);
    var view=await ShowManualJobs56(Current(),_=>Task.FromResult("{\"Rows\":[{\"Id\":\"00000000-0000-0000-0000-000000000056\",\"Status\":\"Результат отсутствует\",\"Stage\":\"Передача не завершена\",\"Updated\":\"\",\"Ended\":\"\",\"Diagnostics\":\"\",\"Restore\":\"\",\"Tasks\":[],\"Processes\":[],\"CanRelease\":true}],\"Removed\":[],\"Owner\":\"Fixture\",\"Message\":\"Прочитана одна оставшаяся резервация\",\"Backup\":\"\"}"));
    try{view.UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(Path.GetTempPath(),"ui-manual56-"+theme+".png"));encoder.Save(file);}finally{view.Close();}
   }}finally{Theme.Apply(theme56,false);}
   UpdateLayout();double linkWidth61=HeaderLink.ActualWidth,pollWidth61=CollectorDetails54.ActualWidth;
   foreach(var width in new[]{1280d,1920d}){
    Width=width;CollectorStage53.Text=new string('Ж',200);CollectorStage53.Visibility=Visibility.Visible;UpdateLayout();
    if(Math.Abs(HeaderLink.ActualWidth-linkWidth61)>1||Math.Abs(CollectorDetails54.ActualWidth-pollWidth61)>1)throw new Exception("Header controls resized with collector activity");
    if(HeaderLink.Padding!=CollectorDetails54.Padding||HeaderLink.Padding!=HeaderReboot.Padding||HeaderLink.ActualHeight!=CollectorDetails54.ActualHeight||HeaderLink.ActualHeight!=HeaderReboot.ActualHeight)throw new Exception("Header action spacing is inconsistent");
   }
  }finally{backgroundRelease.TrySetResult("done");if(background!=null)await background;PollingControl43.Resume();if(paused)PollingControl43.Pause();Width=originalWidth;ShowCurrent();}
 }
}
