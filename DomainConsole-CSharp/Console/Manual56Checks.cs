using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyManual56(){
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
   foreach(var width in new[]{1280d,1920d}){
    Width=width;CollectorStage53.Text=new string('Ж',200);CollectorStage53.Visibility=Visibility.Visible;UpdateLayout();
    if(Math.Abs(HeaderLink.ActualWidth-130)>1||Math.Abs(CollectorDetails54.ActualWidth-68)>1)throw new Exception("Header controls resized with collector activity");
   }
  }finally{backgroundRelease.TrySetResult("done");if(background!=null)await background;PollingControl43.Resume();if(paused)PollingControl43.Pause();Width=originalWidth;ShowCurrent();}
 }
}
