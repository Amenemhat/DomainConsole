using System.IO;
using System.Text.Json;
using DomainConsole.Shared;

namespace DomainConsole;
public partial class MainWindow {
 void VerifyBitsRecovery187(){
  const string host="bits-resume.fixture.invalid";
  var endpoint=ConnectionStore.Capture(new TargetRecord{Host=host});
  var now=DateTimeOffset.UtcNow;
  var download=new BackgroundDownload{
   Id=Guid.NewGuid().ToString(),State="Queued",Received=20,Total=100,
   LocalName=@"C:\Temp\agent.cab",RemoteName="http://wsus.fixture.invalid:8530/Content/AA/package.cab",
   SystemOwned=true,Files=1,ModifiedUtc=now.ToString("o")
  };
  var live=new LiveUpdateActivity{UseWsus=true,PolicySource="http://wsus.fixture.invalid:8530",Downloads=new(){download}};
  void Sample(DateTimeOffset at){
   live.CollectedUtc=at.ToString("o");
   RememberBits(host,endpoint,live,at);
  }
  bool Eligible()=>CanResumeBits187(endpoint,download,live,10,now);
  try{
   Sample(now.AddMinutes(-10));
   if(Eligible())throw new Exception("A single queued sample was treated as a stalled download.");
   for(int n=9;n>=1;n--)Sample(now.AddMinutes(-n));
   Sample(now.AddSeconds(-1));
   if(Eligible())throw new Exception("BITS recovery was allowed at 9:59.");
   if(!BitsResumeBlock187(endpoint,download,live,10,now).Contains("без роста байтов подтверждено"))
    throw new Exception("An early manual BITS attempt did not explain why it was skipped.");
   Sample(now);
   if(!Eligible())throw new Exception("Ten minutes of fresh identical BITS byte counts were ignored.");
   if(!HasBitsActivity187(endpoint,live,now)||HasBitsActivity187(endpoint,live,now.AddSeconds(90)))
    throw new Exception("Manual BITS continuation did not distinguish current and stale activity.");
   live.Limitation="WUA · 0x80070005";
   if(HasBitsActivity187(endpoint,live,now)||!BitsResumeBlock187(endpoint,download,live,10,now).Contains("0x80070005"))
    throw new Exception("An incomplete activity read was hidden or reused for manual BITS continuation.");
   live.Limitation="";
   live.InstallerBusy=true;
   if(Eligible())throw new Exception("BITS recovery was allowed during installation.");
   live.InstallerBusy=false;
   var duplicate=Store.Clone(download);
   duplicate.Id=Guid.NewGuid().ToString();
   live.Downloads.Add(duplicate);
   if(Eligible())throw new Exception("BITS recovery was allowed for an ambiguous file.");
   live.Downloads.Remove(duplicate);
   download.State="Suspended";
   if(!Eligible())throw new Exception("Manual resume was refused for an identified paused job.");
   download.State="Transferring";
   if(Eligible())throw new Exception("Moving BITS transfer was presented as recoverable.");
   download.State="Queued";
   ForgetBits(host);
   Sample(now);
   if(Eligible())throw new Exception("A connection break retained the stall timer.");
   var fixture=JsonSerializer.Serialize(new{
    download.Id,download.State,download.Received,download.Total,download.RemoteName,download.LocalName,download.ModifiedUtc,
    live.PolicySource,Automatic=false
   });
   var system=RemoteService.BitsSystemScript187(fixture);
   if(!system.Contains("BitsControl187")||!system.Contains("IgnoreNew")||!system.Contains("SYSTEM"))
    throw new Exception("BITS continuation lost its bounded SYSTEM entry point.");
   var activitySystem=RemoteService.BitsActivitySystemScript187();
   if(!activitySystem.Contains("BitsControl187")||!activitySystem.Contains("Microsoft.Update.Installer")||
      !activitySystem.Contains("IgnoreNew")||!activitySystem.Contains("SYSTEM"))
    throw new Exception("BITS installer preflight did not reuse the bounded SYSTEM operation.");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"bits-activity187-system.ps1"),activitySystem,new System.Text.UTF8Encoding(true));
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"bits-resume187-fixture.json"),fixture);
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"bits-resume187-template.ps1"),RemoteService.BitsResumeScript187("{}"));
  }finally{ForgetBits(host);}
 }
}
