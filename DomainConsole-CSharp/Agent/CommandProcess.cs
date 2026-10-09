using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace DomainConsole.Agent {
 // An isolated hidden console allows Ctrl+C without signalling the executor or other jobs.
 internal sealed class CommandProcess:IDisposable {
  [StructLayout(LayoutKind.Sequential)]struct Security {public int Length;public IntPtr Descriptor;[MarshalAs(UnmanagedType.Bool)]public bool Inherit;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Startup {public int Size;public string Reserved,Desktop,Title;public int X,Y,XSize,YSize,XChars,YChars,Fill,Flags;public short Show,ReservedBytes;public IntPtr ReservedPointer,Input,Output,Error;}
  [StructLayout(LayoutKind.Sequential)]struct Info {public IntPtr Process,Thread;public int Pid,Tid;}
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool CreatePipe(out IntPtr read,out IntPtr write,ref Security security,int size);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetHandleInformation(IntPtr handle,uint mask,uint flags);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string application,System.Text.StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string directory,ref Startup startup,out Info info);
  [DllImport("kernel32.dll",SetLastError=true)]static extern uint ResumeThread(IntPtr thread);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool AttachConsole(uint pid);
  [DllImport("kernel32.dll")]static extern bool FreeConsole();
  [DllImport("kernel32.dll")]static extern bool SetConsoleCtrlHandler(IntPtr handler,bool add);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GenerateConsoleCtrlEvent(uint type,uint group);
  public Process Process {get;private set;}public Stream Output {get;private set;}public Stream Error {get;private set;}readonly DiagnosticProcessGroup group=new DiagnosticProcessGroup();
  public CommandProcess(string exe,string args){IntPtr outputRead=IntPtr.Zero,outputWrite=IntPtr.Zero,errorRead=IntPtr.Zero,errorWrite=IntPtr.Zero,inputRead=IntPtr.Zero,inputWrite=IntPtr.Zero;var sa=new Security{Length=Marshal.SizeOf(typeof(Security)),Inherit=true};Info info=new Info();
   try{if(!CreatePipe(out outputRead,out outputWrite,ref sa,0)||!CreatePipe(out errorRead,out errorWrite,ref sa,0)||!CreatePipe(out inputRead,out inputWrite,ref sa,0))throw new Win32Exception();if(!SetHandleInformation(outputRead,1,0)||!SetHandleInformation(errorRead,1,0)||!SetHandleInformation(inputWrite,1,0))throw new Win32Exception();
    var startup=new Startup{Size=Marshal.SizeOf(typeof(Startup)),Flags=0x101,Show=0,Input=inputRead,Output=outputWrite,Error=errorWrite};
    if(!CreateProcess(null,new System.Text.StringBuilder("\""+exe+"\" "+args),IntPtr.Zero,IntPtr.Zero,true,0x10|0x4,IntPtr.Zero,null,ref startup,out info))throw new Win32Exception();
    Process=Process.GetProcessById(info.Pid);var handle=Process.Handle;group.Attach(Process);Output=new FileStream(new SafeFileHandle(outputRead,true),FileAccess.Read);outputRead=IntPtr.Zero;Error=new FileStream(new SafeFileHandle(errorRead,true),FileAccess.Read);errorRead=IntPtr.Zero;if(ResumeThread(info.Thread)==uint.MaxValue)throw new Win32Exception();
   }catch{group.Stop();Dispose();throw;}finally{foreach(var h in new[]{outputRead,outputWrite,errorRead,errorWrite,inputRead,inputWrite,info.Thread,info.Process})if(h!=IntPtr.Zero)CloseHandle(h);}
  }
  public bool Interrupt(){if(Process.HasExited)return true;FreeConsole();if(!AttachConsole((uint)Process.Id))return false;try{SetConsoleCtrlHandler(IntPtr.Zero,true);return GenerateConsoleCtrlEvent(0,0);}finally{FreeConsole();}}
  public void Force()=>group.Stop();
  public void Dispose(){group.Dispose();if(Output!=null)Output.Dispose();if(Error!=null)Error.Dispose();if(Process!=null)Process.Dispose();}
 }
}
