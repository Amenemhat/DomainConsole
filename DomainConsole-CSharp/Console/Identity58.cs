using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 readonly Dictionary<string,ClientIdentity58> identities58=new(Store.Read("client-identities58.json",new Dictionary<string,ClientIdentity58>()),StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> compromisedIds58=new(Store.Read("compromised-ids58.json",new List<string>()),StringComparer.OrdinalIgnoreCase);
 void ObserveIdentity58(string host,ClientIdentity58? identity){
  if(identity==null||identity.At.Length==0)return;identity.Host=host;
  if(identities58.TryGetValue(host,out var previous)&&string.CompareOrdinal(previous.At,identity.At)>0)return;
  identities58[host]=identity;Store.Write("client-identities58.json",identities58);
  if(IdentityPolicy58.Confirmed(identities58.Values,identity,DateTimeOffset.UtcNow)){
   compromisedIds58.Add(identity.Key);Store.Write("compromised-ids58.json",compromisedIds58.ToList());
   foreach(var member in IdentityPolicy58.Participants(identities58.Values,identity,DateTimeOffset.UtcNow))Event31(member.Host,"identity58",identity.Key,"Подтверждён общий SusClientId","Warning","ID: "+identity.SusClientId+". Участники: "+string.Join(", ",IdentityPolicy58.Participants(identities58.Values,identity,DateTimeOffset.UtcNow).Select(r=>r.Name))+". Требуется перерегистрация всех участников.");
  }
  if(AutoServiceEnabled&&compromisedIds58.Contains(identity.Key))_=RepairIdentityGroup58(host,false);
 }
 bool IdentityConflict58(string host)=>DuplicateRecords58(host)||identities58.TryGetValue(host,out var i)&&compromisedIds58.Contains(i.Key);
 void RefreshIdentity58(string host){
  HeaderIdentity58.Visibility=IdentityConflict58(host)?Visibility.Visible:Visibility.Collapsed;
  HeaderIdentity58.Content=identities58.TryGetValue(host,out var i)&&compromisedIds58.Contains(i.Key)?"Конфликт ID":"Несколько записей WSUS";
 }
 void Identity58_Click(object sender,RoutedEventArgs e){
  var row=Current();var lines=new List<string>();
  if(identities58.TryGetValue(row.Host,out var identity)){
   lines.Add("Клиент: "+identity.Name+"\nSusClientId: "+identity.SusClientId+"\nИсточник: "+identity.Server+"\nПроверено: "+Display36.Date(identity.At));
   foreach(var peer in identities58.Values.Where(x=>x.Key==identity.Key))lines.Add(peer.Name+" · "+Display36.Date(peer.At));
  }
  if(SnapshotMatches58)foreach(var record in serverSnapshot58.Find(ConnectionStore.WsusName(row.Host)))lines.Add("WSUS: "+record.Name+" · ID "+record.Id+" · отчёт "+Display36.Date(record.Summary?.LastReportedUtc??""));
  lines.Add("Сначала подтвердите связь участников. Одинаковое имя или IP сами по себе не доказывают общий ID. Старые записи сервера не удаляются.");
  var window=new Window{Owner=this,Title="Идентичность WSUS · "+row.Name,Width=650,Height=450,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new DockPanel{Margin=new(16)};window.Content=panel;
  var repair=new Button{Content="Перерегистрировать всех участников конфликта…",IsEnabled=identity!=null&&compromisedIds58.Contains(identity.Key)};
  DockPanel.SetDock(repair,Dock.Bottom);panel.Children.Add(repair);
  repair.Click+=async(s,a)=>{window.Close();await RepairIdentityGroup58(row.Host,true);};
  panel.Children.Add(new TextBox{Text=string.Join("\n\n",lines),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});window.Show();
 }
}
public sealed partial class RemoteService {
 public const string IdentityProbe58="""
$dcIdentity58=@{At=[DateTime]::UtcNow.ToString('o');Name='';Machine='';Server='';SusClientId='';Error=''}
try {
 $computer58=Get-CimInstance Win32_ComputerSystem -ErrorAction Stop
 $dcIdentity58.Name=$env:COMPUTERNAME+$(if($computer58.PartOfDomain){'.'+$computer58.Domain}else{''})
 $dcIdentity58.Machine=[string](Get-CimInstance Win32_ComputerSystemProduct -ErrorAction Stop).UUID
 $policy58=Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate' -ErrorAction Stop
 $dcIdentity58.Server=[string]$policy58.WUServer
 $dcIdentity58.SusClientId=[string](Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate' -ErrorAction Stop).SusClientId
}catch{$dcIdentity58.Error=$_.Exception.Message}
""";
}
