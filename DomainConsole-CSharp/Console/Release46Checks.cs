using System;
using System.Windows;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease46(){
  if(!Application.Current.Resources.Contains("ReadinessDisclosure46"))throw new Exception("Borderless readiness template missing");
  if(EventHeader46==null||EventCopySelected36.Content is string)throw new Exception("History header actions lost icons");
  var sections=LogViewer.Sections("## Result\nhello\n## Empty\n");if(sections.Count!=1)throw new Exception("Empty section retained");
 }
}
