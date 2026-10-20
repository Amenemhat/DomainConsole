using System.Text.RegularExpressions;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 static bool Failed35(string status)=>status is "Failed" or "CompletedWithErrors" or "InterventionRequired";
 static string JobEvent35(PollResult data){var state=data.State!;if(!Failed35(state.Status))return ComputerRow.StatusText(state.Status)+" · "+state.Stage;var bad=data.Updates.FirstOrDefault(u=>u.Download is 3 or 4||u.ResultCode is 3 or 4);if(bad!=null){var kb=Regex.Match(bad.Title,@"(?i)KB\s*\d+").Value;return "Ошибка "+(bad.Download is 3 or 4?"скачивания":"установки")+(kb.Length>0?" · "+kb:"");}var result=state.Results.LastOrDefault(x=>x.ExitCode!=0);return ComputerRow.StatusText(state.Status)+(result!=null?" · "+result.Name:"");}
 static string TerminalText35(ComputerRow row,RemoteState? state){
  if(state==null)return row.Status;
  if(Failed35(row.Target.Status)){
   var card=LogViewer.Sections(row.Target.Output).FirstOrDefault(x=>x.Title.StartsWith("Причина сбоя"));var lines=(card.Body??"").Split('\n',StringSplitOptions.RemoveEmptyEntries).Where(x=>!x.StartsWith("Возможные причины:")&&!x.StartsWith("Сначала перезагрузите")&&!x.StartsWith("Сообщение операции:")).Take(3).ToList();
   if(lines.Count>0)return "Ошибка · "+string.Join("\n",lines.Select(x=>x.Length>220?x[..220]+"…":x));
   var code=Regex.Match(state.Error,@"(?i)0x[89a-f][0-9a-f]{7}");var result=state.Results.LastOrDefault(x=>x.ExitCode!=0);var cause=code.Success?code.Value+" · "+ExecutionDiagnosis.Explain(unchecked((int)Convert.ToUInt32(code.Value[2..],16))):ShortConnectionError(state.Error);
   return ComputerRow.StatusText(row.Target.Status)+(result!=null?" · "+result.Name:"")+(cause.Length>0?"\n"+(cause.Length>260?cause[..260]+"…":cause):"");
  }
  string name=state.Results.LastOrDefault()?.Name??"";return ComputerRow.StatusText(row.Target.Status)+(name.Length>0?" · "+name:"")+(DateTimeOffset.TryParse(state.Ended,out var at)?" · "+at.ToLocalTime().ToString("dd.MM HH:mm"):"");
 }
}
