using System.Text.RegularExpressions;
namespace DomainConsole;
public static class RebootSnapshotPolicy {
 public static bool NewBoot(RebootSnapshot? old,RebootSnapshot next)=>old!=null&&DateTimeOffset.TryParse(old.BootUtc,out var before)&&DateTimeOffset.TryParse(next.BootUtc,out var after)&&after>before;
 public static bool SupersedesPositive(RebootSnapshot? old,RebootSnapshot next){var stamp=old?.Required==true?old.CollectedUtc:old?.PreservedRequired==true?old.PreservedUtc:"";return DateTimeOffset.TryParse(stamp,out var at)&&DateTimeOffset.TryParse(next.BootUtc,out var boot)&&boot>at;}
 public static bool SameBoot(RebootSnapshot? old,RebootSnapshot next)=>old!=null&&DateTimeOffset.TryParse(old.BootUtc,out var before)&&DateTimeOffset.TryParse(next.BootUtc,out var after)&&after==before;
 public static bool EvidenceAfterBoot(string evidence,string boot)=>DateTimeOffset.TryParse(evidence,out var at)&&DateTimeOffset.TryParse(boot,out var start)&&at>=start;
 public static void Merge(RebootSnapshot? old,RebootSnapshot next){
  next.PreservedRequired=null;next.PreservedUtc="";
  if(next.Required==null&&SameBoot(old,next)&&!(old!.Sources.Count>0&&old.Sources.All(x=>x.StartsWith("PendingFileRenameOperations")))){
   var stamp=old!.Required.HasValue?old.CollectedUtc:old.PreservedUtc;
   if(EvidenceAfterBoot(stamp,next.BootUtc)){next.PreservedRequired=old.Required??old.PreservedRequired;next.PreservedUtc=stamp;}
  }
 }
}
public sealed class StatusDiagnostic {public string SinceUtc {get;set;}="";public string LastUtc {get;set;}="";public string Error {get;set;}="";public bool Resolved {get;set;}}
public partial class MainWindow {
 readonly Dictionary<string,StatusDiagnostic> statusDiagnostics=new(Store.Read("status-diagnostics.json",new Dictionary<string,StatusDiagnostic>()),StringComparer.OrdinalIgnoreCase);
 void RecordStatusDiagnostic(string host,RebootSnapshot snapshot){
  if(snapshot.Error.Length>0){Event31(host,"diagnostic",snapshot.Error,StatusErrorSummary(snapshot.Error),"Warning",snapshot.Error);if(!statusDiagnostics.TryGetValue(host,out var entry)||entry.Error!=snapshot.Error||entry.Resolved){entry=new(){SinceUtc=snapshot.CollectedUtc,Error=snapshot.Error};statusDiagnostics[host]=entry;}entry.LastUtc=snapshot.CollectedUtc;entry.Resolved=false;}
  else if(statusDiagnostics.TryGetValue(host,out var entry)&&!entry.Resolved){entry.Resolved=true;entry.LastUtc=snapshot.CollectedUtc;Event31(host,"diagnostic","resolved","Проверка статуса восстановлена","Success");}
  Store.Write("status-diagnostics.json",statusDiagnostics);
 }
 public static string StatusErrorSummary(string error){if(error.Contains("80070005",StringComparison.OrdinalIgnoreCase)||error.Contains("отказано в доступе",StringComparison.OrdinalIgnoreCase))return "Не удалось полностью проверить необходимость перезагрузки: 0x80070005 — отказано в доступе.";var code=Regex.Match(error,@"(?i)(?:0x)?8[0-9a-f]{7}").Value;return "Проверка необходимости перезагрузки неполная"+(code.Length>0?" · "+code:"")+". Подробности в техническом разделе.";}
 string StatusDiagnosticText(string host){if(!statusDiagnostics.TryGetValue(host,out var d)||d.Resolved)return "";return "\n\n## Диагностика статуса клиента\n"+(d.Resolved?"Повторная проверка выполнена успешно; прежнее ограничение снято.":StatusErrorSummary(d.Error)+" Связь WinRM установлена; это не ошибка подключения.")+"\n"+LocalDateConverter.Format(d.Resolved?d.LastUtc:d.SinceUtc)+(d.Resolved?"":"\n\n## Технический журнал · проверка статуса\nПервое наблюдение: "+LocalDateConverter.Format(d.SinceUtc)+"\nПоследнее наблюдение: "+LocalDateConverter.Format(d.LastUtc)+"\n"+d.Error);}
}

