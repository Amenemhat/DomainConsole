using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;

namespace DomainConsole;

public sealed partial class RemoteService {
 public static string BitsResumeScript187(string input)=>Resource42("bits-resume187.ps1")
  .Replace("@@REQUEST@@",PowerShellBridge.Literal(input));

 public static string BitsSystemScript187(string input)=>BoundedSystemTask59(BitsResumeScript187(input),"BitsControl187");

 public static string BitsActivitySystemScript187()=>BoundedSystemTask59(LiveUpdateScript,"BitsControl187");

 public async Task<LiveUpdateActivity> ReadBitsActivity187(TargetRecord target){
  using var lane=PollingControl43.Manual56("control");
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(65));
  if(Local(target))RequireLocalAdmin();
  var json=await PowerShellBridge.Execute(Invoke(target,BitsActivitySystemScript187()),timeout.Token);
  return ParseData34<LiveUpdateActivity>(json,"проверка BITS и установщика Windows Update");
 }

 public async Task<string> ResumeBits187(TargetRecord target,BackgroundDownload download,LiveUpdateActivity live,
  ManagedBitsTransfer? proof,string operation,bool automatic) {
  using var lane=automatic?null:PollingControl43.Manual56("control");
  using var read=automatic?PollingControl43.Read():null;
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(65));
  if(Local(target))RequireLocalAdmin();
  var input=JsonSerializer.Serialize(new {
   download.Id,download.State,download.Received,download.Total,download.RemoteName,download.LocalName,
   download.ModifiedUtc,live.PolicySource,Automatic=automatic,Proof=proof,Operation=operation
  });
  return await PowerShellBridge.Execute(Invoke(target,BitsSystemScript187(input)),timeout.Token);
 }
}

public partial class MainWindow {
 sealed record BitsSample187(string Endpoint,string State,string Url,string File,long Bytes,DateTimeOffset Seen,DateTimeOffset Changed,int Samples,double Speed);
 sealed record BitsReceipt187(string Id,string Endpoint,long Bytes,string Text,bool Accepted);
 readonly Dictionary<(string Host,Guid Id),BitsSample187> bits187=new();
 readonly Dictionary<string,BitsReceipt187> bitsReceipts187=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Guid> bitsAutoRequests187=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> bitsPending187=new(StringComparer.OrdinalIgnoreCase);

 static (string,Guid) BitsKey187(string host,Guid id)=>(host.ToUpperInvariant(),id);
 void ForgetBits(string host){
  foreach(var key in bits187.Keys.Where(k=>k.Host==host.ToUpperInvariant()).ToArray())bits187.Remove(key);
 }
 void RememberBits(string host,TargetRecord endpoint,LiveUpdateActivity live,DateTimeOffset? received=null){
  var now=received??DateTimeOffset.UtcNow;
  var identity=Identity40.EndpointKey(endpoint);
  if(!DateTimeOffset.TryParse(live.CollectedUtc,out var at)||at>now||now-at>TimeSpan.FromSeconds(90)||live.Limitation.Length!=0){
   ForgetBits(host);
   return;
  }
  var present=new HashSet<Guid>();
  foreach(var download in live.Downloads){
   if(!Guid.TryParse(download.Id,out var id)||!present.Add(id))continue;
   var key=BitsKey187(host,id);
   var old=bits187.GetValueOrDefault(key);
   if(old!=null&&old.Endpoint==identity&&at<=old.Seen)continue;
   bool follows=old!=null&&old.Endpoint==identity&&old.State==download.State&&old.Url==download.RemoteName
    &&old.File==download.LocalName&&at-old.Seen<=TimeSpan.FromSeconds(90)&&download.Received>=old.Bytes;
   var changed=follows&&download.Received==old!.Bytes?old.Changed:at;
   var speed=follows?(download.Received-old!.Bytes)/Math.Max(1,(at-old.Seen).TotalSeconds):0;
   bits187[key]=new(identity,download.State,download.RemoteName,download.LocalName,download.Received,at,changed,follows?old!.Samples+1:1,speed);
  }
  foreach(var key in bits187.Keys.Where(k=>k.Host==host.ToUpperInvariant()&&!present.Contains(k.Id)).ToArray())bits187.Remove(key);
  if(!bitsReceipts187.TryGetValue(host,out var receipt)||receipt.Endpoint!=identity||!receipt.Accepted)return;
  var same=live.Downloads.FirstOrDefault(d=>d.Id.Equals(receipt.Id,StringComparison.OrdinalIgnoreCase));
  if(same!=null&&same.Received>receipt.Bytes)
   bitsReceipts187[host]=receipt with {Text="Передача продолжилась · получено "+(same.Received-receipt.Bytes).ToString("N0")+" новых байт.",Accepted=false};
  else if(same==null)
   bitsReceipts187[host]=receipt with {Text=receipt.Text+" Задание больше не видно в BITS. Результат обновления уточняется в Windows Update.",Accepted=false};
 }

