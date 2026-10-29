using System.IO;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyStatus50(){
  var now=DateTimeOffset.UtcNow;string at=now.ToString("o"),scan=now.AddMinutes(-2).ToString("o"),boot=now.AddDays(-1).ToString("o");
  WsusReport Report()=>new(){NeededCount=0,RequiredComplete=true,LastReportedUtc=now.AddMinutes(-1).ToString("o"),LastSyncUtc=scan,LastSyncResult="Succeeded"};
  var clean=new RebootView40(false,true,at,Array.Empty<string>(),"");
  var done=new RemoteState{Status="Completed",Ended=at,UpdatesRemaining=0};
  var observations=new List<string>();
  void Check(bool value,string name){if(!value)throw new Exception("0.50 status: "+name);observations.Add(name+": PASS");}
  foreach(string flag in new[]{"clear","required","unknown","partial","old-clear"})foreach(string server in new[]{"clean","failed","sync-failed","old","partial","needed"})foreach(bool active in new[]{false,true}){
   var report=Report();if(server=="failed")report.FailedCount=1;if(server=="sync-failed")report.LastSyncResult="Failed";if(server=="old")report.LastReportedUtc=now.AddDays(-3).ToString("o");if(server=="partial")report.RequiredComplete=false;if(server=="needed")report.NeededCount=2;
   var reboot=flag switch{"required"=>clean with{Required=true,Sources=new[]{"Windows Update API","RebootRequired (Windows Update)"}},"unknown"=>clean with{Required=null,Fresh=false},"partial"=>clean with{Required=null,Fresh=false,Limitation="CBS не прочитан"},"old-clear"=>clean with{Fresh=false},_=>clean};
   var result=ClientReadiness41.Evaluate(report,false,true,reboot,done,active,true,0,scan,"",now,24,boot);
   string expected=active?"Working":flag=="required"?"Reboot":server=="failed"?"Error":server=="sync-failed"?"SyncError":server=="old"?"Stale":server=="needed"?"Needed":server=="partial"||flag!="clear"?"Warning":"Success";
   Check(result.Kind==expected,flag+"/"+server+"/active="+active);
   var criteria=Criteria45.Build(result,report,reboot,done,active,scan,0,false,"",boot);
   Check(criteria.Count==3&&criteria[2].Action==(!active&&!reboot.Fresh&&result.Kind!="Success"),"action "+flag+"/"+server+"/"+active);
   Check(report.LastReportedUtc==(server=="old"?now.AddDays(-3).ToString("o"):now.AddMinutes(-1).ToString("o")),"report provenance "+flag+"/"+server+"/"+active);
  }
  foreach(string stamp in new[]{"invalid",now.AddHours(1).ToString("o")}){
   var invalid=DescribeReboot40(new(){Required=true,CollectedUtc=stamp},"",now);
   Check(invalid.Required==null&&!invalid.Fresh,"invalid reboot timestamp "+stamp);
  }
  var incomplete=DescribeReboot40(new(){Required=false,CollectedUtc=at,Error="CBS unavailable"},boot,now);
  Check(incomplete.Required==null&&!incomplete.Fresh,"freshly received partial flags are not complete");
  // Identical facts must yield identical text and evidence even off selection.
  var current=Current();var other=new ComputerRow(new(){Host="status50.fixture.invalid",Name="STATUS50"});
  var oldCurrent=criteria45.GetValueOrDefault(current.Host);var oldOther=criteria45.GetValueOrDefault(other.Host);
  var oldSnapshots=new[]{current.Host,other.Host}.ToDictionary(h=>h,h=>rebootSnapshots.GetValueOrDefault(h));
  var oldErrors=new[]{current.Host,other.Host}.ToDictionary(h=>h,h=>overviewErrors.GetValueOrDefault(h));
  var oldLive=new[]{current.Host,other.Host}.ToDictionary(h=>h,h=>liveReports.Contains(h));
  try{
   foreach(string host in oldSnapshots.Keys){rebootSnapshots.Remove(host);overviewErrors.Remove(host);liveReports.Remove(host);}
   var report=Report();report.RequiredComplete=false;var cache=new CachedWsusReport{Found=true,Server=OverviewServer,Report=report};
   var unknown=clean with{Required=null,Fresh=false};var a=ClientReadiness41.Evaluate(report,false,true,unknown,done,false,false,null,"","",now,24);
   var selected=ExplainAssessment45(current,a,cache,unknown,done,false,"",null);var background=ExplainAssessment45(other,a,cache,unknown,done,false,"",null);
   Check(selected==background,"selected and background assessment identical");
   Check(criteria45[other.Host].Count==3&&criteria45[other.Host][2].Action,"background criteria and contextual action present");
  }finally{if(oldCurrent==null)criteria45.Remove(current.Host);else criteria45[current.Host]=oldCurrent;if(oldOther==null)criteria45.Remove(other.Host);else criteria45[other.Host]=oldOther;
   foreach(string host in oldSnapshots.Keys){if(oldSnapshots[host]==null)rebootSnapshots.Remove(host);else rebootSnapshots[host]=oldSnapshots[host]!;if(oldErrors[host]==null)overviewErrors.Remove(host);else overviewErrors[host]=oldErrors[host]!;if(oldLive[host])liveReports.Add(host);else liveReports.Remove(host);}
  }
  // Administrative reread cannot freshen LastReported, nor old failure become a new attempt.
  var historical=Report();historical.LastReportedUtc=now.AddDays(-2).ToString("o");historical.ReceivedUtc41=at;
  Check(ClientReadiness41.Evaluate(historical,false,true,clean,done,false,true,0,scan,"",now,24).Kind=="Stale","old report with fresh administrative read");
  historical=Report();historical.FailedCount=1;historical.FailedUpdates=new(){new(){Id="00000000-0000-0000-0000-000000000050",Revision=3}};
  done.InstalledUpdates45=new(){new(){Id=historical.FailedUpdates[0].Id,Revision=3,At=at}};
  Check(ClientReadiness41.Evaluate(historical,false,true,clean,done,false,true,0,scan,"",now,24).Kind=="Warning","matching newer installation waits for report");
  done.InstalledUpdates45[0].Revision=4;
  Check(ClientReadiness41.Evaluate(historical,false,true,clean,done,false,true,0,scan,"",now,24).Kind=="Error","different revision does not clear failure");
  var restored=Store.Clone(historical);Check(restored.LastReportedUtc==historical.LastReportedUtc&&restored.FailedUpdates[0].Revision==3,"report cache preserves timestamp and revision");
  // Cached synthetic names keep this session-selection test entirely offline.
  string userHost="users50.fixture.invalid",accountA="FIXTURE50\\a",accountB="FIXTURE50\\b";
  var savedUser=users37.GetValueOrDefault(userHost);var savedA=userNames37.GetValueOrDefault(accountA);var savedB=userNames37.GetValueOrDefault(accountB);
  try{
   userNames37[accountA]=new(){Name="Первый Пользователь",CheckedUtc=at};userNames37[accountB]=new(){Name="Второй Пользователь",CheckedUtc=at};users37.Remove(userHost);
   ObserveUsers37(userHost,new(){UsersComplete37=true,CollectedUtc=at,Users37=new(){new(){Account=accountA,Active=true},new(){Account=accountB}}});
   Check(users37[userHost].Account==accountA,"active session chosen initially");
   ObserveUsers37(userHost,new(){UsersComplete37=true,CollectedUtc=at,Users37=new(){new(){Account=accountB,Active=true},new(){Account=accountA}}});
   Check(users37[userHost].Account==accountA,"existing session stable when another becomes active");
   ObserveUsers37(userHost,new(){UsersComplete37=false,CollectedUtc=at});Check(users37[userHost].Current,"partial session read preserves current observation");
   ObserveUsers37(userHost,new(){UsersComplete37=true,CollectedUtc=at});Check(!users37[userHost].Current&&users37[userHost].SeenUtc==at&&users37[userHost].Name=="Первый Пользователь","logout preserves last name and observation time");
   ObserveUsers37(userHost,new(){UsersComplete37=true,CollectedUtc=at,Users37=new(){new(){Account=accountB,Active=true}}});Check(users37[userHost].Name=="Второй Пользователь","new session resolves its own cached name");
  }finally{if(savedUser==null)users37.Remove(userHost);else users37[userHost]=savedUser;if(savedA==null)userNames37.Remove(accountA);else userNames37[accountA]=savedA;if(savedB==null)userNames37.Remove(accountB);else userNames37[accountB]=savedB;Store.Write("client-users37.json",users37);}
  File.WriteAllLines(Path.Combine(Path.GetTempPath(),"ui-status50-matrix.txt"),observations);
 }
}
