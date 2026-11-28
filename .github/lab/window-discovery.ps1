Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
public static class DCLabNativeWindows {
    delegate bool Visitor(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(int thread, Visitor callback, IntPtr data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder title, int size);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int size);
    [DllImport("user32.dll", EntryPoint="PostMessageW", SetLastError=true)]
    public static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    public static IntPtr Locate(int process, string title, bool prefix) {
        var match=IntPtr.Zero;
        foreach(ProcessThread thread in Process.GetProcessById(process).Threads) {
            EnumThreadWindows(thread.Id,delegate(IntPtr h,IntPtr unused) {
                var text=new StringBuilder(600); GetWindowText(h,text,text.Capacity);
                bool same=prefix?text.ToString().StartsWith(title,StringComparison.OrdinalIgnoreCase):text.ToString().Equals(title,StringComparison.OrdinalIgnoreCase);
                if(same){match=h;return false;}
                return true;
            },IntPtr.Zero);
            if(match!=IntPtr.Zero)break;
        }
        return match;
    }
    public static string Inventory(int process) {
        var names=new List<string>();
        foreach(ProcessThread thread in Process.GetProcessById(process).Threads) {
            EnumThreadWindows(thread.Id,delegate(IntPtr h,IntPtr unused){
                var text=new StringBuilder(600);var name=new StringBuilder(120);
                GetWindowText(h,text,text.Capacity);GetClassName(h,name,name.Capacity);
                names.Add(name.ToString()+"["+text.ToString()+"]");return true;
            },IntPtr.Zero);
        }
        return string.Join(", ",names);
    }
}
'@
function Application-Window($title,[bool]$prefix=$false){
 $process.Refresh()
 if($process.HasExited){throw "Application process exited with code $($process.ExitCode)"}
 $handle=[DCLabNativeWindows]::Locate($process.Id,$title,$prefix)
 if($handle -ne [IntPtr]::Zero){return [Windows.Automation.AutomationElement]::FromHandle($handle)}
}

function Close-ApplicationWindow {
 $handle=[DCLabNativeWindows]::Locate($process.Id,'DomainConsole ·',$true)
 if($handle -eq [IntPtr]::Zero -or ![DCLabNativeWindows]::PostMessage($handle,0x0010,[IntPtr]::Zero,[IntPtr]::Zero)){
   throw 'Could not send WM_CLOSE to the verified DomainConsole window'
 }
}
