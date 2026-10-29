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
   Check(criteria[2].Glyph==(flag=="required"?"Reboot":flag=="clear"?"Success":flag=="old-clear"?"Stale":"Unknown"),"reboot glyph "+flag+"/"+server+"/"+active);
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
  foreach(var pair in new[]{("Установка · KB", "Install"),("Скачивание · KB", "Download"),("WSUS · поиск", "Search"),("Диагностика", "Search"),("Очистка кэша", "Repair"),("Ожидание результата", "Stale"),("Неизвестный этап", "Working")})Check(StatusGlyph.ForOperation50(pair.Item1)==pair.Item2,"operation glyph "+pair.Item1);
  foreach(string tone in new[]{"Success","Error","Warning","Stale","Unknown"})Check(StatusGlyph.ForTone50(tone)==tone,"criterion glyph "+tone);
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
  Check(!ObservationExpired51(now.AddHours(-47).ToString("o"),now),"51 observation before absolute deadline");
  Check(ObservationExpired51(now.AddDays(-2).ToString("o"),now)&&ObservationExpired51(now.AddDays(-3).ToString("o"),now),"51 observation deadline is stable after restart");
  Check(ObservationExpired51("invalid",now),"51 malformed observation start does not grant indefinite waiting");
  Check(StatusGlyph.Shape("Working")=="M 5,2 L 16,9 L 5,16 Z","51 generic execution glyph is not a gear");
  var waiting51=new MaintenancePlan{Phase="Ожидается перезагрузка",BootUtc=boot,Cycles=1};
  var signature51=MaintenanceEventSignature51(waiting51);
  for(int i51=0;i51<10;i51++){waiting51.Detail=i51%2==0?"Клиент пока не ответил; повторный опрос запланирован":"Перезагрузите компьютер";Check(signature51==MaintenanceEventSignature51(waiting51),"51 polling detail is not a phase transition "+i51);}
  waiting51.Cycles++;Check(signature51!=MaintenanceEventSignature51(waiting51),"51 next cycle gets separate history event");
  var watch51=new RebootWatch{Confirmed=true,ExpectedUtc=now.AddSeconds(65)};
  Check(RebootHeadline51(watch51,now).EndsWith("01:05"),"51 confirmed reboot countdown");
  Check(RebootHeadline51(watch51,now.AddSeconds(66))=="Ожидается ответ после перезагрузки","51 due time is not reboot success");
  watch51.ReadyAfter=now.AddMinutes(2);Check(RebootHeadline51(watch51,now.AddSeconds(66)).StartsWith("Новая загрузка подтверждена"),"51 returned boot awaits state verification");
  // 0.51: connectivity changes must invalidate every list presentation without
  // changing the saved result or creating any remote work.
  var row51=new ComputerRow(new(){Status="Running",State=new(){Stage="Скачивание · fixture"}});
  var changed51=new HashSet<string>();row51.PropertyChanged+=(sender,args)=>changed51.Add(args.PropertyName??"");
  row51.SetConnection("Connected", "fresh");row51.SetOutcome("Working","saved running");row51.SetActivity(true,null,"running",false);
  Check(row51.DisplayOutcome51=="Download"&&row51.Working,"51 connected operation uses stage glyph");
  foreach(var outcome51 in new[]{"Working","Success","Error","Reboot"}){
   row51.SetOutcome(outcome51,"saved "+outcome51);row51.SetConnection("Unreachable","last response: fixture");
   Check(row51.DeviceBrush39.ToString()==Theme.Brush("TextMuted").ToString()&&!row51.Working&&!row51.WorkBusy&&row51.DisplayOutcome51=="Unknown","51 offline "+outcome51);
   Check(row51.Outcome==outcome51,"51 saved outcome preserved "+outcome51);
   row51.SetConnection("Connected","returned");Check(row51.Working,"51 presentation resumes after response "+outcome51);
  }
  Check(new[]{"DeviceBrush39","DisplayOutcome51","Working","ActivityVisibility"}.All(changed51.Contains),"51 link changes notify list bindings");
  var installed51=new RemoteState{Status="AwaitingReboot",InstalledCount38=1,Results=new(){new(){Kind="Updates",ExitCode=0,Ended=at}}};
  var old51=Report();old51.NeededCount=1;old51.FailedCount=1;
  var criteria51=Criteria45.Build(new("Reboot","",""),old51,clean with{Required=true},installed51,false,scan,1,false,"",boot);
  Check(criteria51[0].Tone=="Success"&&criteria51[0].Glyph=="Success","51 installation success stays green independently of old report");
  Check(CollectorNotice43.Parent==IdentityHeader34,"51 collector details belong to header");
  File.WriteAllLines(Path.Combine(Path.GetTempPath(),"ui-status50-matrix.txt"),observations);
 }
}
