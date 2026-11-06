using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DomainConsole;
public sealed class HistoryLimits58 {
 public int OrdinaryDays {get;set;}=30;
 public int OrdinaryCount {get;set;}=2000;
 public int ImportantDays {get;set;}=180;
 public int ImportantCount {get;set;}=5000;
 public int OutputDays {get;set;}=14;
 public int OutputMiB {get;set;}=20;
 public int TotalOutputMiB {get;set;}=2048;
 public void Validate(){if(OrdinaryDays<1||ImportantDays<OrdinaryDays||ImportantDays>3650||OrdinaryCount<1||ImportantCount<1||OrdinaryCount>100000||ImportantCount>100000||OutputDays<1||OutputMiB<1||OutputMiB>1024||TotalOutputMiB<OutputMiB||TotalOutputMiB>65536)throw new ArgumentException("Некорректные ограничения истории");}
}
public static class HistoryRetention58 {
 sealed record Entry(string File,string Raw,JsonObject? Data,DateTimeOffset? At,bool Important,bool Protected);
 public static bool Important(string operation,string kind)=>kind is "Error" or "Warning"||operation.StartsWith("update-")||operation.StartsWith("job-result")||operation.StartsWith("identity")||operation.StartsWith("boot")||operation.StartsWith("reboot")||operation.StartsWith("maintenance")||operation.StartsWith("manual")||operation.StartsWith("job|");
 // Called only in the serialized writer for this client, never from the UI thread.
 // Unknown records survive; cleanup must not silently discard unreadable evidence.
 public static int Compact(string folder,HistoryLimits58 limits,DateTimeOffset now,ISet<string> protectedScopes,bool protectIdentity){
  limits.Validate();if(!Directory.Exists(folder))return 0;
  var entries=new List<Entry>();
  foreach(var file in Directory.GetFiles(folder,"*.jsonl"))foreach(var raw in File.ReadLines(file)){
   try{
    var data=JsonNode.Parse(raw) as JsonObject;string op=data?["Operation36"]?.GetValue<string>()??"";
    DateTimeOffset? at=DateTimeOffset.TryParse(data?["At"]?.GetValue<string>(),out var parsed)?parsed:null;
    bool protect=at==null||at>now||op.Split('|').Any(protectedScopes.Contains)||protectIdentity&&op.StartsWith("identity");
    entries.Add(new(file,raw,data,at,Important(op,data?["Kind"]?.GetValue<string>()??""),protect));
   }catch(Exception ex) when(ex is JsonException or InvalidOperationException or FormatException){entries.Add(new(file,raw,null,null,true,true));}
  }
  int ordinary=0,important=0;long outputBytes=0;var kept=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);int removed=0;
  foreach(var entry in entries.OrderByDescending(x=>x.At??DateTimeOffset.MaxValue).ThenBy(x=>x.Raw,StringComparer.Ordinal)){
   bool keep=entry.Protected;
   if(!keep){int rank=entry.Important?++important:++ordinary;keep=rank<=(entry.Important?limits.ImportantCount:limits.OrdinaryCount)&&now-entry.At!.Value<=TimeSpan.FromDays(entry.Important?limits.ImportantDays:limits.OrdinaryDays);}
   if(!keep){removed++;continue;}
   string raw=entry.Raw;
   if(!entry.Protected&&entry.Data is {} data){
    string details=data["Details"]?.GetValue<string>()??"";
    long size=Encoding.UTF8.GetByteCount(details);
    // Retain event facts/codes. Only verbose command/diagnostic output is subject to output retention.
    string op=data["Operation36"]?.GetValue<string>()??"";
    if(op.StartsWith("command-output")||op.StartsWith("diagnostic-output")){
     if(now-entry.At!.Value>TimeSpan.FromDays(limits.OutputDays)||outputBytes+size>limits.OutputMiB*1024L*1024){data["Details"]="Подробный вывод удалён по сроку/лимиту хранения; результат события сохранён.";raw=data.ToJsonString();}else outputBytes+=size;
    }
   }
   if(!kept.TryGetValue(entry.File,out var lines))kept[entry.File]=lines=new();lines.Add(raw);
  }
  foreach(var group in entries.GroupBy(x=>x.File)){
   var lines=kept.GetValueOrDefault(group.Key)??new();lines.Reverse();
   if(lines.Count==0){File.Delete(group.Key);continue;}
   if(group.Select(x=>x.Raw).SequenceEqual(lines))continue;
   DataBackup57.DurableReplace(group.Key,Encoding.UTF8.GetBytes(string.Join(Environment.NewLine,lines)+Environment.NewLine));
  }
  return removed;
 }
}
