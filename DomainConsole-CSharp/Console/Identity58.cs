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
  if(previous!=null&&identity.Error.Length>0){previous.Error=identity.Error;previous.ErrorAt=identity.At;Store.Write("client-identities58.json",identities58);return;}
  identities58[host]=identity;Store.Write("client-identities58.json",identities58);
  if(IdentityPolicy58.Confirmed(identities58.Values,identity,DateTimeOffset.UtcNow)){
   compromisedIds58.Add(identity.Key);Store.Write("compromised-ids58.json",compromisedIds58.ToList());
   foreach(var member in IdentityPolicy58.Participants(identities58.Values,identity,DateTimeOffset.UtcNow))Event31(member.Host,"identity58",identity.Key,"Подтверждён общий SusClientId","Warning","ID: "+identity.SusClientId+". Участники: "+string.Join(", ",IdentityPolicy58.Participants(identities58.Values,identity,DateTimeOffset.UtcNow).Select(r=>r.Name))+". Требуется перерегистрация всех участников.");
  }
  _=VerifyIdentityRegistration58(host);
  if(AutoServiceEnabled&&compromisedIds58.Contains(identity.Key))_=RepairIdentityGroup58(host,false);
 }
 bool IdentityMismatch58(string host)=>SnapshotMatches58&&identities58.TryGetValue(host,out var identity)&&identity.SusClientId.Length>0&&serverSnapshot58.Records.Any(r=>r.Id.Equals(identity.SusClientId,StringComparison.OrdinalIgnoreCase)&&!r.Name.TrimEnd('.').Equals(identity.Name.TrimEnd('.'),StringComparison.OrdinalIgnoreCase));
 bool IdentityConflict58(string host)=>IdentityMismatch58(host)||identityRepairs58.Values.Any(a=>a.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&a.Phase!="Подтверждено")||DuplicateRecords58(host)||identities58.TryGetValue(host,out var i)&&compromisedIds58.Contains(i.Key);
 void RefreshIdentity58(string host){
  ExpireIdentityRepairs58();
  HeaderIdentity58.Visibility=IdentityConflict58(host)?Visibility.Visible:Visibility.Collapsed;
  HeaderIdentity58.Content=identities58.TryGetValue(host,out var i)&&compromisedIds58.Contains(i.Key)?"Конфликт ID":identityRepairs58.Values.Any(a=>a.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&a.Phase!="Подтверждено")?"Регистрация WSUS":IdentityMismatch58(host)?"Проверить идентичность":"Несколько записей WSUS";
 }
 void Identity58_Click(object sender,RoutedEventArgs e)=>IdentityWindow58().Show();
 Window IdentityWindow58(){
  var row=Current();var lines=new List<string>();
  if(identities58.TryGetValue(row.Host,out var identity)){
   lines.Add("Клиент: "+identity.Name+"\nSusClientId: "+identity.SusClientId+"\nИсточник: "+identity.Server+"\nПроверено: "+Display36.Date(identity.At));
   lines.Add("Сервер отчётов: "+identity.StatusServer+"\nUseWUServer: "+(identity.UseWUServer?.ToString()??"неизвестно")+"\nTargetGroupEnabled: "+(identity.TargetGroupEnabled?.ToString()??"неизвестно")+"; группа: "+identity.TargetGroup+"\nSusClientIdValidation: "+(identity.ValidationBytes.HasValue?identity.ValidationBytes+" байт":"неизвестно"));
   if(identity.Error.Length>0)lines.Add("Последняя проверка не выполнена: "+Display36.Date(identity.ErrorAt)+" · "+identity.Error+". Показаны прежние сведения.");
   foreach(var peer in identities58.Values.Where(x=>x.Key==identity.Key))lines.Add(peer.Name+" · "+Display36.Date(peer.At));
  }
  if(SnapshotMatches58)foreach(var record in serverSnapshot58.Find(ConnectionStore.WsusName(row.Host)).Concat(serverSnapshot58.Records.Where(r=>identity!=null&&r.Id.Equals(identity.SusClientId,StringComparison.OrdinalIgnoreCase))).DistinctBy(r=>r.Id))lines.Add("WSUS: "+record.Name+" · ID "+record.Id+" · отчёт "+Display36.Date(record.Summary?.LastReportedUtc??""));
  foreach(var attempt in identityRepairs58.Values.Where(a=>a.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)))lines.Add("Перерегистрация: "+attempt.Phase+" · "+Display36.Date(attempt.At)+"\nПрежний ID: "+attempt.OldId+"\nНовый ID: "+(attempt.NewId.Length>0?attempt.NewId:"ещё не прочитан")+"\nПроверка до: "+Display36.Date(attempt.DeadlineUtc)+"\nГруппы новой записи: "+(attempt.Groups.Count>0?string.Join(", ",attempt.Groups):"ещё не прочитаны"));
  lines.Add("Сначала подтвердите связь участников. Одинаковое имя или IP сами по себе не доказывают общий ID. Старые записи сервера не удаляются. При серверном назначении групп новая запись может потребовать повторного назначения групп администратором WSUS.");
  var window=new Window{Owner=this,Title="Идентичность WSUS · "+row.Name,Width=650,Height=450,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new DockPanel{Margin=new(16)};window.Content=panel;
  var repair=new Button{Content="Перерегистрировать всех участников конфликта…",IsEnabled=identity!=null&&compromisedIds58.Contains(identity.Key)};
  DockPanel.SetDock(repair,Dock.Bottom);panel.Children.Add(repair);
  var refresh=new Button{Content="Обновить серверный обзор и проверить регистрацию"};DockPanel.SetDock(refresh,Dock.Bottom);panel.Children.Add(refresh);
  refresh.Click+=async(s,a)=>{window.Close();await RunOverview(true);};
  repair.Click+=async(s,a)=>{window.Close();await RepairIdentityGroup58(row.Host,true);};
  panel.Children.Add(new TextBox{Text=string.Join("\n\n",lines),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});return window;
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
 $dcIdentity58.StatusServer=[string]$policy58.WUStatusServer
 $dcIdentity58.TargetGroup=[string]$policy58.TargetGroup
 $dcIdentity58.TargetGroupEnabled=$policy58.TargetGroupEnabled
 $dcIdentity58.UseWUServer=(Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -ErrorAction Stop).UseWUServer
 $registration58=Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate' -ErrorAction Stop
 $dcIdentity58.SusClientId=[string]$registration58.SusClientId
 $dcIdentity58.ValidationBytes=if($null -eq $registration58.SusClientIdValidation){0}else{@($registration58.SusClientIdValidation).Count}
}catch{$dcIdentity58.Error=$_.Exception.Message}
""";
}
