using System.IO;
using System.Text;
using System.Security;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease27(){
  var row=Computers.Last();var old=reportCache.GetValueOrDefault(row.Host);var previous=rebootSnapshots.GetValueOrDefault(row.Host);
  try{
   reportCache[row.Host]=new(){Server=OverviewServer,Report=new(){FailedCount=2,InstalledPendingRebootCount=0,LastSyncResult="Succeeded",LastReportedUtc=DateTime.UtcNow.AddMinutes(-5).ToString("o")}};
   rebootSnapshots[row.Host]=new(){Required=true,CollectedUtc=DateTime.UtcNow.ToString("o"),Sources=new(){"fixture"}};
   if(OverviewStatus(row).Kind!="Reboot"||RebootReason(row).Length==0||reportCache[row.Host].Report.FailedCount!=2||!criteria45.GetValueOrDefault(row.Host,new()).SelectMany(g=>g.Checks).Any(c=>c.Tone=="Error"))throw new Exception("Server errors hidden or confirmed reboot evidence lost");
   rebootSnapshots[row.Host].Required=false;if(OverviewStatus(row).Kind!="Error")throw new Exception("Unresolved errors hidden after reboot flags clear");
   rebootSnapshots[row.Host].Required=null;if(OverviewStatus(row).Kind!="Error")throw new Exception("Unknown reboot flag masks an error");
  }finally{if(old==null)reportCache.Remove(row.Host);else reportCache[row.Host]=old;if(previous==null)rebootSnapshots.Remove(row.Host);else rebootSnapshots[row.Host]=previous;}
  var freshness=new WsusReport{TaskEndedUtc=DateTime.UtcNow.ToString("o"),LastReportedUtc=DateTime.UtcNow.AddMinutes(-2).ToString("o")};if(freshness.Fresh(DateTime.UtcNow.AddMinutes(-10).ToString("o"))||!freshness.Freshness("").Contains("предшествует"))throw new Exception("Report before job end treated as final confirmation");
  var update=new WsusFailedUpdate{Id="fixture",Revision=7,Title="fixture"};var history=new[]{new ClientUpdateHistory{Id="fixture",Revision=7,Operation=1,Date=DateTime.UtcNow.ToString("o"),ResultCode=1,HResult=unchecked((int)0x80242014)},new ClientUpdateHistory{Id="fixture",Revision=7,Operation=1,Date=DateTime.UtcNow.AddHours(-1).ToString("o"),ResultCode=4,HResult=unchecked((int)0x80240034)}};
  if(WsusFailurePresentation.Describe(update,history,true).Contains("Рекомендация по неудачной"))throw new Exception("Old error recommends cleanup while reboot pending");
  if(ConnectionStore.Normalize(" 10.1.2.24 ")!="10.1.2.24"||ConnectionStore.Normalize("2001:db8::1")!="2001:db8::1")throw new Exception("IP parsing failed");
  try{ConnectionStore.Normalize("10.1.2.24;hostname");throw new Exception("Invalid IP accepted");}catch(ArgumentException){}
  string host="endpoint-fixture.invalid";var saved=ConnectionStore.Profiles.GetValueOrDefault(host);
  try{ConnectionStore.Profiles[host]=new(){AuthMode="Specific",Address="192.0.2.10",Identity="fixture-id",ComputerName="FIXTURE",Fqdn=host};
   var captured=ConnectionStore.Capture(new(){Host=host});ConnectionStore.Profiles[host].Address="192.0.2.11";
   if(ConnectionStore.Address(captured)!="192.0.2.10"||ConnectionStore.Address(new(){Host=host})!="192.0.2.11")throw new Exception("Changing IP redirects saved jobs");
   using var password=new SecureString();foreach(char c in "fixture-only-password")password.AppendChar(c);ConnectionStore.SetCredential("192.0.2.10",false,"fixture-user",password);
   var script=RemoteService.SessionCode(captured);
   if(script.Contains("fixture-only-password")||!script.Contains("fixture-id")||!script.Contains("192.0.2.10")||!script.Contains("-Credential"))throw new Exception("Endpoint credential/identity transport invalid");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"endpoint-session.ps1"),script+"try{Invoke-Command -Session $s -ScriptBlock {'PAYLOAD'}}finally{Remove-PSSession $s}",new UTF8Encoding(true));
   captured.ConnectionSsl=true;ConnectionStore.SetCredential("192.0.2.10",true,"fixture-user",password);if(!RemoteService.SessionCode(captured).Contains("-UseSSL"))throw new Exception("HTTPS transport ignored");
   var legacy=new TargetRecord{Host=host,ConnectionSnapshot=true};if(ConnectionStore.Address(legacy).Length>0)throw new Exception("Legacy job moved to new IP");
   if(Store.Clone(captured).ConnectionIdentity!="fixture-id")throw new Exception("Connection snapshot not persisted");
  }finally{if(saved==null)ConnectionStore.Profiles.Remove(host);else ConnectionStore.Profiles[host]=saved;}
  System.Windows.Threading.DispatcherTimer? closeDialog=null;closeDialog=new(){Interval=TimeSpan.FromMilliseconds(200)};closeDialog.Tick+=(s,e)=>{var dialog=OwnedWindows.Cast<System.Windows.Window>().FirstOrDefault(w=>w.Title.StartsWith("Адрес подключения"));if(dialog==null)return;closeDialog!.Stop();dialog.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(dialog);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(Path.GetTempPath(),"ui-connection.png")))encoder.Save(file);dialog.Close();};closeDialog.Start();EditConnection(row,"").GetAwaiter().GetResult();closeDialog.Stop();
  File.WriteAllText(Path.Combine(Path.GetTempPath(),"reboot-state.ps1"),RemoteService.RebootStateScript,new UTF8Encoding(true));
 }
}

