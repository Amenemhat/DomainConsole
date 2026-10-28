using System.IO;
using System.Text;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease43(){VerifyRelease44();VerifyRelease45();VerifyRelease46();
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"collector43-remove.ps1"),RemoteService.RemoveCollectorScript43(),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"collector43-enable.ps1"),RemoteService.RemoveCollectorScript43("Enable"),new UTF8Encoding(true));
  AwaitUi39(VerifyPause43());
  AwaitUi39(VerifyOverviewCancellation47());
  var current43=Current();current43.SetService43("Чтение состояния задания");CaptureContent38("release43-service",1000,640);current43.SetService43("");
  var row=Current();var old=readyProofs43.GetValueOrDefault(row.Host);CachedWsusReport? cache=null;var now=DateTimeOffset.UtcNow;var reboot=new RebootView40(false,true,now.ToString("o"),Array.Empty<string>(),"");
  try{readyProofs43.Remove(row.Host);var warning=new ClientAssessment41("Warning","Проверка неполная","");if(PreserveReady43(row,warning,false,null,cache,reboot,"").Kind=="Success")throw new Exception("Unknown result became success");PreserveReady43(row,new("Success","Проверено", ""),false,null,cache,reboot,"scan");var held=PreserveReady43(row,new("Stale","Снимок устарел", ""),false,null,cache,reboot with{Fresh=false},"scan");if(held.Kind!="Success")throw new Exception("Clock aging erased successful proof");if(PreserveReady43(row,new("Needed","К установке: 1", ""),false,null,cache,reboot,"scan").Kind!="Needed")throw new Exception("New update hidden by saved green");}
  finally{if(old==null)readyProofs43.Remove(row.Host);else readyProofs43[row.Host]=old;Store.Write("ready-proofs43.json",readyProofs43);}
 }
 static async Task VerifyOverviewCancellation47(){
  bool paused=TransportBudget41.Paused;
  try{
   foreach(bool resume in new[]{false,true}){
    PollingControl43.Resume();using var cancel=CancellationTokenSource.CreateLinkedTokenSource(PollingControl43.Token);
    var source=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    var pending=AwaitOverviewResult47(source.Task,cancel.Token);PollingControl43.Pause();if(resume)PollingControl43.Resume();source.SetResult(7);
    bool rejected=false;try{await pending;}catch(OperationCanceledException){rejected=true;}
    if(!rejected)throw new Exception("Old overview result applied after OFF or OFF/ON");
   }
   PollingControl43.Resume();using var fresh=CancellationTokenSource.CreateLinkedTokenSource(PollingControl43.Token);
   if(await AwaitOverviewResult47(Task.FromResult(9),fresh.Token)!=9)throw new Exception("Fresh overview response discarded");
   fresh.Cancel();bool stopped=false;try{await AwaitOverviewResult47(Task.FromResult(11),fresh.Token);}catch(OperationCanceledException){stopped=true;}
   if(!stopped)throw new Exception("Stopped overview response applied");
  }finally{if(paused)PollingControl43.Pause();else PollingControl43.Resume();}
 }
 static async Task VerifyPause43(){bool paused=TransportBudget41.Paused;try{PollingControl43.Resume();using(var read=PollingControl43.Read()){var token=PollingControl43.Token;PollingControl43.Pause();if(!token.IsCancellationRequested)throw new Exception("Pause did not cancel read token");int calls=0;bool blocked=false;try{await PowerShellBridge.Execute("throw 'must not execute'");}catch(OperationCanceledException){blocked=true;}if(!blocked||calls!=0)throw new Exception("Paused read reached PowerShell");PollingControl43.Resume();if(!PollingControl43.Token.IsCancellationRequested)throw new Exception("Old read generation revived on resume");}PollingControl43.Resume();using(PollingControl43.Read()){if(PollingControl43.Token.IsCancellationRequested)throw new Exception("Resume reused cancelled generation");}PollingControl43.Pause();bool denied=false;try{using var read=PollingControl43.Read();}catch(OperationCanceledException){denied=true;}if(!denied)throw new Exception("Read scope admitted during OFF");}finally{if(paused)PollingControl43.Pause();else PollingControl43.Resume();}}
}

