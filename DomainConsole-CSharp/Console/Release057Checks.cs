using System.Runtime.InteropServices;
using System.Windows;
using DomainConsole.Shared;

namespace DomainConsole;

public partial class MainWindow {
 void VerifyRelease057(){
  var now=DateTimeOffset.UtcNow;
  var connection=new ClientConnectionState();
  foreach(var offset in new[]{0,30,60})connection.Observe38(false,"WinRM timeout",now.AddSeconds(offset),now.AddSeconds(offset));
  var loss=now.AddSeconds(60);
  var stage=new RemoteState{Stage="Установка · тест",Progress=24,Updated=now.AddHours(-1).ToString("o")};
  if(!connection.InLinkGrace(loss)||connection.RecoveryDue(loss.AddSeconds(29))||!connection.RecoveryDue(loss.AddSeconds(30)))throw new Exception("Link recovery schedule");
  if(InterruptedTransfer(connection,stage,loss.AddMinutes(14)) is not {Busy:true,Percent:24}||InterruptedTransfer(connection,stage,loss.AddMinutes(15)) is not {Busy:false,Percent:null})throw new Exception("Grace boundary or stage changed");
  connection.Observe38(true,"",loss.AddMinutes(16),loss.AddMinutes(16));
  if(InterruptedTransfer(connection,stage,loss.AddMinutes(17))!=null)throw new Exception("Reachable stationary installer treated as disconnected");
  var old=new RebootView40(true,true,now.ToString("o"),new[]{"CBS"},"");
  if(ExpireUnavailableProof(old,now.AddHours(24).AddTicks(-1)).Required!=true||ExpireUnavailableProof(old,now.AddHours(24)) is not {Required:null,Fresh:false} expired||expired.Sources.Length!=1)throw new Exception("Reboot evidence expiry");
  var row=new ComputerRow(new(){AdDisabled=true,OS="Windows Server"});row.SetConnection("Connected","fixture");row.SetNeeded37(6,true,"fixture");row.SetIdentityConflict58(true);
  if(row.DisplayOutcome51!="AdDisabled"||row.DeviceKind38!="Server"||row.OperationalVisibility!=Visibility.Collapsed||row.NeededVisibility37!=Visibility.Collapsed||row.IdentityVisibility58!=Visibility.Collapsed||!Equals(row.NameBrush,row.DeviceBrush39)||!Equals(row.NameBrush,row.OutcomeBrush))throw new Exception("Disabled AD navigation appearance");
  if(!App.IsRenderFailure(new Exception("wrapped",new COMException("render",unchecked((int)0x88980406))))||App.IsRenderFailure(new InvalidOperationException("test")))throw new Exception("Render failure classification");
  using(var before=new RebootAdmission()){
   if(!before.Cancel())throw new Exception("Cancellation before reboot admission failed");
   try{before.Enter();throw new Exception("Cancelled reboot entered transport");}catch(OperationCanceledException){}
  }
  using(var sent=new RebootAdmission()){sent.Enter();if(sent.Cancel())throw new Exception("Admitted request was cancelled locally");}
  var freshReport=new WsusReport{NeededCount=6,RequiredComplete=true,LastReportedUtc=now.ToString("o"),LastSyncResult="Succeeded"};
  var staleScan=ClientReadiness41.Evaluate(freshReport,false,true,old with {Required=null},null,false,true,0,now.AddDays(-7).ToString("o"),"",now,24);
  if(staleScan.Kind!="Needed")throw new Exception("Old scan overrode fresh server-only count");
  VerifyRecentOrder057();
  AwaitUi39(VerifySurvey057());
  var group=Enumerable.Range(0,5).Select(i=>new ClientIdentity58{Machine=Guid.NewGuid().ToString(),SusClientId="11111111-2222-3333-4444-555555555555",Server="http://example.invalid",Name="test-"+i,At=now.ToString("o")}).ToList();
  group.Add(group[0]);if(!IdentityPolicy58.Confirmed(group,group[0],now)||IdentityPolicy58.Participants(group,group[0],now).Count!=5)throw new Exception("WSUS identity group or alias");
  var ambiguous=Store.Clone(group[0]);ambiguous.Name="different-fqdn";group.Add(ambiguous);
  if(IdentityPolicy58.Participants(group,group[0],now).Count!=4)throw new Exception("Ambiguous hardware ID admitted to repair");
  if(IdentityPolicy58.Confirmed(group,group[0],now.AddMinutes(10)))throw new Exception("Stale WSUS identity");
 }
 async Task VerifySurvey057(){
  var before=survey;bool enabled=AutoServiceEnabled;var oldWindow=refreshSurvey;var jobCount=History.Count;
  var rows=Enumerable.Range(0,5).Select(i=>new ComputerRow(new(){Name="clone-"+i,Host="clone-"+i+".survey.invalid",OU="OU=Lab"+i})).ToList();
  var id=Guid.NewGuid().ToString();string key="http://wsus.survey.invalid:8530|"+id;
  var reports=rows.Select((r,i)=>new ClientIdentity58{Host=r.Host,Name=r.Host,Machine=Guid.NewGuid().ToString(),SusClientId=id,Server="http://wsus.survey.invalid:8530",UseWUServer=1,At=DateTimeOffset.UtcNow.ToString("o")}).ToList();
  var reads=new List<string>();
  var delayed=new TaskCompletionSource<ClientIdentity58>(TaskCreationOptions.RunContinuationsAsynchronously);
  Task<ClientIdentity58> Client(TargetRecord t,CancellationToken token){reads.Add(t.Host);return t.Host==rows[0].Host?delayed.Task:Task.FromResult(reports.Single(r=>r.Host==t.Host));}
  Task<IdentityServerReply58> Server(WsusSettings _,string expected,CancellationToken token)=>Task.FromResult(new IdentityServerReply58{Id=expected,Name=rows[1].Host});
  try{
   AutoServiceEnabled=true;refreshSurvey=null;
   foreach(var row in rows)Computers.Add(row);
   foreach(var previous in reports.Skip(2))identities58[previous.Host]=Store.Clone(previous);
   var run=RunIdentitySurvey(rows[0],false,false,Client,Server);
   await RunIdentitySurvey(rows[0],false,false,Client,Server);
   if(reads.Count!=1)throw new Exception("Repeated identity click did not join");
   delayed.SetResult(reports[0]);await run;
   if(survey.Results.Count!=5||survey.Hosts.Count!=5||reads.Distinct().Count()!=5||reads.Count!=5||History.Count!=jobCount||!manualIdentityScopes.TryGetValue(key,out var allowed)||allowed.Count!=0)throw new Exception("Scoped WSUS identity survey lost candidates or authorized a write");
   if(!IdentityPolicy58.Confirmed(identities58.Values,reports[0],DateTimeOffset.UtcNow))throw new Exception("Five fresh identities not confirmed");
   var checkpoint=Store.Read("identity-survey.json",new IdentitySurvey());
   if(checkpoint.Owner!=rows[0].Host||checkpoint.Results.Count!=5||checkpoint.ServerName!=rows[1].Host)throw new Exception("Survey checkpoint not restored");
  }finally{
   AutoServiceEnabled=enabled;refreshSurvey=oldWindow;survey=before;
   foreach(var row in rows){Computers.Remove(row);identities58.Remove(row.Host);clientConnections.Remove(row.Host);liveConnections.Remove(row.Host);}
   manualIdentityScopes.Remove(key);compromisedIds58.Remove(key);
   Store.Write("client-identities58.json",identities58);Store.Write("identity-survey.json",survey);Store.Write("identity-scope-approvals.json",manualIdentityScopes);Store.Write("compromised-ids58.json",compromisedIds58.ToList());Store.Write("client-connections.json",clientConnections);
  }
 }
 void VerifyRecentOrder057(){
  var saved=recentHosts.ToList();var pins=workHosts.ToList();int limit=recentLimit;int color=Theme.Current;bool expanded=RecentGroup.IsExpanded;
  var rows=Enumerable.Range(0,8).Select(i=>new ComputerRow(new(){Name="Lab-"+i,Host="recent057-"+i+".invalid",AdDisabled=i==4})).ToList();
  foreach(var item in rows)Computers.Add(item);
  try{
   recentLimit=5;recentHosts.Clear();workHosts.Clear();
   rows[0].SetOutcome("Error","fixture");rows[0].SetConnection("Unreachable","fixture");rows[1].SetOutcome("Success","fixture");
   rows[4].SetConnection("Connected","fixture");rows[4].SetNeeded37(6,true,"fixture");
   PromoteRecent(new[]{rows[0].Host},false);PromoteRecent(rows.Skip(1).Take(5).Select(r=>r.Host),false);
   if(RecentComputers.Contains(rows[0])||RecentComputers.Count(r=>rows.Contains(r))!=5||!rows[1].StartsReady)throw new Exception("Bulk rotation or shared ready limit");
   workHosts.Add(rows[0].Host);
   foreach(var r in rows.Skip(6)){preparing60[PreparationKey60(r.Host,"WSUS")]=("WSUS","Подготовка",DateTimeOffset.UtcNow);PromoteRecent(new[]{r.Host},true);}
   if(RecentComputers[0]!=rows[7]||RecentComputers[1]!=rows[6]||!RecentComputers.Contains(rows[0])||RecentComputers.Distinct().Count()!=RecentComputers.Count)throw new Exception("Preparation order or pin lost");
   foreach(int theme in new[]{0,1,2}){Theme.Apply(theme,false);RefreshRecent();UpdateLayout();foreach(var size in new[]{(1000d,640d),(1920d,1080d)})CaptureContent38("priority57-navigation-"+theme+"-"+size.Item1,size.Item1,size.Item2);}
  }finally{
   foreach(var r in rows){preparing60.Remove(PreparationKey60(r.Host,"WSUS"));Computers.Remove(r);}
   recentHosts.Clear();recentHosts.AddRange(saved);workHosts.Clear();workHosts.AddRange(pins);recentLimit=limit;Store.Write("recent-computers.json",recentHosts);RecentGroup.IsExpanded=expanded;Theme.Apply(color,false);RefreshRecent();
  }
 }
}
