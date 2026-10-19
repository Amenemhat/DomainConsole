using System;
using System.Collections.Generic;
using Microsoft.Win32;
namespace DomainConsole.Shared {
 public sealed class RebootEvidence {
  public bool? Required {get;set;}public string CollectedUtc {get;set;}="";public string Error {get;set;}="";public List<string> Sources {get;set;}=new List<string>();public List<string> PendingFileOperations {get;set;}=new List<string>();public int ProbeVersion {get;set;}
  public static RebootEvidence Collect(){var r=new RebootEvidence{CollectedUtc=DateTime.UtcNow.ToString("o"),ProbeVersion=2};var errors=new List<string>();
   object info=null;try{info=Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.SystemInfo",true));if((bool)info.GetType().InvokeMember("RebootRequired",System.Reflection.BindingFlags.GetProperty,null,info,null))r.Sources.Add("Windows Update API");}catch(Exception ex){errors.Add("WUA: "+ex.Message);}finally{if(info!=null&&System.Runtime.InteropServices.Marshal.IsComObject(info))System.Runtime.InteropServices.Marshal.ReleaseComObject(info);}
   try{using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,Environment.Is64BitOperatingSystem?RegistryView.Registry64:RegistryView.Registry32)){
    foreach(var path in new[]{@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"}){try{using(var key=root.OpenSubKey(path)){if(key!=null)r.Sources.Add(path);}}catch(Exception ex){errors.Add(path+": "+ex.Message);}}
    try{using(var key=root.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager")){if(key==null)errors.Add("Session Manager: раздел недоступен");else{var pending=key.GetValue("PendingFileRenameOperations") as string[];if(pending!=null)r.PendingFileOperations.AddRange(pending);}}}catch(Exception ex){errors.Add("PendingFileRenameOperations: "+ex.Message);}
   }}catch(Exception ex){errors.Add("Registry: "+ex.Message);}
   r.Error=string.Join("; ",errors);r.Required=r.Sources.Count>0?(bool?)true:errors.Count>0?null:(bool?)false;return r;
  }
 }
}

