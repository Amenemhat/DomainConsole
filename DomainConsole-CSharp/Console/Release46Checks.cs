using System;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease46(){
  var interrupted47=new RemoteJob{Targets=new(){new(){Host="restart47.fixture.invalid"}}};BeginMaintenance(interrupted47);
  try{var data47=new PollResult{AgentAlive=false,TaskState="Ready",BootUtc=DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),State=new(){Status="InterventionRequired",HasUpdateSteps=true,Started=DateTimeOffset.UtcNow.AddHours(-1).ToString("o")}};
   ReconcilePlan44(interrupted47.Targets[0].Host,interrupted47.Id,data47);
   var plan47=maintenancePlans[interrupted47.Targets[0].Host];if(!plan47.RecoveryCheck47||plan47.Done)throw new Exception("Interrupted update did not enter fresh search recovery");
   var due47=plan47.ReadyAfter;ReconcilePlan44(interrupted47.Targets[0].Host,interrupted47.Id,data47);if(plan47.ReadyAfter!=due47)throw new Exception("Repeated stale status extended recovery deadline");
   plan47.JobId="";plan47.PlanId37="";QueueMaintenanceJob(plan47,interrupted47.Targets[0]);var next47=History.First(j=>j.Id==plan47.JobId);
   if(next47.Steps.Single().Kind!="UpdateScan"||!plan47.Install||plan47.RecoveryCheck47)throw new Exception("Interrupted installation replayed instead of a fresh search");
   FinishPlan44(interrupted47.Targets[0].Host);History.Remove(next47);
  }finally{maintenancePlans.Remove(interrupted47.Targets[0].Host);SaveMaintenance();}
  if(EventFilter47.Items.Count!=4||CollectorRepair47==null)throw new Exception("Collector repair/history filter controls absent");
  var error47=new ClientEvent31{Kind="Error"};var warning47=new ClientEvent31{Kind="Warning"};var success47=new ClientEvent31{Kind="Success"};
  if(!EventMatches47(error47,1)||!EventMatches47(warning47,1)||EventMatches47(success47,1)||EventMatches47(error47,3)||EventMatches47(warning47,2)||!EventMatches47(success47,0))throw new Exception("History filters lose or include wrong event kinds");
  if(CollectorNotice43.BorderThickness!=new Thickness(0))throw new Exception("Collector disclosure retained outer border");

  if(!Application.Current.Resources.Contains("ReadinessDisclosure46"))throw new Exception("Borderless readiness template missing");
  if(EventHeader46==null||EventCopySelected36.Content is string)throw new Exception("History header actions lost icons");
  var sections=LogViewer.Sections("## Result\nhello\n## Empty\n");if(sections.Count!=1)throw new Exception("Empty section retained");
 }
}
