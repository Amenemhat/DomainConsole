using System.IO;
using System.Text;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease42(){VerifyRelease43();
  foreach(string mode in new[]{"Get","Read","Inspect","Recover","Stop"})File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe42-"+mode+".ps1"),RemoteService.SnapshotScript42(mode),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe42-error.ps1"),RemoteService.SnapshotScript42("Get",reboot:"throw 'FIXTURE_REBOOT_ERROR42'"),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe42-slow.ps1"),RemoteService.SnapshotScript42("Get",activity:"[Threading.Thread]::Sleep(90000);'{}'"),new UTF8Encoding(true));
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"probe42-autonomous.ps1"),RemoteService.SnapshotScript42("Get",supervisor:"Start-Sleep -Seconds 90"),new UTF8Encoding(true));
  // Live collector partial source failures must not claim known reboot flags.
  var wait=new PollWait42("Blocked","fixture: требуется сверка",0);if(wait.Next!=DateTimeOffset.MaxValue)throw new Exception("Manual recovery incorrectly schedules collector");
  var key="poll-fixture42.invalid";int count=pollEventKeys42.Count;RecordPollWait42(key,"флаги и аптайм",wait);for(int n=0;n<20;n++)RecordPollWait42(key,"флаги и аптайм",wait);if(pollEventKeys42.Count!=count+1)throw new Exception("Pause event transition cache grows per timer tick");pollEventKeys42.Remove(key+"|флаги и аптайм");readFailures34.Remove(key+"|флаги и аптайм");statusLimitations.Remove(key);
 }
}
