using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;

namespace DomainConsole;

public sealed partial class RemoteService {
 public static string BitsResumeScript187(string input)=>Resource42("bits-resume187.ps1")
  .Replace("@@REQUEST@@",PowerShellBridge.Literal(input));

 public static string BitsSystemScript187(string input)=>BoundedSystemTask59(BitsResumeScript187(input),"BitsControl187");

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

 bool CanResumeBits187(TargetRecord endpoint,BackgroundDownload download,LiveUpdateActivity live,int minutes,DateTimeOffset now){
  if(!DateTimeOffset.TryParse(live.CollectedUtc,out var at)||at>now||now-at>=TimeSpan.FromSeconds(90)
    ||live.InstallerBusy||live.InstallationEvidence.Length!=0||live.Limitation.Length!=0||!WsusBits187(download,live)
    ||!Guid.TryParse(download.Id,out var id)||id==Guid.Empty)return false;
  if(download.State=="Suspended")return true;
  if(download.State!="Queued"||!bits187.TryGetValue(BitsKey187(endpoint.Host,id),out var sample))return false;
  return sample.Endpoint==Identity40.EndpointKey(endpoint)&&sample.Seen==at&&sample.Bytes==download.Received
   &&sample.Samples>=3&&at-sample.Changed>=TimeSpan.FromMinutes(minutes);
 }

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
  try{
   if(!await EnsureEndpointCredentials(new(){row}))return;
   endpoint=ConnectionStore.Capture(row.Target);
   var live=await remote.ReadManualUpdateActivity(endpoint);
   if(!CurrentEndpoint(endpoint))return;
   backgroundUpdates[row.Host]=live;
   RememberBits(row.Host,endpoint,live);
   var minimum=Math.Clamp(Store.Read("download-settings.json",new DownloadSettings()).NoMovementMinutes,10,180);
   var candidates=live.Downloads.Where(d=>CanResumeBits187(endpoint,d,live,minimum,DateTimeOffset.UtcNow)).ToList();
   if(candidates.Count==0){
    Notify(row.Name+" · доступные задания обновлены. Для задания в очереди нужен подтверждённый застой без установки.");
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
   if(dialog.ShowDialog()!=true||list.SelectedItem is not BackgroundDownload download||
      !CurrentEndpoint(endpoint)||!CanResumeBits187(endpoint,download,live,minimum,DateTimeOffset.UtcNow))return;
   var proof=ExecutionRow(row,true).Target.State?.ManagedBits187;
   await ApplyBits187(row,endpoint,download,live,proof?.Id==download.Id?proof:null,"",false);
  }catch{ForgetBits(row.Host);throw;}
  finally{bitsPending187.Remove(row.Host);if(IsCurrent(row))UpdateWsusStage();}
 });

 async Task ApplyBits187(ComputerRow row,TargetRecord endpoint,BackgroundDownload download,LiveUpdateActivity live,
  ManagedBitsTransfer? proof,string operation,bool automatic){
  if(automatic&&!bitsPending187.Add(row.Host))return;
  var key=Identity40.EndpointKey(endpoint);
  try{
   bitsReceipts187[row.Host]=new(download.Id,key,download.Received,"Передаём запрос на продолжение скачивания…",false);
   if(IsCurrent(row))UpdateWsusStage();
   var json=await remote.ResumeBits187(endpoint,download,live,proof,operation,automatic);
   if(!CurrentEndpoint(endpoint))return;
   using var result=JsonDocument.Parse(json);
   var payload=result.RootElement;
   var text=payload.GetProperty("Message").GetString()??"Результат не получен.";
   bitsReceipts187[row.Host]=new(download.Id,key,download.Received,text,payload.GetProperty("AwaitGrowth").GetBoolean());
   Event31(row.Host,"bits-resume",Guid.NewGuid().ToString("N"),"Продолжение скачивания BITS","Info",text,scope:download.Id);
   if(!automatic)Notify(row.Name+" · "+text);
  }catch(Exception error){
   if(!CurrentEndpoint(endpoint))return;
   ForgetBits(row.Host);
   var text="Исход запроса не подтверждён. "+ShortConnectionError(error.Message);
   bitsReceipts187[row.Host]=new(download.Id,key,download.Received,text,false);
   Event31(row.Host,"bits-resume",Guid.NewGuid().ToString("N"),"Продолжение скачивания BITS","Warning",text,scope:download.Id);
   if(!automatic)Notify(row.Name+" · "+text);
  }finally{
   if(automatic)bitsPending187.Remove(row.Host);
   if(IsCurrent(row))UpdateWsusStage();
  }
 }
}
