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
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr target, uint flags);
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    public static bool ClickVisible(IntPtr window, int x, int y) {
        SetForegroundWindow(window);
        var point = new Point { X = x, Y = y };
        if (GetAncestor(WindowFromPoint(point), 2) != window || !SetCursorPos(x, y)) return false;
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        return true;
    }
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
