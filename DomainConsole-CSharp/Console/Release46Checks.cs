using System;
using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease46(){
  System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"maintenance47.ps1"),MaintenanceRegistration47.Script,new System.Text.UTF8Encoding(true));
  var stopJob47=new RemoteJob{OutputChannel="WSUS",Steps=new(){new(){Kind="CacheCleanup"},new(){Kind="PowerShell"},new(){Kind="Updates"}}};
  var stopState47=new RemoteState{Step=1,SupportsImmediateStop=true,SupportsWuaCancel=true,StopStatus="ForceRequired",Stage="Preparation KB890830"};
  if(StopPolicy47.For(stopJob47,stopState47)!=new StopPolicy47(false,false,false,false))throw new Exception("Cache preparation exposed WUA or forced termination");
  stopState47.Step=2;
  if(StopPolicy47.For(stopJob47,stopState47)!=new StopPolicy47(false,true,true,true))throw new Exception("Text preparation lost its command stop policy");
  stopState47.Step=3;
  if(StopPolicy47.For(stopJob47,stopState47)!=new StopPolicy47(true,false,true,true))throw new Exception("MRT lost verified force route");
  stopState47.Stage="Installing cumulative update";
  if(StopPolicy47.For(stopJob47,stopState47).Force)throw new Exception("Ordinary WUA update exposed forced termination");
  stopState47.SupportsWuaCancel=false;
  if(StopPolicy47.For(stopJob47,stopState47).Immediate)throw new Exception("Old executor exposed WUA cancel");
  stopState47.Step=0;
  if(StopPolicy47.For(stopJob47,stopState47)!=new StopPolicy47(false,false,false,false)||StopPolicy47.For(null,stopState47).Force)throw new Exception("Unknown step inferred from future updates");
  var cleanupRows47=new List<ComputerRow>{new(new TargetRecord{Host="first.cleanup.invalid",ConnectionSnapshot=true,ConnectionAddress="192.0.2.1"}),new(new TargetRecord{Host="second.cleanup.invalid",ConnectionSnapshot=true,ConnectionAddress="192.0.2.2"})};
  var cleanupTargets47=CaptureCollectorTargets47(cleanupRows47);cleanupRows47[1].Target.ConnectionAddress="192.0.2.99";cleanupRows47.Clear();
  if(cleanupTargets47.Count!=2||cleanupTargets47[0].Target.ConnectionAddress!="192.0.2.1"||cleanupTargets47[1].Target.ConnectionAddress!="192.0.2.2")throw new Exception("Confirmed collector cleanup targets changed during sequential work");
  AwaitUi39(VerifySubmission47());
  AwaitUi39(VerifyRecoveryPipeline47());
  string shellError47="Внутренняя ошибка Windows PowerShell. Ошибка 80070570 при загрузке управляемой оболочки Windows PowerShell.";
  string diagnosed47=ExecutionDiagnosis.PowerShellProcessFailure47(-65536,false,shellError47,"step-1");
  if(!diagnosed47.Contains("команда не выполнялась")||!diagnosed47.Contains("0x80070570")||!diagnosed47.Contains(shellError47)||ResultPresentation36.BriefFailure(diagnosed47)!="PowerShell не запускается · 0x80070570")throw new Exception("PowerShell startup failure lost original cause");
  if(ExecutionDiagnosis.PowerShellProcessFailure47(-65536,true,shellError47,"step-1").Contains("команда не выполнялась"))throw new Exception("Script stderr mistaken for shell startup failure");
  if(ExecutionDiagnosis.PowerShellProcessFailure47(-65536,false,"","step-1").Contains("0x80070570"))throw new Exception("Unknown exit assigned invented cause");
  var killRoot47=Environment.GetEnvironmentVariable("DOMAINCONSOLE_INTERRUPTION47");
  if(!string.IsNullOrEmpty(killRoot47)){
   var killed47=System.Text.Json.JsonSerializer.Deserialize<List<RemoteState>>(System.IO.File.ReadAllText(System.IO.Path.Combine(killRoot47,"recovered-states47.json")))!;
   if(killed47.Count!=4)throw new Exception("Abrupt-process results missing");
   foreach(var state47 in killed47){
    var fixture47=new RemoteJob{Targets=new(){new(){Host="killed47.fixture.invalid"}}};BeginMaintenance(fixture47);
    try{var data47=new PollResult{AgentAlive=false,TaskState="Missing",BootUtc=DateTimeOffset.UtcNow.AddDays(-1).ToString("o"),State=state47};
     ReconcilePlan44(fixture47.Targets[0].Host,fixture47.Id,data47);var plan47=maintenancePlans[fixture47.Targets[0].Host];
     if(!plan47.RecoveryCheck47||plan47.Done)throw new Exception("Killed same-boot worker did not schedule reconciliation");
     plan47.JobId="";plan47.PlanId37="";QueueMaintenanceJob(plan47,fixture47.Targets[0]);var next47=History.First(j=>j.Id==plan47.JobId);
     if(next47.Steps.Single().Kind!="UpdateScan")throw new Exception("Killed worker resumed installation directly");
     FinishPlan44(fixture47.Targets[0].Host);History.Remove(next47);
    }finally{maintenancePlans.Remove(fixture47.Targets[0].Host);SaveMaintenance();}
   }
  }

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
