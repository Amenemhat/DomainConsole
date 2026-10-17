using System.Text.RegularExpressions;
namespace DomainConsole;
public partial class MainWindow {
 public static string ShortRebootSource(string source)=>source.Contains("Component Based")?"CBS":source.Contains("WindowsUpdate")?"Windows Update":source.Contains("PendingFileRename")?"Ожидающие файловые операции":source;
 public static string CompactDiagnostic(string text){var lines=text.Replace("\r","").Split('\n');var header=lines.Where(l=>l.StartsWith("Снимок")||l.StartsWith("Интервал")).Take(2);var relevant=lines.Where(l=>Regex.IsMatch(l,@"(?i)(0x8[0-9a-f]{7}|\\b(failed|failure|error|corrupt)\\b|ошиб|поврежд)")&&!l.StartsWith("Ошибка: Код завершения")&&!l.Contains("не найдено")&&!l.Contains("не подтверждает")&&!l.Contains("ExitCode")).Distinct().Take(24);return string.Join("\n",header)+"\n"+(relevant.Any()?string.Join("\n",relevant):"Релевантные ошибки в дополнительном снимке не выявлены. Полные данные доступны в скачанном журнале.");}
}
