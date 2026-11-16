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
 internal static bool HandleRenderFailure(Exception error){
  if(!IsRenderFailure(error))return false;
  if(Interlocked.Exchange(ref renderFailure,1)!=0)return true;
  try{if(Current.MainWindow is MainWindow window)window.StopAfterRenderFailure();}catch{}
  try{
   var folder=Path.Combine(Store.Root,"Diagnostics");Directory.CreateDirectory(folder);
   string details=DateTimeOffset.Now.ToString("O")+" · "+typeof(App).Assembly.GetName().Version+" · "+Environment.OSVersion+"\n"+error;
   File.WriteAllText(Path.Combine(folder,"last-render-failure.txt"),details[..Math.Min(details.Length,32768)]);
  }catch{}
  try{MessageBoxW(IntPtr.Zero,"Сбой отрисовки. Закройте уведомление и запустите консоль снова. Для программной отрисовки используйте --software-rendering. Переданные клиентам задания продолжаются.","Domain Console",0x10);}
  finally{exitFallback=new System.Threading.Timer(_=>Environment.Exit(1),null,TimeSpan.FromSeconds(8),Timeout.InfiniteTimeSpan);Current.Shutdown(1);}
  return true;
 }
}
public partial class MainWindow {
 internal void StopAfterRenderFailure(){
  closing=true;closeReady39=true;timer.Stop();rebootTimer.Stop();overviewCancel?.Cancel();surveyRun?.Cancel();PollingControl43.Pause();
 }
}
