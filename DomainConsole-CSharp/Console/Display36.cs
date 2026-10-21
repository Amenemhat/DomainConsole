using System.Globalization;
using System.Text.RegularExpressions;
namespace DomainConsole;
public static class Display36 {
 public static string Date(string value,bool full=false)=>DateTimeOffset.TryParse(value,out var at)&&at.Year>=2000?Date(at.ToLocalTime().DateTime,full):"нет данных";
 public static string Date(DateTime at,bool full=false)=>full?at.ToString("dd.MM.yyyy HH:mm:ss"):at.Date==DateTime.Now.Date?"сегодня, "+at.ToString("HH:mm"):at.Year==DateTime.Now.Year?at.ToString("dd.MM, HH:mm"):at.ToString("dd.MM.yyyy, HH:mm");
 public static string Text(string text)=>Regex.Replace(Regex.Replace(text,@"(?<![\w\\/])\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?(?: ?(?:Z|[+-]\d{2}:\d{2}))?",m=>DateTimeOffset.TryParse(m.Value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)?Date(at.LocalDateTime):m.Value),@"(?<![\w\\/])\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}(?: [+-]\d{2}:\d{2})?",m=>DateTimeOffset.TryParseExact(m.Value,new[]{"dd.MM.yyyy HH:mm:ss zzz","dd.MM.yyyy HH:mm:ss"},CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)?Date(at.LocalDateTime):m.Value);
 public static bool TryDate(string text,out DateTimeOffset at){if(text.StartsWith("сегодня, "))text=DateTime.Today.ToString("dd.MM.yyyy")+" "+text[9..];else if(Regex.IsMatch(text,@"^\d{2}\.\d{2},"))text=text[..5]+"."+DateTime.Now.Year+text[6..];else text=text.Replace(",","");return DateTimeOffset.TryParseExact(text,new[]{"dd.MM.yyyy HH:mm:ss","dd.MM.yyyy HH:mm"},CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out at);}
}