 static bool WsusBits187(BackgroundDownload download,LiveUpdateActivity live)=>
  live.UseWsus&&download.SystemOwned&&download.Files==1&&download.LocalName.Length>0&&download.Received>=0
  &&download.Total>download.Received&&Uri.TryCreate(download.RemoteName,UriKind.Absolute,out var file)
  &&Uri.TryCreate(live.PolicySource,UriKind.Absolute,out var wsus)
  &&file.Scheme is "http" or "https"&&file.Scheme==wsus.Scheme&&file.Host.Equals(wsus.Host,StringComparison.OrdinalIgnoreCase)
  &&file.Port==wsus.Port&&file.AbsolutePath.StartsWith("/Content/",StringComparison.OrdinalIgnoreCase)
  &&live.Downloads.Count(d=>Uri.TryCreate(d.RemoteName,UriKind.Absolute,out var u)&&u.Equals(file)
     ||d.LocalName.Equals(download.LocalName,StringComparison.OrdinalIgnoreCase))==1;

 static bool FreshBitsActivity187(LiveUpdateActivity live,DateTimeOffset now,out DateTimeOffset at)=>
  DateTimeOffset.TryParse(live.CollectedUtc,out at)&&at<=now&&now-at<TimeSpan.FromSeconds(90);

 bool HasBitsActivity187(TargetRecord endpoint,LiveUpdateActivity live,DateTimeOffset now)=>
  live.Limitation.Length==0&&FreshBitsActivity187(live,now,out var at)&&
  live.Downloads.Any(d=>Guid.TryParse(d.Id,out var id)&&bits187.TryGetValue(BitsKey187(endpoint.Host,id),out var sample)
    &&sample.Endpoint==Identity40.EndpointKey(endpoint)&&sample.Seen==at);

 string BitsResumeBlock187(TargetRecord endpoint,BackgroundDownload download,LiveUpdateActivity live,int minutes,DateTimeOffset now){
  if(live.Limitation.Length>0)return "Не удалось проверить BITS и установщик Windows Update. "+live.Limitation.Trim(' ',';');
  if(!FreshBitsActivity187(live,now,out var at))return "Снимок BITS устарел. Нажмите «Продолжить скачивание» ещё раз для проверки.";
  if(live.InstallerBusy||live.InstallationEvidence.Length>0)return "Обнаружена установка или обслуживание Windows. Дождитесь завершения. "+live.InstallationEvidence;
  if(!WsusBits187(download,live)||!Guid.TryParse(download.Id,out var id)||id==Guid.Empty)
   return "Принадлежность передачи Windows Update и источнику WSUS не подтверждена либо найден дубль.";
  if(download.State=="Suspended")return "";
  if(download.State!="Queued")return "Состояние передачи изменилось · BITS "+download.State+".";
  var sample=bits187.GetValueOrDefault(BitsKey187(endpoint.Host,id));
  if(sample==null||sample.Endpoint!=Identity40.EndpointKey(endpoint)||sample.Seen!=at||sample.Bytes!=download.Received)
   return "Для передачи в очереди пока нет последовательных замеров по тому же GUID.";
  if(sample.Samples>=3&&at-sample.Changed>=TimeSpan.FromMinutes(minutes))return "";
  return "BITS в очереди · без роста байтов подтверждено "+Age(at-sample.Changed)+" из "+minutes+
   " мин. Передача сохранена. Замеры продолжатся в обычном опросе.";
 }
 bool CanResumeBits187(TargetRecord endpoint,BackgroundDownload download,LiveUpdateActivity live,int minutes,DateTimeOffset now)=>
  BitsResumeBlock187(endpoint,download,live,minutes,now).Length==0;

