namespace DomainConsole;
public partial class MainWindow {
 void RefreshNeeded37(ComputerRow row){
  if(!reportCache.TryGetValue(row.Host,out var cached)||!cached.Found||!CacheMatches(cached)){row.SetNeeded37(null,false,"");return;}
  var report=cached.Report;row.SetNeeded37(report.NeededCount,report.RequiredComplete,"К установке: "+(report.RequiredComplete?"":"≥ ")+report.NeededCount+" — одобрены и применимы\nПо последнему отчёту: "+Display36.Date(report.LastReportedUtc,true)+" · "+ReportPresentation35.Age(report.LastReportedUtc));
 }
}
