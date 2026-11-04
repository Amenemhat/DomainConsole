namespace DomainConsole;
public sealed class ClientIdentity58 {
 public string Host {get;set;}="";
 public string Name {get;set;}="";
 public string Machine {get;set;}="";
 public string Server {get;set;}="";
 public string SusClientId {get;set;}="";
 public string At {get;set;}="";
 public string Error {get;set;}="";
 public string ErrorAt {get;set;}="";
 public string Key=>Server.TrimEnd('/').ToLowerInvariant()+"|"+SusClientId.ToLowerInvariant();
 public bool Fresh(DateTimeOffset now)=>Error.Length==0&&DateTimeOffset.TryParse(At,out var at)&&at<=now&&now-at<TimeSpan.FromMinutes(10);
}
public static class IdentityPolicy58 {
 public static List<ClientIdentity58> Participants(IEnumerable<ClientIdentity58> records,ClientIdentity58 selected,DateTimeOffset now)=>records.Where(r=>r.Key==selected.Key&&r.Fresh(now)&&r.Error.Length==0&&Guid.TryParse(r.SusClientId,out _)&&Guid.TryParse(r.Machine,out var machine)&&machine!=Guid.Empty&&r.Server.Length>0&&r.Name.Length>0).DistinctBy(r=>r.Machine,StringComparer.OrdinalIgnoreCase).ToList();
 public static bool Confirmed(IEnumerable<ClientIdentity58> records,ClientIdentity58 selected,DateTimeOffset now){var list=Participants(records,selected,now);return list.Count>1&&list.Select(r=>r.Name.TrimEnd('.')).Distinct(StringComparer.OrdinalIgnoreCase).Count()==list.Count;}
}
