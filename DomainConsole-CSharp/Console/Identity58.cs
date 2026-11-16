using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public partial class MainWindow {
 readonly Dictionary<string,ClientIdentity58> identities58=new(Store.Read("client-identities58.json",new Dictionary<string,ClientIdentity58>()),StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> compromisedIds58=new(Store.Read("compromised-ids58.json",new List<string>()),StringComparer.OrdinalIgnoreCase);
 void ObserveIdentity58(string host,ClientIdentity58? identity,bool allowRepair=true){
  if(identity==null||identity.At.Length==0)return;identity.Host=host;
  if(identities58.TryGetValue(host,out var previous)&&string.CompareOrdinal(previous.At,identity.At)>0)return;
  if(previous!=null&&identity.Error.Length>0){previous.Error=identity.Error;previous.ErrorAt=identity.At;Store.Write("client-identities58.json",identities58);return;}
  // A collector reading the same endpoint and hardware may reuse only the still-fresh proof from a manual pass.
  if(identity.VerifiedSource.Length==0&&previous?.VerifiedSource.Length>0&&DateTimeOffset.TryParse(previous.SourceCheckedAt,out var verifiedAt)&&DateTimeOffset.UtcNow-verifiedAt<TimeSpan.FromMinutes(10)&&previous.Server.Equals(identity.Server,StringComparison.OrdinalIgnoreCase)&&previous.Machine.Equals(identity.Machine,StringComparison.OrdinalIgnoreCase)&&previous.Name.Equals(identity.Name,StringComparison.OrdinalIgnoreCase)){
   identity.VerifiedSource=previous.VerifiedSource;identity.ServerId=previous.ServerId;identity.SourceCheckedAt=previous.SourceCheckedAt;
  }
  identities58[host]=identity;Store.Write("client-identities58.json",identities58);
  if(!allowRepair)RememberDiagnosticScope(identity);
  if(IdentityPolicy58.Confirmed(identities58.Values,identity,DateTimeOffset.UtcNow)){
   compromisedIds58.Add(identity.Key);Store.Write("compromised-ids58.json",compromisedIds58.ToList());
   foreach(var member in IdentityPolicy58.Participants(identities58.Values,identity,DateTimeOffset.UtcNow))Event31(member.Host,"identity58",identity.Key,"Подтверждён общий SusClientId","Warning","ID: "+identity.SusClientId+". Участники: "+string.Join(", ",IdentityPolicy58.Participants(identities58.Values,identity,DateTimeOffset.UtcNow).Select(r=>r.Name))+". Требуется перерегистрация всех участников.");
  }
  if(allowRepair)_=VerifyIdentityRegistration58(host);
  if(allowRepair&&surveyWindow==null&&surveyRun==null&&AutoServiceEnabled&&compromisedIds58.Contains(identity.Key))_=RepairIdentityGroup58(host,false);
 }
 bool IdentityMismatch58(string host)=>SnapshotMatches58&&identities58.TryGetValue(host,out var identity)&&identity.SusClientId.Length>0&&serverSnapshot58.Records.Any(r=>r.Id.Equals(identity.SusClientId,StringComparison.OrdinalIgnoreCase)&&!r.Name.TrimEnd('.').Equals(identity.Name.TrimEnd('.'),StringComparison.OrdinalIgnoreCase));
 bool IdentityConflict58(string host)=>IdentityMismatch58(host)||identityRepairs58.Values.Any(a=>a.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&a.Phase!="Подтверждено")||DuplicateRecords58(host)||identities58.TryGetValue(host,out var i)&&compromisedIds58.Contains(i.Key);
 void RefreshIdentity58(string host){
  ExpireIdentityRepairs58();
  HeaderIdentity58.Visibility=IdentityConflict58(host)?Visibility.Visible:Visibility.Collapsed;
  HeaderIdentity58.Content=identities58.TryGetValue(host,out var i)&&compromisedIds58.Contains(i.Key)?"Конфликт ID":identityRepairs58.Values.Any(a=>a.Host.Equals(host,StringComparison.OrdinalIgnoreCase)&&a.Phase!="Подтверждено")?"Регистрация WSUS":IdentityMismatch58(host)?"Проверить идентичность":"Несколько записей WSUS";
 }
 void Identity58_Click(object sender,RoutedEventArgs e)=>OpenIdentitySurvey(Current());
 Window IdentityWindow58(){
  var row=Current();string Detail(){var lines=new List<string>();
  if(identities58.TryGetValue(row.Host,out var identity)){
   lines.Add("Клиент: "+identity.Name+"\nSusClientId: "+identity.SusClientId+"\nИсточник: "+identity.Server+"\nПроверено: "+Display36.Date(identity.At));
   lines.Add("Сервер отчётов: "+identity.StatusServer+"\nUseWUServer: "+(identity.UseWUServer?.ToString()??"неизвестно")+"\nTargetGroupEnabled: "+(identity.TargetGroupEnabled?.ToString()??"неизвестно")+"; группа: "+identity.TargetGroup+"\nSusClientIdValidation: "+(identity.ValidationBytes.HasValue?identity.ValidationBytes+" байт":"неизвестно"));
   if(identity.Error.Length>0)lines.Add("Последняя проверка не выполнена: "+Display36.Date(identity.ErrorAt)+" · "+identity.Error+". Показаны прежние сведения.");
   foreach(var peer in identities58.Values.Where(x=>x.Key==identity.Key))lines.Add(peer.Name+" · "+Display36.Date(peer.At));
  }
  if(SnapshotMatches58)foreach(var record in serverSnapshot58.Find(ConnectionStore.WsusName(row.Host)).Concat(serverSnapshot58.Records.Where(r=>identity!=null&&r.Id.Equals(identity.SusClientId,StringComparison.OrdinalIgnoreCase))).DistinctBy(r=>r.Id))lines.Add("WSUS: "+record.Name+" · ID "+record.Id+" · отчёт "+Display36.Date(record.Summary?.LastReportedUtc??""));
  foreach(var attempt in identityRepairs58.Values.Where(a=>a.Host.Equals(row.Host,StringComparison.OrdinalIgnoreCase)))lines.Add("Перерегистрация: "+attempt.Phase+" · "+Display36.Date(attempt.At)+(attempt.GroupNote.Length>0?"\n"+attempt.GroupNote:"")+"\nПрежний ID: "+attempt.OldId+"\nНовый ID: "+(attempt.NewId.Length>0?attempt.NewId:"ещё не прочитан")+"\nПроверка до: "+Display36.Date(attempt.DeadlineUtc)+"\nГруппы новой записи: "+(attempt.Groups.Count>0?string.Join(", ",attempt.Groups):"ещё не прочитаны"));
  lines.Add("Ручная диагностика читает данные. Изменение ID обнаруженных участников требует подтверждения состава группы.");
  lines.Add("Сначала подтвердите связь участников. Одинаковое имя или IP сами по себе не доказывают общий ID. Старые записи сервера не удаляются. При серверном назначении групп новая запись может потребовать повторного назначения групп администратором WSUS.");
  return string.Join("\n\n",lines);}
  var window=new Window{Owner=this,Title="Идентичность WSUS · "+row.Name,Width=750,Height=560,MinWidth=470,MinHeight=360,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new DockPanel{Margin=new(16)};window.Content=panel;
  var repair=new Button{Content="Перерегистрировать всех участников конфликта…",IsEnabled=false};
  DockPanel.SetDock(repair,Dock.Bottom);panel.Children.Add(repair);
  var buttons=new WrapPanel();DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);
  var peers=new Button{Content="Проверить клиента и кандидатов"};var all=new Button{Content="Проверить весь список AD"};
  var stop=new Button{Content="Остановить"};var resume=new Button{Content="Продолжить"};
  var verify=new Button{Content="Повторить проверку результата"};
  foreach(var button in new[]{peers,all,stop,resume,verify})buttons.Children.Add(button);
  var pages=new TabControl();panel.Children.Add(pages);
  var resultPanel=new DockPanel();
  var headline=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new(4,4,4,8)};DockPanel.SetDock(headline,Dock.Top);resultPanel.Children.Add(headline);
  var table=new DataGrid{IsReadOnly=true,CanUserAddRows=false,AutoGenerateColumns=false,EnableRowVirtualization=true,HeadersVisibility=DataGridHeadersVisibility.Column,SelectionUnit=DataGridSelectionUnit.FullRow};
  foreach(var col in new[]{("Компьютер","Host"),("Проверка","Status"),("Время","At"),("SusClientId","Id"),("Источник WSUS","Server"),("Результат исправления","Repair")}){
   table.Columns.Add(new DataGridTextColumn{Header=col.Item1,Binding=new System.Windows.Data.Binding(col.Item2),MaxWidth=280});
  }
  resultPanel.Children.Add(table);pages.Items.Add(new TabItem{Header="Проверка участников",Content=resultPanel});
  var output=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  pages.Items.Add(new TabItem{Header="Подробности",Content=output});
  void Refresh(){
   bool owner=survey.Owner.Equals(row.Host,StringComparison.OrdinalIgnoreCase);
   var anchor=RepairAnchor58(row.Host);
   bool repairing=anchor!=null&&identityRepairBusy58.Contains(anchor.Key);
   bool busy=surveyRun!=null||repairing;peers.IsEnabled=all.IsEnabled=!busy;stop.IsEnabled=busy;resume.IsEnabled=!busy&&owner&&survey.Results.Count<survey.Hosts.Count;
   var current=anchor;
   var members=current==null?new List<ClientIdentity58>():IdentityPolicy58.Participants(identities58.Values,current,DateTimeOffset.UtcNow);
   bool confirmed=current!=null&&IdentityPolicy58.Confirmed(members,current,DateTimeOffset.UtcNow);
   bool approved=current!=null&&manualIdentityScopes.TryGetValue(current.Key,out var allowed)&&members.Any(m=>allowed.Contains(MemberIdentity(m)));
   var candidates=current==null?new List<ClientIdentity58>():RepairCandidates58(current,true);
   bool expanded=current!=null&&manualIdentityScopes.TryGetValue(current.Key,out var existing)&&existing.Count>0&&candidates.Any(p=>!existing.Contains(MemberIdentity(p)));
   repair.IsEnabled=!busy&&candidates.Count>0;
   repair.Content=expanded?"Подтвердить расширение группы · "+string.Join(", ",candidates.Select(p=>p.Name)):confirmed?"Исправить общий ID · подтверждённых участников: "+members.Count:approved?"Продолжить ранее согласованное исправление":"Исправить подтверждённый конфликт";
   var attempts=RepairGroupFor(row.Host);verify.IsEnabled=!busy&&attempts.Count>0;
   var coverage=owner?$"{(survey.Domain?"Область AD":"Клиент и известные кандидаты")} · проверено {survey.Results.Count} из {survey.Hosts.Count} · ожидают {survey.Hosts.Count-survey.Results.Count} · общий ID подтверждён у {(confirmed?members.Count:0)}": "Выберите область проверки.";
   int failed=owner?survey.Results.Count(p=>p.Value.StartsWith("Не удалось")||p.Value.StartsWith("Неполные")||p.Value.StartsWith("Пропущен")):0;
   int complete=attempts.Count(a=>a.Phase=="Подтверждено");
   string groupState=attempts.Count==0?"":complete==attempts.Count?" · регистрации подтверждены":complete>0?" · частично выполнено":" · ожидается подтверждение";
   string fixedCount=attempts.Count>0?$" · регистрация подтверждена у {attempts.Count(a=>a.Phase=="Подтверждено")} из {attempts.Count}":"";
   headline.Text=coverage+fixedCount+groupState+(repairing?" · передаётся исправление":"")+(owner?" · без подтверждения "+failed+"\n"+survey.ScopeMessage+(survey.Started==default?"":" · "+Age((busy?DateTimeOffset.UtcNow:survey.Updated)-survey.Started))+"\n"+survey.ServerResult:"");
   if(owner)table.ItemsSource=survey.Hosts.Select(h=>{
    var known=identities58.GetValueOrDefault(h);
    bool done=survey.Results.TryGetValue(h,out var result);
    bool ok=done&&known!=null&&IdentityPolicy58.Usable(known)&&result!.Contains(known.SusClientId,StringComparison.OrdinalIgnoreCase);
    return new{Host=h,Status=ok?"Прочитано":done?result:"Ожидает проверки",At=Display36.Date(known?.Error.Length>0?known.ErrorAt:known?.At??""),Id=known?.SusClientId,Server=known?.Server,Repair=identityRepairs58.Values.Where(a=>a.Host.Equals(h,StringComparison.OrdinalIgnoreCase)).OrderByDescending(a=>a.At).FirstOrDefault()?.Phase};
   }).ToList();
   string detail=Detail();if(output.Text!=detail)output.Text=detail;
  }
  peers.Click+=async(s,a)=>await Guard(()=>RunIdentitySurvey(row,false));all.Click+=async(s,a)=>await Guard(()=>RunIdentitySurvey(row,true));
  verify.Click+=async(s,a)=>await Guard(()=>RunIdentitySurvey(row,false,verifyRegistration:true));
  stop.Click+=(s,a)=>{surveyRun?.Cancel();StopIdentityRepair58(row.Host);};resume.Click+=async(s,a)=>await Guard(()=>RunIdentitySurvey(row,survey.Domain,true));
  repair.Click+=async(s,a)=>{await RepairIdentityGroup58(row.Host,true);Refresh();};
  window.Loaded+=(s,a)=>{refreshSurvey=Refresh;Refresh();};Refresh();return window;
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
