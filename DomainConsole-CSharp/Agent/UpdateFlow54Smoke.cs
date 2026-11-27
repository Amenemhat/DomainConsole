using System;
using System.Collections.Generic;
using System.IO;
using DomainConsole.Shared;
namespace DomainConsole.Agent {
 public sealed class Identity54Fixture {public string UpdateID{get;set;} public int RevisionNumber=>3;}
 public sealed class Behavior54Fixture {public bool CanRequestUserInput=>false;}
 public sealed class DownloadContent54Fixture {public string DownloadUrl=>"http://wsus.invalid/Content/fixture.cab";}
 public sealed class DownloadContents54Fixture {public int Count=>1;public object Item(int i)=>new DownloadContent54Fixture();}
 public sealed class Update54Fixture {public DownloadContents54Fixture DownloadContents=>new DownloadContents54Fixture();public Collection54Fixture BundledUpdates=>new Collection54Fixture();public Identity54Fixture Identity{get;set;} public string Title{get;set;} public bool IsDownloaded{get;set;} public bool IsInstalled{get;set;}public bool EulaAccepted=>true;public Behavior54Fixture InstallationBehavior=>new Behavior54Fixture();}
 public sealed class Collection54Fixture {public List<Update54Fixture> Items=new List<Update54Fixture>();public int Count=>Items.Count;public object Item(int i)=>Items[i];public int Add(Update54Fixture u){Items.Add(u);return Items.Count;}}
 public sealed class Result54Fixture {public int ResultCode{get;set;}=2;public int HResult=>ResultCode==2?0:unchecked((int)0x80240034);public bool RebootRequired{get;set;} public Collection54Fixture Updates{get;set;} public object GetUpdateResult(int i)=>this;}
 public sealed class Search54Fixture {public int ServerSelection{get;set;} public bool Online{get;set;} public Result54Fixture Result;public object BeginSearch(string criteria,object callback,object state)=>new FakeSearchJob();public object EndSearch(object job)=>Result;}
 public sealed class Transfer54Fixture {
  public Collection54Fixture Updates{get;set;}public bool AllowSourcePrompts{get;set;}public bool IsBusy {get {if(Owner.BusyReads<=0)return false;Owner.BusyReads--;return true;}}public Session54Fixture Owner;
  public object BeginDownload(object a,object b,object c){Owner.Downloads++;return new FakeSearchJob();}
  public object EndDownload(object op)=>new Result54Fixture{ResultCode=Owner.FailFirst&&Updates.Items[0].Title=="KB A"?3:2};
  public object BeginInstall(object a,object b,object c){if(Owner.Race){Owner.Race=false;Updates.Items[0].IsInstalled=true;throw new System.Runtime.InteropServices.COMException("Another install",unchecked((int)0x80240009));}Owner.Installs++;return new FakeSearchJob();}
  public object EndInstall(object op)=>new Result54Fixture{RebootRequired=Owner.Reboot};
 }
 public sealed class Session54Fixture {public string ClientApplicationID{get;set;} public int Downloads,Installs,BusyReads;public bool FailFirst,Reboot,Race;public Search54Fixture Search=new Search54Fixture();public object CreateUpdateSearcher()=>Search;public object CreateUpdateDownloader()=>new Transfer54Fixture{Owner=this};public object CreateUpdateInstaller()=>new Transfer54Fixture{Owner=this};}
 public sealed class Info54Fixture {public bool RebootRequired=>false;}
 internal static partial class Program {
  static Func<string,object> fixtureCom54;
  static Func<ExistingUpdateTransfer[]> fixtureTransfers54;
  static void UpdateFlow54Smoke(){
   var originalJob=Job;var originalState=State;
   try{
    fixturePolicy54=()=>Tuple.Create("",0,1);Job=new RemoteJob{UpdateSource54="WindowsUpdate"};State=new RemoteState{Status="Running"};
    bool denied=false;fixtureCom54=name=>throw new Exception("Blocked source activated COM");
    try{Updates("flow54-blocked",true);}catch(InvalidOperationException){denied=true;}
    if(!denied)throw new Exception("Internet policy restriction bypassed");
    fixturePolicy54=()=>Tuple.Create("",0,0);
    foreach(string mode in new[]{"scan","install","partial","reboot","empty","cached"}){
     var session=new Session54Fixture{FailFirst=mode=="partial",Reboot=mode=="reboot"};
     var updates=new Collection54Fixture();if(mode!="empty")foreach(string name in new[]{"A","B"})updates.Items.Add(new Update54Fixture{Title="KB "+name,IsDownloaded=mode=="cached",Identity=new Identity54Fixture{UpdateID=Guid.NewGuid().ToString()}});
     session.Search.Result=new Result54Fixture{Updates=updates};
     fixtureCom54=name=>name=="Microsoft.Update.Session"?(object)session:name=="Microsoft.Update.UpdateColl"?new Collection54Fixture():name=="Microsoft.Update.SystemInfo"?new Info54Fixture():throw new Exception("Unexpected COM activation: "+name);
     Job=new RemoteJob{UpdateSource54="WindowsUpdate",AutoInstall54=mode!="scan"};State=new RemoteState{Status="Running"};
     int code=Updates("flow54-"+mode,true);
     int expected=mode=="scan"||mode=="empty"?0:mode=="partial"||mode=="reboot"?1:2;
     if(session.Search.ServerSelection!=2||!session.Search.Online||session.Installs!=expected||State.InstalledCount38!=expected||State.UpdatesRemaining!=updates.Count-expected||State.ReportRequested)throw new Exception("Auto-install/source/count/report flow failed: "+mode);
     if(code!=(mode=="partial"?1:0)||State.RebootRequired!=(mode=="reboot"))throw new Exception("Partial failure or reboot result lost: "+mode);
     string saved=File.ReadAllText(FilePath("flow54-"+mode+".updates.json"));
     if(expected>0&&(!saved.Contains("ResultCode")||!saved.Contains("Revision")||!saved.Contains("Ended54")))throw new Exception("Individual installation result missing");
     if(mode=="partial"&&(!saved.Contains("Download")||!saved.Contains("KB A")||!saved.Contains("KB B")))throw new Exception("Partial batch erased outcome");
     int downloads=mode=="scan"||mode=="cached"||mode=="empty"?0:mode=="reboot"?1:2;
     if(session.Downloads!=downloads)throw new Exception("Download-to-install flow failed in mode "+mode);
    }
    foreach(string mode in new[]{"background-download","background-disappeared","background-install","installer-race"}){
     var update=new Update54Fixture{Title="KB external",IsDownloaded=mode is not ("background-download" or "background-disappeared"),IsInstalled=mode=="background-install",
      Identity=new Identity54Fixture{UpdateID=Guid.NewGuid().ToString()}};
     var session=new Session54Fixture{BusyReads=mode=="background-install"?1:0,Race=mode=="installer-race"};
     var collection=new Collection54Fixture();collection.Add(update);
     session.Search.Result=new Result54Fixture{Updates=collection};
     fixtureCom54=name=>name=="Microsoft.Update.Session"?(object)session:name=="Microsoft.Update.UpdateColl"?new Collection54Fixture():name=="Microsoft.Update.SystemInfo"?new Info54Fixture():throw new Exception(name);
     int snapshots=0;
     fixtureTransfers54=()=>{
      if(++snapshots!=1)return Array.Empty<ExistingUpdateTransfer>();
      if(mode=="background-download"){
       var fresh=new Collection54Fixture();fresh.Add(new Update54Fixture{Title=update.Title,Identity=update.Identity,IsDownloaded=true});
       session.Search.Result=new Result54Fixture{Updates=fresh};
      }
      return new[]{new ExistingUpdateTransfer{Id="background",Urls=new[]{"http://wsus.invalid/Content/fixture.cab"},Files=1,Bytes=100,Total=200}};
     };
     State=new RemoteState{Status="Running"};
     Job=new RemoteJob{UpdateSource54="WindowsUpdate",AutoInstall54=true};
     int code=Updates("flow54-"+mode,true);
     bool expectedInstall=mode is "background-download" or "background-disappeared";
     if(code!=0||session.Downloads!=(mode=="background-disappeared"?1:0)||session.Installs!=(expectedInstall?1:0)||State.InstalledCount38!=1||session.Search.Online||
        (mode=="background-disappeared"&&snapshots!=3)||(mode=="background-download"&&snapshots!=1))
      throw new Exception("Existing Windows Update operation was not reused: "+mode);
    }
    Console.WriteLine("0.54: real update flow with isolated WUA fixture: scan-only, auto-install, partial download failure, reboot boundary, empty result, Internet source and retained package metadata passed.");
   }finally{fixturePolicy54=null;fixtureCom54=null;fixtureTransfers54=null;Job=originalJob;State=originalState;}
  }
 }
}
