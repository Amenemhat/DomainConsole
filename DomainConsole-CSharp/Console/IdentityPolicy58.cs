using System.Globalization;

namespace DomainConsole;
public sealed class ClientIdentity58 {
 public string Address {get;set;}="";
 public string Activity {get;set;}="";
 public string Host {get;set;}="";
 public string Name {get;set;}="";
 public string Machine {get;set;}="";
 public string InstallationId {get;set;}="";
 public string DirectoryId {get;set;}="";
 public string AdapterMac {get;set;}="";
 public string Server {get;set;}="";
 public string SourceCheckedAt {get;set;}="";
 public string VerifiedSource {get;set;}="";
 public string ServerId {get;set;}="";
 public string Source=>VerifiedSource.Length>0&&Guid.TryParse(ServerId,out var id)&&id!=Guid.Empty?VerifiedSource:Server;
 public string SusClientId {get;set;}="";
 public string StatusServer {get;set;}="";
 public int? UseWUServer {get;set;}
 public int? TargetGroupEnabled {get;set;}
 public string TargetGroup {get;set;}="";
 public int? ValidationBytes {get;set;}

 public string At {get;set;}="";
 public string Error {get;set;}="";
 public string ErrorAt {get;set;}="";
 public string Key=>(Uri.TryCreate(Source,UriKind.Absolute,out var uri)?uri.AbsoluteUri:Source).TrimEnd('/').ToLowerInvariant()+"|"+(Guid.TryParse(SusClientId,out var id)?id.ToString("D"):SusClientId.ToLowerInvariant());
 public bool Fresh(DateTimeOffset now)=>Error.Length==0&&DateTimeOffset.TryParse(At,out var at)&&at<=now&&now-at<TimeSpan.FromMinutes(10);
}
public static class IdentityPolicy58 {
 public static Guid Installation(ClientIdentity58 item)=>Guid.TryParse(item.InstallationId,out var id)?id:Guid.Empty;
 public static Guid DirectoryObject(ClientIdentity58 item)=>Guid.TryParse(item.DirectoryId,out var id)?id:Guid.Empty;
 public static bool ValidAdapterMac(string value)=>value.Length==12&&ulong.TryParse(value,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out var mac)&&mac>0&&(mac&0x010000000000UL)==0;
 public static bool HasAdapterEvidence(ClientIdentity58 item)=>DirectoryObject(item)!=Guid.Empty&&ValidAdapterMac(item.AdapterMac);
 public static bool Usable(ClientIdentity58 r)=>r.UseWUServer==1&&r.Error.Length==0&&Guid.TryParse(r.SusClientId,out var id)&&id!=Guid.Empty&&Guid.TryParse(r.Machine,out var hardware)&&hardware!=Guid.Empty&&hardware!=Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")&&Uri.TryCreate(r.Server,UriKind.Absolute,out var uri)&&(uri.Scheme=="http"||uri.Scheme=="https")&&!string.IsNullOrWhiteSpace(r.Name);
 public static bool SameApprovedDevice(ClientIdentity58 approved,ClientIdentity58 current,DateTimeOffset now)=>
  current.Fresh(now)&&Usable(current)&&current.Key==approved.Key&&
  current.Name.TrimEnd('.').Equals(approved.Name.TrimEnd('.'),StringComparison.OrdinalIgnoreCase)&&
  Guid.TryParse(approved.Machine,out var hardware)&&hardware==Guid.Parse(current.Machine)&&
  (Installation(approved)==Guid.Empty||Installation(current)==Installation(approved))&&
  (!HasAdapterEvidence(approved)||HasAdapterEvidence(current)&&DirectoryObject(current)==DirectoryObject(approved)&&current.AdapterMac.Equals(approved.AdapterMac,StringComparison.OrdinalIgnoreCase))&&
  current.Server.TrimEnd('/').Equals(approved.Server.TrimEnd('/'),StringComparison.OrdinalIgnoreCase)&&
  (approved.StatusServer.Length==0||current.StatusServer.TrimEnd('/').Equals(approved.StatusServer.TrimEnd('/'),StringComparison.OrdinalIgnoreCase));
 public static List<ClientIdentity58> Participants(IEnumerable<ClientIdentity58> records,ClientIdentity58 selected,DateTimeOffset now,bool manual=false){
  var participants=new List<ClientIdentity58>();
  foreach(var hardware in records.Where(r=>r.Key==selected.Key&&r.Fresh(now)&&Usable(r)).GroupBy(r=>Guid.Parse(r.Machine))){
   var devices=hardware.GroupBy(r=>r.Name.TrimEnd('.'),StringComparer.OrdinalIgnoreCase)
    .Select(g=>g.OrderByDescending(r=>r.At,StringComparer.Ordinal).First()).ToList();
   if(devices.Count>1&&(devices.Any(r=>Installation(r)==Guid.Empty)||devices.Select(Installation).Distinct().Count()!=devices.Count)){
    if(!manual||devices.Any(r=>!HasAdapterEvidence(r))||devices.Select(DirectoryObject).Distinct().Count()!=devices.Count||
       devices.Select(r=>r.AdapterMac).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=devices.Count)continue;
   }
   participants.AddRange(devices);
  }
  return participants;
 }
 public static bool IndependentNewId(IEnumerable<ClientIdentity58> known,ClientIdentity58 item,DateTimeOffset now)=>
  !known.Any(other=>other.Key==item.Key&&other.Fresh(now)&&Usable(other)&&(!other.Machine.Equals(item.Machine,StringComparison.OrdinalIgnoreCase)||!other.Name.TrimEnd('.').Equals(item.Name.TrimEnd('.'),StringComparison.OrdinalIgnoreCase)));
 public static bool RegistrationConfirmed(ClientIdentity58 identity,string oldId,string started,string serverId,string serverName,string reportTime,DateTimeOffset now)=>
  identity.Fresh(now)&&Usable(identity)&&!identity.SusClientId.Equals(oldId,StringComparison.OrdinalIgnoreCase)&&
  identity.SusClientId.Equals(serverId,StringComparison.OrdinalIgnoreCase)&&identity.Name.TrimEnd('.').Equals(serverName.TrimEnd('.'),StringComparison.OrdinalIgnoreCase)&&
  DateTimeOffset.TryParse(started,out var began)&&DateTimeOffset.TryParse(reportTime,out var reported)&&reported>=began&&reported<=now.AddMinutes(1);
 public static bool Confirmed(IEnumerable<ClientIdentity58> records,ClientIdentity58 selected,DateTimeOffset now,bool manual=false){var list=Participants(records,selected,now,manual);return list.Count>1&&list.Select(r=>r.Name.TrimEnd('.')).Distinct(StringComparer.OrdinalIgnoreCase).Count()==list.Count;}
}