 async Task MaybeRecoverDownload(ComputerRow row,LiveUpdateActivity live){
  if(closing||!AutoServiceEnabled||!ConnectionFresh(row.Host)||bitsPending187.Contains(row.Host))return;
  var settings=Store.Read("download-settings.json",new DownloadSettings());
  if(!settings.AutomaticRecovery)return;
  var current=ExecutionRow(row,true).Target;
  var proof=current.State?.ManagedBits187;
  var now=DateTimeOffset.UtcNow;
  if(current.Status!="Running"||current.State?.ManagedSource54!=true||proof==null||
     !Guid.TryParse(proof.Id,out var id)||!Guid.TryParse(proof.UpdateId,out _)||proof.Revision<1
     ||!DateTimeOffset.TryParse(proof.ObservedUtc,out var at)||at>now||now-at>TimeSpan.FromSeconds(90)
     ||bitsAutoRequests187.GetValueOrDefault(row.Host)==id)return;
  var endpoint=ConnectionStore.Capture(row.Target);
  var download=live.Downloads.FirstOrDefault(d=>Guid.TryParse(d.Id,out var observed)&&observed==id);
  if(download==null||download.State!="Queued"||proof.Url!=download.RemoteName||
     !CanResumeBits187(endpoint,download,live,Math.Clamp(settings.NoMovementMinutes,10,180),now))return;
  bitsAutoRequests187[row.Host]=id;
  await ApplyBits187(row,endpoint,download,live,proof,current.State!.Id,true);
 }

