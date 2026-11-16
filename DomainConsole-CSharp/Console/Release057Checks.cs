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
  var group=Enumerable.Range(0,5).Select(i=>new ClientIdentity58{Machine=Guid.NewGuid().ToString(),SusClientId="11111111-2222-3333-4444-555555555555",Server="http://example.invalid",Name="test-"+i,At=now.ToString("o")}).ToList();
  group.Add(group[0]);if(!IdentityPolicy58.Confirmed(group,group[0],now)||IdentityPolicy58.Participants(group,group[0],now).Count!=5)throw new Exception("WSUS identity group or alias");
  if(IdentityPolicy58.Confirmed(group,group[0],now.AddMinutes(10)))throw new Exception("Stale WSUS identity");
 }
}
