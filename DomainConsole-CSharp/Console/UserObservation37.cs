using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
namespace DomainConsole;
public sealed class InteractiveUser37 {public string Account {get;set;}="";public bool Active {get;set;}}
public sealed class UserObservation37 {public string PreviousName37 {get;set;}="";public string PreviousSeen37 {get;set;}="";public string Account {get;set;}="";public string Name {get;set;}="";public string SeenUtc {get;set;}="";public string ReceivedUtc {get;set;}="";public bool Current {get;set;}}
public sealed class UserName37 {public string Name {get;set;}="";public string CheckedUtc {get;set;}="";}
public partial class MainWindow {
 readonly Dictionary<string,UserObservation37> users37=new(Store.Read("client-users37.json",new Dictionary<string,UserObservation37>()),StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,UserName37> userNames37=new(Store.Read("user-names37.json",new Dictionary<string,UserName37>()),StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> resolvingUsers37=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,string> sessionReadErrors=new(StringComparer.OrdinalIgnoreCase);
 public static string UserLabel37(UserObservation37? value,bool online){
  if(value==null)return "Сведения о пользователе не получены";
  string name=value.Name.Length>0?value.Name:value.PreviousName37;
  string seen=value.Name.Length>0?value.SeenUtc:value.PreviousSeen37;
  if(name.Length==0){
   if(!online)return "Сведения о пользователе не получены";
   return value.Current?"Имя пользователя не получено":"Интерактивных сеансов нет";
  }
  return (value.Current&&online&&value.Name.Length>0?"":"Последний: ")+name+" · "+
   (DateTimeOffset.TryParse(seen,out _)?Display36.Date(seen):"время неизвестно");
 }
 void RefreshUser37(ComputerRow row){
  var value=users37.GetValueOrDefault(row.Host);
  string error=sessionReadErrors.GetValueOrDefault(row.Host,"");
  bool fresh=value!=null&&error.Length==0&&ConnectionFresh(row.Host)&&
   DateTimeOffset.TryParse(value.ReceivedUtc,out var at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromSeconds(90);
  var label=UserLabel37(value,fresh);
  if(row.UserText37!=label){row.UserText37=label;row.Refresh();}
  if(CurrentHost(row.Host)){
   SetUserHeader39(row.UserText37);
   HeaderUser37.ToolTip=label+(error.Length>0?"\nОпрос сеансов не завершён: "+error:"");
  }
 }
 void ObserveUsers37(string host,RebootSnapshot snapshot){
  if(!snapshot.UsersComplete37){
   sessionReadErrors[host]=snapshot.UserError37.Length>0?snapshot.UserError37:"Список интерактивных сеансов не получен";
   return;
  }
  sessionReadErrors.Remove(host);
  var now=snapshot.CollectedUtc;if(!DateTimeOffset.TryParse(now,out _))return;
  var old=users37.GetValueOrDefault(host);var candidates=snapshot.Users37.Where(u=>u.Account.Length>0).ToList();
  var selected=candidates.FirstOrDefault(u=>u.Account.Equals(old?.Account,StringComparison.OrdinalIgnoreCase))??candidates.OrderByDescending(u=>u.Active).ThenBy(u=>u.Account,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
  if(selected==null){
   var observation=old??new UserObservation37();
   observation.Current=false;
   observation.ReceivedUtc=DateTime.UtcNow.ToString("o");
   users37[host]=observation;
   Store.Write("client-users37.json",users37);
   return;
  }
  bool same=old?.Account.Equals(selected.Account,StringComparison.OrdinalIgnoreCase)==true;
  var name=userNames37.GetValueOrDefault(selected.Account);string display=name?.Name.Length>0?name.Name:same?old!.Name:"";
  // An unresolved new account must not be attributed the previous person's name.
  users37[host]=new(){PreviousName37=old?.Name.Length>0?old.Name:old?.PreviousName37??"",PreviousSeen37=old?.Name.Length>0?old.SeenUtc:old?.PreviousSeen37??"",Account=selected.Account,Name=display,Current=true,SeenUtc=now,ReceivedUtc=DateTime.UtcNow.ToString("o")};Store.Write("client-users37.json",users37);
  if(name==null||!DateTimeOffset.TryParse(name.CheckedUtc,out var checkedAt)||DateTimeOffset.UtcNow-checkedAt>TimeSpan.FromHours(name.Name.Length>0?24:1))_=ResolveUser37(selected.Account);
 }
 async Task ResolveUser37(string account){if(!resolvingUsers37.Add(account))return;try{
  string name=await Task.Run(()=>DirectoryUserName37(account));if(closing)return;userNames37[account]=new(){Name=name,CheckedUtc=DateTime.UtcNow.ToString("o")};Store.Write("user-names37.json",userNames37);
  foreach(var entry in users37.Where(x=>x.Value.Account.Equals(account,StringComparison.OrdinalIgnoreCase))){if(name.Length>0)entry.Value.Name=name;var row=Computers.FirstOrDefault(x=>x.Host.Equals(entry.Key,StringComparison.OrdinalIgnoreCase));if(row!=null)RefreshUser37(row);}Store.Write("client-users37.json",users37);
 }catch(Exception ex){userNames37[account]=new(){CheckedUtc=DateTime.UtcNow.ToString("o")};Store.Write("user-names37.json",userNames37);var folder=System.IO.Path.Combine(Store.Root,"Diagnostics");System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"user-name37.log"),ex.ToString());}finally{resolvingUsers37.Remove(account);}}
 static string DirectoryUserName37(string account){
  var sid=(SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));var bytes=new byte[sid.BinaryLength];sid.GetBinaryForm(bytes,0);string filter=string.Concat(bytes.Select(b=>"\\"+b.ToString("X2")));
  using var root=new DirectoryEntry("LDAP://RootDSE");using var directory=new DirectoryEntry("LDAP://"+(string?)root.Properties["defaultNamingContext"].Value);using var search=new DirectorySearcher(directory){Filter="(&(objectCategory=person)(objectClass=user)(objectSid="+filter+"))",ClientTimeout=TimeSpan.FromSeconds(3),ServerTimeLimit=TimeSpan.FromSeconds(3)};
  search.PropertiesToLoad.AddRange(new[]{"givenName","sn","displayName"});var result=search.FindOne();if(result==null)return "";string Get(string key)=>result.Properties[key].Count>0?result.Properties[key][0]?.ToString()?.Trim()??"":"";
  string given=Get("givenName"),surname=Get("sn");if(given.Length>0&&surname.Length>0)return given+" "+surname;string display=Get("displayName");return display.Contains(' ')&&!display.Contains('\\')&&!display.Contains('@')?display:"";
 }
}
public sealed partial class RemoteService {
 public const string UserProbeScript37="""
$dcUsers37=@();$dcUsersComplete37=$false;$dcUserError37=''
try{
 if(-not ('DcSessions37' -as [type])){Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;using System.Collections.Generic;
public static class DcSessions37 {
 [StructLayout(LayoutKind.Sequential)]public struct Session {public int Id;public IntPtr Station;public int State;}
 [DllImport("wtsapi32.dll",EntryPoint="WTSEnumerateSessionsW",SetLastError=true)]static extern bool Enumerate(IntPtr server,int reserved,int version,out IntPtr data,out int count);
 [DllImport("wtsapi32.dll",EntryPoint="WTSQuerySessionInformationW",SetLastError=true)]static extern bool Query(IntPtr server,int session,int kind,out IntPtr data,out int bytes);
 [DllImport("wtsapi32.dll")]static extern void WTSFreeMemory(IntPtr data);
 static string Text(int id,int kind){IntPtr data;int count;if(!Query(IntPtr.Zero,id,kind,out data,out count))throw new System.ComponentModel.Win32Exception();try{return Marshal.PtrToStringUni(data)??"";}finally{WTSFreeMemory(data);}}
 public static string[] Read(){IntPtr data;int count;if(!Enumerate(IntPtr.Zero,0,1,out data,out count))throw new System.ComponentModel.Win32Exception();try{var users=new List<string>();for(int i=0;i<count;i++){var s=(Session)Marshal.PtrToStructure(IntPtr.Add(data,i*Marshal.SizeOf(typeof(Session))),typeof(Session));if(s.Id==0||(s.State!=0&&s.State!=4))continue;string user=Text(s.Id,5);if(user.Length==0)continue;string domain=Text(s.Id,7);if(user.Length>0)users.Add((s.State==0?"1":"0")+"|"+domain+"\\"+user);}return users.ToArray();}finally{WTSFreeMemory(data);}}
}
'@}
 $dcUsers37=@([DcSessions37]::Read()|ForEach-Object {$parts=$_ -split '\|',2;@{Account=$parts[1];Active=($parts[0] -eq '1')}});$dcUsersComplete37=$true
}catch{$dcUserError37=$_.Exception.Message}
""";
}
