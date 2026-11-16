$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class RenderNotice {
    public delegate bool Visitor(IntPtr hwnd, IntPtr unused);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Visitor fn, IntPtr arg);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd,System.Text.StringBuilder text,int length);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr hwnd, int id);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    public static void CloseNotices(int pid, System.Collections.Generic.HashSet<IntPtr> seen) {
        EnumWindows(delegate(IntPtr hwnd, IntPtr unused) {
            int owner; GetWindowThreadProcessId(hwnd,out owner);
            if (owner != pid) return true;
            var name=new System.Text.StringBuilder(80); GetClassName(hwnd,name,name.Capacity);
            if(name.ToString()=="#32770" && IsWindowVisible(hwnd)){
                IntPtr ok=GetDlgItem(hwnd,1);
                if(ok!=IntPtr.Zero){seen.Add(hwnd);PostMessage(ok,0x00f5,IntPtr.Zero,IntPtr.Zero);}
            }
            return true;
        },IntPtr.Zero);
    }
}
'@
$folder=Join-Path $env:RUNNER_TEMP 'DomainConsole-render-test'
New-Item $folder -ItemType Directory -Force | Out-Null
Copy-Item Publish/DomainConsole.exe $folder
$env:DOMAINCONSOLE_RENDER_TEST='1'
$info=[Diagnostics.ProcessStartInfo]::new((Join-Path $folder 'DomainConsole.exe'),'--test-render-failure --software-rendering')
$info.UseShellExecute=$false
$p=[Diagnostics.Process]::Start($info)
$seen=[Collections.Generic.HashSet[IntPtr]]::new()
try {
    $until=[DateTime]::UtcNow.AddSeconds(40)
    while (!$p.HasExited -and [DateTime]::UtcNow -lt $until) {
        [RenderNotice]::CloseNotices($p.Id,$seen)
        Start-Sleep -Milliseconds 100
    }
    if (!$p.WaitForExit(2000) -or $p.ExitCode -ne 1 -or $seen.Count -ne 1){throw "Expected one native notice and exit 1; got $($seen.Count) and $($p.ExitCode)"}
    $log=Get-Item (Join-Path $folder 'Data/Diagnostics/last-render-failure.txt')
    if ($log.Length -gt 131072 -or !(Select-String -Path $log.FullName -Pattern 'UCEERR_RENDERTHREADFAILURE' -Quiet)){throw 'Missing bounded renderer diagnostic'}
} finally {
    if (!$p.HasExited){$p.Kill()}
    Remove-Item Env:DOMAINCONSOLE_RENDER_TEST
}
