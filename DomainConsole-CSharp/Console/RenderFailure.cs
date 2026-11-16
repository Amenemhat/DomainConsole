using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace DomainConsole;

public partial class App {
 static int renderFailure;
 static System.Threading.Timer? exitFallback;
 internal static bool IsRenderFailure(Exception? error){
  for(;error!=null;error=error.InnerException)
   if(error.HResult==unchecked((int)0x88980406)||error.Message.Contains("UCEERR_RENDERTHREADFAILURE",StringComparison.OrdinalIgnoreCase))return true;
  return false;
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]
 static extern int MessageBoxW(IntPtr owner,string text,string title,uint flags);
 [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
 internal static bool HandleRenderFailure(Exception error){
  if(!IsRenderFailure(error))return false;
  if(Interlocked.Exchange(ref renderFailure,1)!=0)return true;
  // The native dialog and filesystem can both stall after a renderer failure.
  exitFallback=new System.Threading.Timer(_=>Environment.Exit(1),null,TimeSpan.FromSeconds(45),Timeout.InfiniteTimeSpan);
  try{if(Current.MainWindow is MainWindow window)window.StopAfterRenderFailure();}catch{}
  string diagnostic="Не удалось сохранить диагностический файл в Data/Diagnostics.";
  try{
   var folder=Path.Combine(Store.Root,"Diagnostics");Directory.CreateDirectory(folder);
   var path=Path.Combine(folder,"last-render-failure.txt");
   var lines=new List<string>{DateTimeOffset.Now.ToString("O"),"DomainConsole "+typeof(App).Assembly.GetName().Version,Environment.OSVersion.ToString(),RuntimeInformation.FrameworkDescription};
   try{
    using var process=Process.GetCurrentProcess();
    lines.Add($"Session={process.SessionId}, remote={GetSystemMetrics(0x1000)!=0}, render={System.Windows.Media.RenderOptions.ProcessRenderMode}");
    lines.Add($"WorkingSet={process.WorkingSet64}, private={process.PrivateMemorySize64}, handles={process.HandleCount}");
   }catch{}
   for(Exception? cause=error;cause!=null;cause=cause.InnerException)lines.Add($"{cause.GetType().FullName} HRESULT 0x{cause.HResult:X8}");
   lines.Add(error.ToString());
   string details=string.Join("\n",lines);
   File.WriteAllText(path,details[..Math.Min(details.Length,32768)]);diagnostic="Диагностический файл: "+path;
  }catch{}
  try{MessageBoxW(IntPtr.Zero,"Сбой отрисовки. Консоль завершит работу. "+diagnostic+"\nДля программной отрисовки используйте --software-rendering. Переданные клиентам задания продолжаются.","Domain Console",0x10);}
  finally{exitFallback.Change(TimeSpan.FromSeconds(8),Timeout.InfiniteTimeSpan);Current.Shutdown(1);}
  return true;
 }
}
public partial class MainWindow {
 internal void StopAfterRenderFailure(){
  closing=true;closeReady39=true;timer.Stop();rebootTimer.Stop();overviewCancel?.Cancel();surveyRun?.Cancel();PollingControl43.Pause();
 }
}
