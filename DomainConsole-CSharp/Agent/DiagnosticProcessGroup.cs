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
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int infoClass,IntPtr info,uint size);
  [StructLayout(LayoutKind.Sequential)]struct BasicLimits {public long PerProcessUserTime,PerJobUserTime;public uint Flags;public UIntPtr MinimumWorkingSet,MaximumWorkingSet;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;}
  [StructLayout(LayoutKind.Sequential)]struct IoCounters {public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
  [StructLayout(LayoutKind.Sequential)]struct ExtendedLimits {public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
  IntPtr handle;
  public DiagnosticProcessGroup(){handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)throw new Win32Exception();var limits=new ExtendedLimits();limits.Basic.Flags=0x2000;int size=Marshal.SizeOf(typeof(ExtendedLimits));var memory=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(limits,memory,false);if(!SetInformationJobObject(handle,9,memory,(uint)size)){int error=Marshal.GetLastWin32Error();CloseHandle(handle);handle=IntPtr.Zero;throw new Win32Exception(error);}}finally{Marshal.FreeHGlobal(memory);}}
  public void Attach(Process process){if(!AssignProcessToJobObject(handle,process.Handle)){try{process.Kill();}catch{}throw new Win32Exception();}}
  public void Stop(){if(handle!=IntPtr.Zero&&!TerminateJobObject(handle,124))throw new Win32Exception();}
  public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
 }
}
