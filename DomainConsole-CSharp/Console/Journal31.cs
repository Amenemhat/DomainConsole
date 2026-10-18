using System.Text.RegularExpressions;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 static string FailureCard31(PollResult data){
  if(data.State?.Status is not ("Failed" or "CompletedWithErrors" or "InterventionRequired"))return "";
  var errors=data.Updates.Where(u=>u.HResult.HasValue&&u.HResult!=0&&(u.ResultCode is 3 or 4||u.Download is 3 or 4)).GroupBy(u=>u.Title+"|"+u.HResult).Select(g=>g.First()).Take(6).ToList();
  var blocks=new List<string>();foreach(var u in errors){var hr=u.HResult!.Value;var card=ErrorReference31.Find(hr);var lines=new List<string>{u.Title+" · "+(u.Download.HasValue?"скачивание":"установка"),ExecutionDiagnosis.Code(hr)+" · "+ExecutionDiagnosis.Explain(hr)};if(card!=null&&card.Causes.Length>0)lines.Add("Возможные причины: "+card.Causes);lines.Add("Первое действие: "+ExecutionDiagnosis.Recommend(hr));var evidence=Regex.Split(data.Output??"",@"\r?\n").FirstOrDefault(l=>l.Contains("CBS")&&l.Contains("0x"+unchecked((uint)hr).ToString("x8"),StringComparison.OrdinalIgnoreCase)&&l.Contains("Failed",StringComparison.OrdinalIgnoreCase));if(evidence!=null)lines.Add("Свидетельство CBS (требует сопоставления времени и пакета): "+(evidence.Length>650?evidence[..650]+"…":evidence));blocks.Add(string.Join("\n",lines));}
  if(blocks.Count==0){var message=data.State.Error;var m=Regex.Match(message,@"(?i)0x([89a-f][0-9a-f]{7})");if(m.Success){var hr=unchecked((int)Convert.ToUInt32(m.Groups[1].Value,16));blocks.Add(message+"\n"+ExecutionDiagnosis.Code(hr)+" · "+ExecutionDiagnosis.Explain(hr)+"\nПервое действие: "+ExecutionDiagnosis.Recommend(hr));}else if(message.Length>0)blocks.Add(message);}
  return blocks.Count>0?"## Причина сбоя и следующее действие\n"+string.Join("\n\n",blocks):"";
 }
 public static string CompactJournal31(string text){
  if(!text.Contains("## "))return text;var sections=LogViewer.Sections(text);var output=new List<string>();var signatures=new HashSet<string>(StringComparer.Ordinal);var explained=new HashSet<string>();
  foreach(var section in sections){var title=section.Title;var body=section.Body;
   if(title.StartsWith("Расшифровка кодов"))continue;
   if(title.StartsWith("Контроль выполнения")&&sections.Any(s=>s.Title.StartsWith("Причина сбоя")))continue;
   if(title.StartsWith("Диагностика статуса")&&body.Contains("прежнее ограничение снято"))continue;
   var unique=new List<string>();foreach(var originalLine in body.Split('\n')){var line=originalLine;var codeMatch=Regex.Match(line,@"(?i)0x([89a-f][0-9a-f]{7})");if(codeMatch.Success){var codeKey=codeMatch.Groups[1].Value.ToUpperInvariant();var card=ErrorReference31.Find(unchecked((int)Convert.ToUInt32(codeKey,16)));if(card!=null&&card.Symbol.Length>0&&line.Contains(" · "+card.Symbol)){if(!explained.Add(codeKey)){int marker=line.IndexOf(" · "+card.Symbol,StringComparison.Ordinal);line=line[..marker];}}}var key=line.Trim();if(key.Length==0){if(unique.Count>0&&unique[^1].Length>0)unique.Add("");continue;}if(key=="Команда завершилась с ненулевым кодом. Подробности смотрите в выводе и журналах; код зависит от запущенной программы.")continue;
    if(key.Length>70&&!signatures.Add(key))continue;
    unique.Add(line);
   }
   if(unique.Count==0)continue;var normalized=string.Join("\n",unique).Trim();if(normalized.Length>0)output.Add("## "+title+"\n"+normalized);
  }
  return string.Join("\n\n",output);
 }
}