 async void ContinueBits187_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  var row=Current();
  if(!bitsPending187.Add(row.Host))return;
  var endpoint=ConnectionStore.Capture(row.Target);
  var live=backgroundUpdates.GetValueOrDefault(row.Host);
  var selectedId=live?.Downloads.FirstOrDefault(d=>d.State is "Suspended" or "Queued")?.Id??"";
  var pending=new BitsReceipt187(selectedId,Identity40.EndpointKey(endpoint),0,"Проверяем выбранную загрузку BITS…",false);
  bitsReceipts187[row.Host]=pending;
  if(IsCurrent(row))UpdateWsusStage();
  try{
   if(!await EnsureEndpointCredentials(new(){row})){
    RecordBitsReceipt187(row,endpoint,selectedId,0,"Продолжение не запущено · не подтверждено подключение к клиенту.","Warning");
    return;
   }
   endpoint=ConnectionStore.Capture(row.Target);
   if(live==null||!HasBitsActivity187(endpoint,live,DateTimeOffset.UtcNow)){
    live=await remote.ReadBitsActivity187(endpoint);
    if(!CurrentEndpoint(endpoint)){
     RecordBitsReceipt187(row,endpoint,selectedId,0,"Подключение изменилось. Продолжение скачивания не отправлено.","Warning");
     return;
    }
    // Preserve the previous trusted snapshot when a manual read reports a partial result.
    if(live.Limitation.Length==0){
     backgroundUpdates[row.Host]=live;
     RememberBits(row.Host,endpoint,live);
    }
   }
   var minimum=Math.Clamp(Store.Read("download-settings.json",new DownloadSettings()).NoMovementMinutes,10,180);
   var now=DateTimeOffset.UtcNow;
   var offered=live.Downloads.Where(d=>d.State is "Suspended" or "Queued").ToArray();
   var candidates=offered.Where(d=>CanResumeBits187(endpoint,d,live,minimum,now)).ToList();
   if(candidates.Count==0){
    var first=offered.FirstOrDefault();
    var reason=live.Limitation.Length>0?"Не удалось проверить BITS и установщик Windows Update. "+live.Limitation.Trim(' ',';'):
     first==null?"Подходящая передача BITS больше не видна либо её состояние изменилось.":
     BitsResumeBlock187(endpoint,first,live,minimum,now);
    RecordBitsReceipt187(row,endpoint,first?.Id??selectedId,first?.Received??0,"Продолжение не запущено · "+reason,"Warning");
    return;
   }
   var panel=new StackPanel{Margin=new(16)};
   panel.Children.Add(new TextBlock{Text="Выберите загрузку Windows Update. Уже полученные данные сохранятся.",TextWrapping=TextWrapping.Wrap});
   var list=new ComboBox{ItemsSource=candidates,DisplayMemberPath="Label",SelectedIndex=0,Margin=new(0,8,0,8)};
   panel.Children.Add(list);
   var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};
   var ok=new Button{Content="Продолжить",IsDefault=true};
   var cancel=new Button{Content="Отмена",IsCancel=true};
   actions.Children.Add(cancel);actions.Children.Add(ok);panel.Children.Add(actions);
   var dialog=new Window{Owner=this,Title="Продолжить скачивание · "+row.Name,Width=560,
    SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};
   ok.Click+=(s,args)=>dialog.DialogResult=true;
   if(dialog.ShowDialog()!=true||list.SelectedItem is not BackgroundDownload download)return;
   var latest=backgroundUpdates.GetValueOrDefault(row.Host)??live;
   var matching=latest.Downloads.FirstOrDefault(d=>d.Id==download.Id);
   string blocked=!CurrentEndpoint(endpoint)?"Подключение изменилось.":
    matching==null?"Выбранный GUID больше не виден в BITS.":
    matching.RemoteName!=download.RemoteName||matching.LocalName!=download.LocalName||
     matching.Received!=download.Received||matching.Total!=download.Total||matching.State!=download.State||
     latest.PolicySource!=live.PolicySource?"Источник, состояние или байты передачи изменились. Проверьте загрузку повторно.":
    BitsResumeBlock187(endpoint,matching,latest,minimum,DateTimeOffset.UtcNow);
   if(blocked.Length>0||matching==null){
    RecordBitsReceipt187(row,endpoint,download.Id,download.Received,"Продолжение не запущено · "+blocked,"Warning");
    return;
   }
   var proof=ExecutionRow(row,true).Target.State?.ManagedBits187;
   await ApplyBits187(row,endpoint,matching,latest,proof?.Id==matching.Id?proof:null,"",false);
  }catch(Exception error){
   RecordBitsReceipt187(row,endpoint,selectedId,0,"Продолжение не запущено · "+ShortConnectionError(error.Message),"Warning");
  }
  finally{
   bitsPending187.Remove(row.Host);
   if(ReferenceEquals(bitsReceipts187.GetValueOrDefault(row.Host),pending))bitsReceipts187.Remove(row.Host);
   if(IsCurrent(row))UpdateWsusStage();
  }
 });

 void RecordBitsReceipt187(ComputerRow row,TargetRecord endpoint,string id,long bytes,string text,string kind,bool awaitGrowth=false,bool notify=true){
  if(CurrentEndpoint(endpoint))
   bitsReceipts187[row.Host]=new(id,Identity40.EndpointKey(endpoint),bytes,text,awaitGrowth);
  Event31(row.Host,"bits-resume",Guid.NewGuid().ToString("N"),"Продолжение скачивания BITS",kind,text,scope:id);
  if(notify)Notify(row.Name+" · "+text);
 }

 async Task ApplyBits187(ComputerRow row,TargetRecord endpoint,BackgroundDownload download,LiveUpdateActivity live,
  ManagedBitsTransfer? proof,string operation,bool automatic){
  if(automatic&&!bitsPending187.Add(row.Host))return;
  var key=Identity40.EndpointKey(endpoint);
  try{
   bitsReceipts187[row.Host]=new(download.Id,key,download.Received,"Передаём запрос на продолжение скачивания…",false);
   if(IsCurrent(row))UpdateWsusStage();
   var json=await remote.ResumeBits187(endpoint,download,live,proof,operation,automatic);
   if(!CurrentEndpoint(endpoint)){
    RecordBitsReceipt187(row,endpoint,download.Id,download.Received,
     "Подключение изменилось после отправки запроса. Исход продолжения скачивания не подтверждён.","Warning",notify:!automatic);
    return;
   }
   using var result=JsonDocument.Parse(json);
   var payload=result.RootElement;
   var text=payload.GetProperty("Message").GetString()??"Результат не получен.";
   RecordBitsReceipt187(row,endpoint,download.Id,download.Received,text,"Info",payload.GetProperty("AwaitGrowth").GetBoolean(),!automatic);
  }catch(Exception error){
   if(CurrentEndpoint(endpoint))ForgetBits(row.Host);
   var text="Исход запроса не подтверждён. "+ShortConnectionError(error.Message);
   RecordBitsReceipt187(row,endpoint,download.Id,download.Received,text,"Warning",notify:!automatic);
  }finally{
   if(automatic)bitsPending187.Remove(row.Host);
   if(IsCurrent(row))UpdateWsusStage();
  }
 }
}
