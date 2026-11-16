namespace DomainConsole;
public sealed class ClientIdentity58 {
 public string Host {get;set;}="";
 public string Name {get;set;}="";
 public string Machine {get;set;}="";
 public string Server {get;set;}="";
 public string SusClientId {get;set;}="";
 public string StatusServer {get;set;}="";
 public int? UseWUServer {get;set;}
 public int? TargetGroupEnabled {get;set;}
 public string TargetGroup {get;set;}="";
 public int? ValidationBytes {get;set;}

 public string At {get;set;}="";
 public string Error {get;set;}="";
 public string ErrorAt {get;set;}="";
 public string Key=>Server.TrimEnd('/').ToLowerInvariant()+"|"+SusClientId.ToLowerInvariant();
 public bool Fresh(DateTimeOffset now)=>Error.Length==0&&DateTimeOffset.TryParse(At,out var at)&&at<=now&&now-at<TimeSpan.FromMinutes(10);
}
public static class IdentityPolicy58 {
 public static bool Usable(ClientIdentity58 r)=>r.Error.Length==0&&Guid.TryParse(r.SusClientId,out var id)&&id!=Guid.Empty&&Guid.TryParse(r.Machine,out var hardware)&&hardware!=Guid.Empty&&hardware!=Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")&&Uri.TryCreate(r.Server,UriKind.Absolute,out var uri)&&(uri.Scheme=="http"||uri.Scheme=="https")&&!string.IsNullOrWhiteSpace(r.Name);
 public static List<ClientIdentity58> Participants(IEnumerable<ClientIdentity58> records,ClientIdentity58 selected,DateTimeOffset now)=>records.Where(r=>r.Key==selected.Key&&r.Fresh(now)&&Usable(r)).DistinctBy(r=>r.Machine,StringComparer.OrdinalIgnoreCase).ToList();
 public static bool RegistrationConfirmed(ClientIdentity58 identity,string oldId,string started,string serverId,string serverName,string reportTime,DateTimeOffset now)=>
  identity.Fresh(now)&&Guid.TryParse(identity.SusClientId,out var id)&&id!=Guid.Empty&&!identity.SusClientId.Equals(oldId,StringComparison.OrdinalIgnoreCase)&&
  identity.SusClientId.Equals(serverId,StringComparison.OrdinalIgnoreCase)&&identity.Name.TrimEnd('.').Equals(serverName.TrimEnd('.'),StringComparison.OrdinalIgnoreCase)&&
  DateTimeOffset.TryParse(started,out var began)&&DateTimeOffset.TryParse(reportTime,out var reported)&&reported>=began&&reported<=now.AddMinutes(1);
 public static bool Confirmed(IEnumerable<ClientIdentity58> records,ClientIdentity58 selected,DateTimeOffset now){var list=Participants(records,selected,now);return list.Count>1&&list.Select(r=>r.Name.TrimEnd('.')).Distinct(StringComparer.OrdinalIgnoreCase).Count()==list.Count;}
}
