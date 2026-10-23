using System.Globalization;
namespace DomainConsole;
// WSUS IComputerTarget.LastReportedStatusTime / LastSyncTime are distinct client timestamps.
public static class ReportPresentation35 {
 internal static int? RefreshHours40;
 public static int FreshHours=>RefreshHours40??Math.Clamp(Store.Read("report-age-hours.json",24),1,720);
 public static string Date(string value)=>Display36.Date(value,true);
 public static string Tone(string value,DateTimeOffset now,int hours){if(!DateTimeOffset.TryParse(value,out var at)||at.Year<2000)return "Unknown";var age=now-at;if(age<TimeSpan.FromMinutes(-5))return "Warning";return age<=TimeSpan.FromHours(hours)?"Success":"Stale";}
 public static string Age(string value)=>AgeAt41(value,DateTimeOffset.UtcNow);
 public static string AgeAt41(string value,DateTimeOffset now){if(!DateTimeOffset.TryParse(value,out var at)||at.Year<2000)return "время неизвестно";var elapsed=now-at;if(elapsed<TimeSpan.FromMinutes(-5))return "время клиента впереди; проверьте часы";if(elapsed<TimeSpan.FromMinutes(1))return "только что";if(elapsed.TotalDays>=1)return (int)elapsed.TotalDays+" дн "+elapsed.Hours+" ч назад";if(elapsed.TotalHours>=1)return (int)elapsed.TotalHours+" ч "+elapsed.Minutes+" мин назад";return (int)elapsed.TotalMinutes+" мин назад";}
 public static string ReportLineTone(string line){var value=line["Последний отчёт клиента: ".Length..].Split(" · ")[0];return Display36.TryDate(value,out var at)?Tone(at.ToString("o"),DateTimeOffset.UtcNow,FreshHours):"Unknown";}
 public static bool Emphasized(string line)=>line.StartsWith("К установке:")||line.StartsWith("Последний отчёт клиента:");
}
