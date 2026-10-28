using System;
using System.Windows;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease46(){
  if(EventFilter47.Items.Count!=4||CollectorRepair47==null)throw new Exception("Collector repair/history filter controls absent");
  var error47=new ClientEvent31{Kind="Error"};var warning47=new ClientEvent31{Kind="Warning"};var success47=new ClientEvent31{Kind="Success"};
  if(!EventMatches47(error47,1)||!EventMatches47(warning47,1)||EventMatches47(success47,1)||EventMatches47(error47,3)||EventMatches47(warning47,2)||!EventMatches47(success47,0))throw new Exception("History filters lose or include wrong event kinds");
  if(CollectorNotice43.BorderThickness!=new Thickness(0))throw new Exception("Collector disclosure retained outer border");

  if(!Application.Current.Resources.Contains("ReadinessDisclosure46"))throw new Exception("Borderless readiness template missing");
  if(EventHeader46==null||EventCopySelected36.Content is string)throw new Exception("History header actions lost icons");
  var sections=LogViewer.Sections("## Result\nhello\n## Empty\n");if(sections.Count!=1)throw new Exception("Empty section retained");
 }
}
