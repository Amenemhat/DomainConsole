using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace DomainConsole;

// Runs only in an explicitly selected, isolated Windows acceptance process.
internal static class RenderFaultProbe {
 delegate IntPtr Hook(int code,IntPtr hwnd,IntPtr details);
 static readonly Hook callback=Activated;
 [DllImport("user32.dll",ExactSpelling=true)] static extern IntPtr SetWindowsHookExW(int kind,Hook hook,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr hwnd,IntPtr details);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int max);
 delegate void TimerProc(IntPtr hwnd,uint message,UIntPtr timer,uint time);
 static readonly TimerProc dismiss=Dismiss;
 static IntPtr notice;
 [DllImport("user32.dll")] static extern UIntPtr SetTimer(IntPtr hwnd,UIntPtr id,uint ms,TimerProc callback);
 [DllImport("user32.dll")] static extern bool KillTimer(IntPtr hwnd,UIntPtr id);
 [DllImport("user32.dll")] static extern bool EndDialog(IntPtr hwnd,IntPtr result);
 static void Dismiss(IntPtr hwnd,uint message,UIntPtr timer,uint time){
  KillTimer(IntPtr.Zero,timer);
  File.AppendAllText(trace,"dismiss "+EndDialog(notice,new IntPtr(1))+"\n");
 }
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 static readonly string trace=Path.Combine(AppContext.BaseDirectory,"render-test.txt");
 static IntPtr Activated(int code,IntPtr hwnd,IntPtr details){
  if(code==5){
   var title=new StringBuilder(256);GetWindowText(hwnd,title,title.Capacity);
   File.AppendAllText(trace,"activated "+title+"\n");
   if(title.ToString()=="Domain Console"){
    // Let MessageBox finish initializing before sending the user's OK.
    notice=hwnd;
    App.HandleRenderFailure(new COMException("second fault during the modal notification",unchecked((int)0x88980406)));
    if(Environment.GetEnvironmentVariable("DOMAINCONSOLE_TEST_UNANSWERED_DIALOG")!="1")SetTimer(IntPtr.Zero,UIntPtr.Zero,250,dismiss);
   }
  }
  return CallNextHookEx(IntPtr.Zero,code,hwnd,details);
 }
 internal static void Start(Dispatcher dispatcher){
  File.WriteAllText(trace,"startup "+System.Windows.Media.RenderOptions.ProcessRenderMode+"\n");
  if(SetWindowsHookExW(5,callback,IntPtr.Zero,GetCurrentThreadId())==IntPtr.Zero)throw new IOException("Could not install the test window hook");
  var error=new COMException("UCEERR_RENDERTHREADFAILURE fault injection",unchecked((int)0x88980406));
  dispatcher.BeginInvoke(new Action(()=>{File.AppendAllText(trace,"dispatcher fault\n");throw error;}));
  dispatcher.BeginInvoke(new Action(()=>App.HandleRenderFailure(error)));
 }
}
