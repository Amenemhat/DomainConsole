using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace DomainConsole.Agent {
 // Only diagnostic subprocesses are attached. No servicing process is terminated.
 internal sealed class DiagnosticProcessGroup:IDisposable {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr attributes,string name);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateJobObject(IntPtr job,uint code);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
  IntPtr handle;
  public DiagnosticProcessGroup(){handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)throw new Win32Exception();}
  public void Attach(Process process){if(!AssignProcessToJobObject(handle,process.Handle)){try{process.Kill();}catch{}throw new Win32Exception();}}
  public void Stop(){if(handle!=IntPtr.Zero&&!TerminateJobObject(handle,124))throw new Win32Exception();}
  public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
 }
}
