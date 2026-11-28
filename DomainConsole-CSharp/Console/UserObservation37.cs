using System.DirectoryServices;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
namespace DomainConsole;
public sealed class InteractiveUser37 {public string Account {get;set;}="";public bool Active {get;set;}public bool Local {get;set;}}
public sealed class UserObservation37 {public string PreviousName37 {get;set;}="";public string PreviousSeen37 {get;set;}="";public string Account {get;set;}="";public string Name {get;set;}="";public string SeenUtc {get;set;}="";public string ReceivedUtc {get;set;}="";public bool Current {get;set;}public bool Local {get;set;}}
public sealed class UserName37 {
 public string Name {get;set;}="";
 public string CheckedUtc {get;set;}="";
 public bool DirectoryUnavailable {get;set;}
 public bool LookupDue(DateTimeOffset now){
  if(!DateTimeOffset.TryParse(CheckedUtc,out var checkedAt)||checkedAt>now)return true;
  var retry=DirectoryUnavailable?TimeSpan.FromMinutes(2):Name.Length>0?TimeSpan.FromHours(24):TimeSpan.FromHours(1);
  return now-checkedAt>=retry;
 }
}
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
  return name+" · "+
   (DateTimeOffset.TryParse(seen,out _)?Display36.Date(seen):"время неизвестно");
 }
 void RefreshUser37(ComputerRow row){
  var value=users37.GetValueOrDefault(row.Host);
  string sessionError=sessionReadErrors.GetValueOrDefault(row.Host,"");
  bool linkFailed=clientConnections.TryGetValue(row.Host,out var link)&&link.Error.Length>0;
  bool fresh=value!=null&&sessionError.Length==0&&!linkFailed&&ConnectionFresh(row.Host)&&
   DateTimeOffset.TryParse(value.ReceivedUtc,out var at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromSeconds(90);
  var label=UserLabel37(value,fresh);
  var hint=label;
  if(linkFailed)hint+="\nНе удалось обновить сеансы через WinRM. Подробности в проверке связи.";
  if(sessionError.Length>0)hint+="\nОпрос сеансов не завершён: "+sessionError;
  if(value is {Current:true,Local:false,Account.Length:>0}&&
     userNames37.TryGetValue(value.Account,out var lookup)&&lookup.DirectoryUnavailable)
   hint+="\nНе удалось получить имя из Active Directory. Подробности в диагностике.";
  if(row.UserText37!=label||row.UserHint37!=hint){
   row.UserText37=label;
   row.UserHint37=hint;
   row.Refresh();
  }
  if(CurrentHost(row.Host)){
   SetUserHeader39(row.UserText37);
   HeaderUser37.ToolTip=hint;
  }
 }
 void ObserveUsers37(string host,RebootSnapshot snapshot){
  if(!snapshot.UsersComplete37){
   sessionReadErrors[host]=snapshot.UserError37.Length>0?snapshot.UserError37:"Список интерактивных сеансов не получен";
  }else sessionReadErrors.Remove(host);
  var now=snapshot.CollectedUtc;if(!DateTimeOffset.TryParse(now,out _))return;
  var old=users37.GetValueOrDefault(host);var candidates=snapshot.Users37.Where(u=>u.Account.Length>0).ToList();
  var selected=candidates.FirstOrDefault(u=>u.Account.Equals(old?.Account,StringComparison.OrdinalIgnoreCase))??candidates.OrderByDescending(u=>u.Active).ThenBy(u=>u.Account,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
  if(selected==null){
   if(!snapshot.UsersComplete37)return;
   var observation=old??new UserObservation37();
   observation.Current=false;
   observation.ReceivedUtc=DateTime.UtcNow.ToString("o");
   users37[host]=observation;
   Store.Write("client-users37.json",users37);
   return;
  }
  bool same=old?.Account.Equals(selected.Account,StringComparison.OrdinalIgnoreCase)==true;
  var name=selected.Local?null:userNames37.GetValueOrDefault(selected.Account);
  string display=selected.Local?"":name?.Name.Length>0?name.Name:same?old!.Name:"";
  // An unresolved new account must not be attributed the previous person's name.
  users37[host]=new(){PreviousName37=old?.Name.Length>0?old.Name:old?.PreviousName37??"",PreviousSeen37=old?.Name.Length>0?old.SeenUtc:old?.PreviousSeen37??"",Account=selected.Account,Name=display,Current=true,Local=selected.Local,SeenUtc=now,ReceivedUtc=DateTime.UtcNow.ToString("o")};Store.Write("client-users37.json",users37);
  if(!selected.Local&&(name==null||name.LookupDue(DateTimeOffset.UtcNow)))_=ResolveUser37(selected.Account);
 }
 async Task ResolveUser37(string account,Func<string,string>? lookup=null){
  if(!resolvingUsers37.Add(account))return;
  try{
   string name;
   try{name=await Task.Run(()=>(lookup??DirectoryUserName37)(account));}
   catch(Exception ex){
    if(closing)return;
    var cache=userNames37.GetValueOrDefault(account)??new();
    cache.CheckedUtc=DateTime.UtcNow.ToString("o");
    cache.DirectoryUnavailable=true;
    userNames37[account]=cache;
    Store.Write("user-names37.json",userNames37);
    RefreshDirectoryUser37(account,"");
    var folder=Path.Combine(Store.Root,"Diagnostics");
    Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder,"user-name37.log"),ex.ToString());
    return;
   }
   if(closing)return;
   userNames37[account]=new(){Name=name,CheckedUtc=DateTime.UtcNow.ToString("o")};
   Store.Write("user-names37.json",userNames37);
   RefreshDirectoryUser37(account,name);
   Store.Write("client-users37.json",users37);
  }finally{resolvingUsers37.Remove(account);}
 }
 void RefreshDirectoryUser37(string account,string name){
  foreach(var entry in users37.Where(x=>!x.Value.Local&&x.Value.Account.Equals(account,StringComparison.OrdinalIgnoreCase))){
   if(name.Length>0)entry.Value.Name=name;
   var row=Computers.FirstOrDefault(x=>x.Host.Equals(entry.Key,StringComparison.OrdinalIgnoreCase));
   if(row!=null)RefreshUser37(row);
  }
 }
 static string DirectoryUserName37(string account){
  int separator=account.IndexOf('\\');
  if(separator<=0||separator==account.Length-1)return "";
  string FilterBytes(byte[] bytes)=>string.Concat(bytes.Select(b=>"\\"+b.ToString("X2")));
  string qualifier=FilterBytes(System.Text.Encoding.UTF8.GetBytes(account[..separator]));
  using var root=new DirectoryEntry("LDAP://RootDSE");
  string configuration=(string?)root.Properties["configurationNamingContext"].Value??throw new InvalidOperationException("Каталог доменов недоступен.");
  using var partitions=new DirectoryEntry("LDAP://CN=Partitions,"+configuration);
  // Only use a domain published by the console's forest, never an LDAP host supplied by the client.
  using var findDomain=new DirectorySearcher(partitions){
   Filter="(&(objectClass=crossRef)(systemFlags:1.2.840.113556.1.4.803:=2)(|(nETBIOSName="+qualifier+")(dnsRoot="+qualifier+")))",
   ClientTimeout=TimeSpan.FromSeconds(3),ServerTimeLimit=TimeSpan.FromSeconds(3)
  };
  findDomain.PropertiesToLoad.AddRange(new[]{"dnsRoot","nCName"});
  var domain=findDomain.FindOne();
  if(domain==null||domain.Properties["dnsRoot"].Count==0||domain.Properties["nCName"].Count==0)return "";
  var sid=(SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));
  var bytes=new byte[sid.BinaryLength];sid.GetBinaryForm(bytes,0);
  using var directory=new DirectoryEntry("LDAP://"+domain.Properties["dnsRoot"][0]+"/"+domain.Properties["nCName"][0]);
  using var search=new DirectorySearcher(directory){Filter="(&(objectCategory=person)(objectClass=user)(objectSid="+FilterBytes(bytes)+"))",ClientTimeout=TimeSpan.FromSeconds(3),ServerTimeLimit=TimeSpan.FromSeconds(3)};
  search.PropertiesToLoad.AddRange(new[]{"givenName","sn","displayName"});var result=search.FindOne();if(result==null)return "";string Get(string key)=>result.Properties[key].Count>0?result.Properties[key][0]?.ToString()?.Trim()??"":"";
  string given=Get("givenName"),surname=Get("sn");if(given.Length>0&&surname.Length>0)return given+" "+surname;string display=Get("displayName");return display.Contains(' ')&&!display.Contains('\\')&&!display.Contains('@')?display:"";
 }
}
public sealed partial class RemoteService {
 public const string UserProbeScript37="""
$dcUsers37=@();$dcUsersComplete37=$false;$dcUserError37=''
try{
 if(-not ('DcSessions58' -as [type])){Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;using System.Collections.Generic;
public static class DcSessions58 {
 [StructLayout(LayoutKind.Sequential)]public struct Session {public int Id;public IntPtr Station;public int State;}
 [DllImport("wtsapi32.dll",EntryPoint="WTSEnumerateSessionsW",SetLastError=true)]static extern bool Enumerate(IntPtr server,int reserved,int version,out IntPtr data,out int count);
 [DllImport("wtsapi32.dll",EntryPoint="WTSQuerySessionInformationW",SetLastError=true)]static extern bool Query(IntPtr server,int session,int kind,out IntPtr data,out int bytes);
 [DllImport("wtsapi32.dll")]static extern void WTSFreeMemory(IntPtr data);
 static string Text(int id,int kind){IntPtr data;int count;if(!Query(IntPtr.Zero,id,kind,out data,out count))throw new System.ComponentModel.Win32Exception();try{return Marshal.PtrToStringUni(data)??"";}finally{WTSFreeMemory(data);}}
 public static string[] Read(out string error){
  error="";IntPtr data;int count;
  if(!Enumerate(IntPtr.Zero,0,1,out data,out count))throw new System.ComponentModel.Win32Exception();
  try{
   var users=new List<string>();
   for(int i=0;i<count;i++){
    var session=(Session)Marshal.PtrToStructure(IntPtr.Add(data,i*Marshal.SizeOf(typeof(Session))),typeof(Session));
    if(session.Id==0||(session.State!=0&&session.State!=4))continue;
    try{
     string user=Text(session.Id,5);
     if(user.Length==0)continue;
     string domain=Text(session.Id,7);
     users.Add((session.State==0?"1":"0")+"|"+domain+"\\"+user);
    }catch(System.ComponentModel.Win32Exception ex){if(error.Length==0)error=ex.Message;}
   }
   return users.ToArray();
  }finally{WTSFreeMemory(data);}
 }
}
'@}
 $dcUsers37=@([DcSessions58]::Read([ref]$dcUserError37)|ForEach-Object {
  $parts=$_ -split '\|',2
  $local=$parts[1].StartsWith($env:COMPUTERNAME+'\',[StringComparison]::OrdinalIgnoreCase)
  @{Account=$parts[1];Active=($parts[0] -eq '1');Local=$local}
 })
 $dcUsersComplete37=($dcUserError37.Length -eq 0)
}catch{$dcUserError37=$_.Exception.Message}
""";
}
