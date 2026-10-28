using System.Text.RegularExpressions;
namespace DomainConsole;
public static class ResultPresentation36 {
 public static string Short(string value,int max=180)=>value.Length>max?value[..max]+"…":value;
 public static string BriefFailure(string body){if(body.Contains("0xFFFF0000",StringComparison.OrdinalIgnoreCase))return "0xFFFF0000 · выполнение команды не подтверждено; подробности в журнале";var lines=body.Split('\n',StringSplitOptions.RemoveEmptyEntries);var code=lines.FirstOrDefault(x=>Regex.IsMatch(x,@"(?i)0x[89a-f][0-9a-f]{7}"))??lines.FirstOrDefault(x=>x.StartsWith("Ошибка:")&&!x.Contains("см. карточку"))??lines.FirstOrDefault(x=>!x.StartsWith("Возможные")&&!x.StartsWith("Первое действие")&&!x.StartsWith("Компьютер:")&&!x.StartsWith("Состояние:")&&!x.StartsWith("Начало:")&&!x.StartsWith("Завершение:"))??"Причина не получена";code=Regex.Replace(code,@"(?i)^.*?(0x[89a-f][0-9a-f]{7})\)?\s*·?\s*(?:[A-Z][A-Z0-9_]+:\s*)?","$1 · ");return Short(code);}
 public static string Format(string text){
  if(!text.Contains("## Итог задания")&&!text.Contains("## Сведения о задании"))return text;
  var sections=LogViewer.Sections(text);if(sections.Any(s=>s.Title=="Главное"))return text;
  var info=sections.FirstOrDefault(s=>s.Title=="Итог задания");if(info.Body==null)return text;
  var failure=sections.FirstOrDefault(s=>s.Title.StartsWith("Причина сбоя"));var results=sections.FirstOrDefault(s=>s.Title=="Результаты команд");var reboot=sections.FirstOrDefault(s=>s.Title.StartsWith("Проверка перезагрузки"));var output=sections.FirstOrDefault(s=>s.Title=="Вывод команды");
  bool failed=Regex.IsMatch(info.Body,@"Состояние: (Ошибка|Завершено с ошибками|Нужно вмешательство)");bool needs=reboot.Body!=null&&(reboot.Body.Contains("Перезагрузка подтверждена")||reboot.Body.Contains("Нужна перезагрузка."));
  string status=info.Body.Split('\n').FirstOrDefault(x=>x.StartsWith("Состояние:"))?.Replace("Состояние: ","")??"Результат не получен";
  string command=results.Body?.Split('\n')[0]??"";command=Regex.Replace(command,@": (успешно|ошибка|остановлено).*","");
  string summary=needs?"Нужна перезагрузка":failed?"Ошибка · "+BriefFailure(failure.Body??info.Body):status+(command.Length>0?" · "+Short(command,100):"");
  if(status.StartsWith("Перезагрузка подтверждена;"))summary="Перезагрузка Windows подтверждена";var main=new List<string>{summary};if(status.StartsWith("Перезагрузка подтверждена;"))main.Add("Флаги перезагрузки не проверены. Повторите проверку клиента; ограничение и время снимка — ниже.");if(needs&&failed)main.Add("Неудачная попытка сохранена в подробностях.");
  if(command.Contains("служб",StringComparison.OrdinalIgnoreCase)&&!failed){var checkedServices=(output.Body??"").Split('\n').Where(x=>Regex.IsMatch(x,@"(?i)^(UsoSvc|wuauserv|bits|cryptsvc):.*работает")).Distinct().ToList();if(checkedServices.Count>0)main.AddRange(checkedServices);else main.Add("Работа служб после команды не подтверждена этим выводом.");main.Add("Далее: проверить, возобновилась ли загрузка обновлений.");}
  if(reboot.Body!=null&&!needs)main.Add(reboot.Body.Contains("Флаги перезагрузки Windows Update / CBS отсутствуют")?"По проверке после операции флаги перезагрузки Windows Update / CBS отсутствуют.":"Проверка перезагрузки: "+Short(reboot.Body.Split('\n')[0]));
  if(failed&&results.Body!=null){int successful=results.Body.Split('\n').Count(x=>x.Contains(": успешно"));int errors=results.Body.Split('\n').Count(x=>x.Contains(": ошибка"));if(successful>0)main.Add("Успешных действий: "+successful+"; с ошибкой: "+errors+". Выполнение частичное.");}
  var next=failure.Body?.Split('\n').FirstOrDefault(x=>x.StartsWith("Первое действие:"));if(failed&&!needs&&next!=null)main.Add(next.Replace("Первое действие:","Далее:"));
  var final=new List<string>{"## Главное\n"+string.Join("\n",main)};
  foreach(var s in sections){if(s.Title=="Итог задания")continue;string title=s.Title;if(title=="Обслуживание клиента"&&!failed)title="Предыдущее обслуживание · состояние отдельного плана";if(title=="Этапы выполнения")title="Технические подробности · этапы выполнения";if(title=="Результаты команд")title="Технические подробности · результаты команд";string body=Regex.Replace(s.Body,@"(?m)^Ошибка: см\. карточку причины ниже\s*$","");if(title=="Вывод команды"&&command.Contains("служб",StringComparison.OrdinalIgnoreCase)){body=string.Join("\n",body.Split('\n').Where(line=>!main.Contains(line)&&!line.Contains("Возобновление загрузки необходимо проверить отдельно")));title="Технические подробности · вывод операции";}
   if(body.Trim().Length>0)final.Add("## "+title+"\n"+body);}
  final.Add("## Сведения о задании\n"+Regex.Replace(info.Body,@"(?m)^Ошибка: см\. карточку причины ниже\s*$",failed?"Причина: "+BriefFailure(failure.Body??info.Body):""));return string.Join("\n\n",final);
 }
}
