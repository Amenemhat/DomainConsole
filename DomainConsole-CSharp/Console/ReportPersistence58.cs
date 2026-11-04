namespace DomainConsole;
public partial class MainWindow {
 readonly LatestWrite58 reportWrites58=new();
 void PersistReportCache58(){var ticket=reportWrites58.Reserve();reportWrites58.Commit(ticket,()=>Store.Write("wsus-report-cache.json",reportCache));}
 Task PersistReportCacheAsync58(){var snapshot=Store.Clone(reportCache);var ticket=reportWrites58.Reserve();return Task.Run(()=>reportWrites58.Commit(ticket,()=>Store.Write("wsus-report-cache.json",snapshot)));}
}
