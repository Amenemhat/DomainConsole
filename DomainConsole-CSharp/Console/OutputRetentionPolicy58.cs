namespace DomainConsole;
public sealed record OutputCandidate58(string Key,string Host,DateTimeOffset At,long Bytes);
public static class OutputRetention58 {
 public static HashSet<string> Expired(IEnumerable<OutputCandidate58> source,DateTimeOffset now,HistoryLimits58 limits){
  var result=new HashSet<string>();var used=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);long total=0;
  foreach(var item in source.OrderByDescending(x=>x.At).ThenBy(x=>x.Key,StringComparer.Ordinal)){
   if(item.At>now)continue;
   var hostBytes=used.GetValueOrDefault(item.Host);
   if(now-item.At>TimeSpan.FromDays(limits.OutputDays)||hostBytes+item.Bytes>limits.OutputMiB*1024L*1024||total+item.Bytes>limits.TotalOutputMiB*1024L*1024)result.Add(item.Key);
   else{used[item.Host]=hostBytes+item.Bytes;total+=item.Bytes;}
  }
  return result;
 }
}
