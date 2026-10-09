using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 public sealed class FakeProgress {public int PercentComplete {get;set;}public string TotalBytesDownloaded {get;set;}="0";public string TotalBytesToDownload {get;set;}="1000";}
 public sealed class FakeOperation {public volatile bool completed;public bool IsCompleted=>completed;public bool Aborted;public FakeProgress Progress=new FakeProgress();public object GetProgress()=>Progress;public void RequestAbort(){Aborted=true;}}
 internal static partial class Program {
  static int UpdateSmoke(string folder){Directory.CreateDirectory(folder);Folder=folder;Job=new RemoteJob{OutputChannel="WSUS"};State=new RemoteState{Status="Running"};Stage("Скачивание · fixture");var callback=new UpdateCallback();foreach(var type in new[]{typeof(ISearchDone),typeof(IDownloadDone),typeof(IInstallDone),typeof(IDownloadChanged),typeof(IInstallChanged)}){IntPtr ptr=Marshal.GetComInterfaceForObject(callback,type);Marshal.Release(ptr);}var fake=new FakeOperation();var worker=Task.Run(()=>WaitUpdate(fake,"Скачивание"));Thread.Sleep(1200);fake.Progress.PercentComplete=30;fake.Progress.TotalBytesDownloaded="300";Thread.Sleep(1200);File.WriteAllText(FilePath("stop-now.flag"),"");Thread.Sleep(1500);if(!fake.Aborted||State.StopStatus!="AbortRequested"||worker.IsCompleted)throw new Exception("Cancellation incorrectly reported operation completed");fake.completed=true;if(!worker.Wait(5000))throw new Exception("Operation completion not observed");if(State.Progress!=30||!State.TransferText.Contains("МБ")||string.IsNullOrEmpty(State.LastMovementUtc))throw new Exception("Download progress missing");File.Delete(FilePath("stop-now.flag"));if(!ExecutionLane().EndsWith("WSUS"))throw new Exception("WSUS lane missing");Job.OutputChannel="Commands";if(!ExecutionLane().EndsWith("Commands"))throw new Exception("Separate command lane missing");ApplyRecommendation("0x80240034");if(State.RecommendationAction!="Download")throw new Exception("Download recommendation missing");Console.WriteLine("WUA callbacks, download progress, cancellation acknowledgement and execution lanes verified");return 0;}
 }
}
