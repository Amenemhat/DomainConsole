using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease26(){
  var output=new PollResult{State=new(){Status="Completed"},Files=new(){new(){Name="runner.log",Body="Начат этап: test"},new(){Name="step-1.err",Body="#< CLIXML\n<Objs/>"},new(){Name="step-1.out",Body="Русский вывод\n--- not-a-file.out ---\nEND"},new(){Name="step-2.out",Body="SECOND_OUTPUT"}}};
  string presented=PresentResult("fixture.invalid",output);
  if(!presented.Contains("## Вывод команды")||!presented.Contains("Русский вывод")||!presented.Contains("SECOND_OUTPUT")||!presented.Contains("END"))throw new Exception("Structured command output lost after CLIXML / marker-like line");
  if(ExecutionDiagnosis.Explain(unchecked((int)0x80070020)).Contains("status.json")||ExecutionDiagnosis.Recommend(unchecked((int)0x80070020)).Contains("status.json"))throw new Exception("WUA sharing violation falsely attributed to agent");
  if(!ExecutionDiagnosis.Explain(unchecked((int)0x80242014)).Contains("перезагруз"))throw new Exception("Pending reboot code not explained");
  var report=new WsusReport{LastSyncResult="Failed",FailedCount=0};if(report.Severity("")!="SyncError"||!report.Format("fixture.invalid","").Contains("клиента с WSUS"))throw new Exception("Client sync failure confused with server availability");report.FailedCount=1;if(report.Severity("")!="Error")throw new Exception("Installation failure hidden");
  var update=new WsusFailedUpdate{Id="fixture",Title="Test update",Revision=7};
  var success=new ClientUpdateHistory{Id="fixture",Revision=7,Operation=1,ResultCode=2,Date=DateTime.UtcNow.ToString("o")};
  string detail=WsusFailurePresentation.Describe(update,new[]{success});
  if(!detail.Contains("Данные расходятся")||detail.Contains("Рекомендация по неудачной записи"))throw new Exception("Successful history treated as failure");
  if(LogViewer.LineTone(WsusFailurePresentation.HistoryText(success))!="Success")throw new Exception("Successful history colored red");
  var pending=new ClientUpdateHistory{Id="fixture",Revision=7,Operation=1,ResultCode=1,HResult=unchecked((int)0x80242014)};
  if(!WsusFailurePresentation.Describe(update,new[]{pending}).Contains("не окончательный")||LogViewer.LineTone(WsusFailurePresentation.HistoryText(pending))!="Warning")throw new Exception("Unfinished history treated as final error");
  success.Revision=6;if(!WsusFailurePresentation.Describe(update,new[]{success}).Contains("Другая ревизия"))throw new Exception("Different update revision incorrectly matched");success.Revision=7;
  update.Events.Add(new(){EventId=167,Message="ReportingEvent.Client.167",IsError=true});
  if(!WsusFailurePresentation.Describe(update,Array.Empty<ClientUpdateHistory>()).Contains("Номер события не является HRESULT"))throw new Exception("Unmapped event invented an error code");
  update.Events[0].ErrorCode=unchecked((int)0x80070020);if(!WsusFailurePresentation.Describe(update,new[]{success}).Contains("0x80070020"))throw new Exception("WSUS event error code lost");
  report.FailureSummary="Код события WSUS: 0x80070020";if(!report.Format("fixture.invalid","").Contains(report.FailureSummary))throw new Exception("Failure detail absent from server report");
  var choice=new ServiceRestartChoice(this);choice.SelectAll(true);var panel=(StackPanel)choice.Content;var checks=panel.Children.OfType<CheckBox>().ToList();if(checks.Count(c=>c.IsChecked==true)!=5)throw new Exception("Select all services did not mark listed services");choice.SelectAll(false);if(checks.Any(c=>c.IsChecked==true))throw new Exception("Clear all services failed");checks[1].IsChecked=true;if(checks[0].IsChecked!=null)throw new Exception("Partial service selection not indeterminate");
  OuTree.UpdateLayout();var root=(TreeViewItem)OuTree.Items[0];var focused=ComputerGrid.SelectedItem;bool expanded=root.IsExpanded;
  ToggleButton? FindToggle(DependencyObject node){if(node is ToggleButton t&&t.Name=="Expander")return t;for(int n=0;n<VisualTreeHelper.GetChildrenCount(node);n++){var found=FindToggle(VisualTreeHelper.GetChild(node,n));if(found!=null)return found;}return null;}
  var toggle=FindToggle(root)??throw new Exception("Tree arrow missing");var click=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseDownEvent,Source=toggle};toggle.RaiseEvent(click);
  if(!click.Handled||root.IsExpanded==expanded||!ReferenceEquals(ComputerGrid.SelectedItem,focused))throw new Exception("Expander changed focus or failed to expand");root.IsExpanded=expanded;
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"wsus-events.ps1"),RemoteService.WsusEventsScript("fixture.invalid",new[]{"00000000-0000-0000-0000-000000000026"},false,8530),new UTF8Encoding(true));
 }
}
