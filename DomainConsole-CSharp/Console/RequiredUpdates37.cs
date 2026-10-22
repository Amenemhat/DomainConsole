namespace DomainConsole;
public partial class MainWindow {
 void Needed37_Click(object sender,System.Windows.RoutedEventArgs e){if(sender is not System.Windows.FrameworkElement {DataContext:ComputerRow row})return;ComputerGrid.SelectedItem=row;MainTabs.SelectedItem=WsusTab;WsusJournalRow37.Height=new(1,System.Windows.GridUnitType.Star);WsusReportRow37.Height=new(2,System.Windows.GridUnitType.Star);ShowCurrent();if(reportCache.TryGetValue(row.Host,out var cache)&&cache.Found&&CacheMatches(cache)){WsusServerOutput.Text=cache.Report.Format(cache.Server,cache.Started);WsusServerOutput.SetExpanded("Необходимые обновления · "+cache.Report.NeededCount,true);}e.Handled=true;}

 void RefreshNeeded37(ComputerRow row){
  if(!reportCache.TryGetValue(row.Host,out var cached)||!cached.Found||!CacheMatches(cached)){row.SetNeeded37(null,false,"");return;}
  var report=cached.Report;row.SetNeeded37(report.NeededCount,report.RequiredComplete,"К установке: "+(report.RequiredComplete?"":"≥ ")+report.NeededCount+" — одобрены и применимы\nПо последнему отчёту: "+Display36.Date(report.LastReportedUtc,true)+" · "+ReportPresentation35.Age(report.LastReportedUtc));
 }
}
