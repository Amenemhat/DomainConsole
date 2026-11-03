using System.Windows;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed partial class RemoteService {
 public async Task<string> Applicability54(WsusSettings settings,IEnumerable<WsusRequiredUpdate37> updates){
  var selected=updates.Where(x=>Guid.TryParse(x.Id,out _)&&x.Revision>0).GroupBy(x=>x.Id+"|"+x.Revision).Select(g=>g.First()).Take(10).ToList();if(selected.Count==0)throw new InvalidOperationException("В отчёте нет идентификаторов обновлений для сверки.");
  var code="[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');$s=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,"+(settings.Ssl?"$true":"$false")+","+settings.Port+");$ids=@("+string.Join(",",selected.Select(x=>"@{Id="+PowerShellBridge.Literal(x.Id)+";Revision="+x.Revision+"}"))+");"+ApplicabilityScript54;
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));return await PowerShellBridge.Execute(Invoke(settings.Host,code),timeout.Token);
 }
 public const string ApplicabilityScript54="""
$groups=@{};foreach($g in $s.GetComputerTargetGroups()){$groups[[string]$g.Id]=$g.Name}
function Show-Approval54($u){$a=@($u.GetUpdateApprovals());if(!$a.Count){'Одобрений нет'};foreach($approval in $a){'Группа: '+$groups[[string]$approval.ComputerTargetGroupId]+'; действие: '+$approval.Action+'; дата: '+$approval.CreationDate+'; срок: '+$approval.Deadline}}
foreach($item in $ids){$id=$item.Id
 try{
  $revision=New-Object Microsoft.UpdateServices.Administration.UpdateRevisionId ([guid]$id),([int]$item.Revision)
  $u=$s.GetUpdate($revision)
  $u|Select-Object Title,@{n='UpdateID';e={$_.Id.UpdateId}},@{n='Revision';e={$_.Id.RevisionNumber}},IsLatestRevision,IsApproved,IsDeclined,IsSuperseded,PublicationState|Format-List|Out-String
  Show-Approval54 $u
  $replacements=@($u.GetRelatedUpdates([Microsoft.UpdateServices.Administration.UpdateRelationship]::UpdatesThatSupersedeThisUpdate))
  'Заменяющих обновлений: '+$replacements.Count+'; показано не более 10'
  foreach($replacement in @($replacements|Select-Object -First 10)){$replacement|Select-Object Title,@{n='UpdateID';e={$_.Id.UpdateId}},@{n='Revision';e={$_.Id.RevisionNumber}},IsApproved,IsDeclined,IsSuperseded|Format-List|Out-String;Show-Approval54 $replacement}
 }catch{'Не удалось прочитать '+$id+': '+$_.Exception.Message}
}
'Одобрение и замена не доказывают применимость на конкретном клиенте. Политики, одобрения и состояние обновлений не менялись.'
""";
}
public partial class MainWindow {
 readonly HashSet<string> applicabilityReading54=new();
 async void Applicability54_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  var row=Current();if(!applicabilityReading54.Add(row.Host))return;
  try{
   if(!reportCache.TryGetValue(row.Host,out var cache)||!cache.Found)throw new InvalidOperationException("Сначала прочитайте отчёт WSUS.");
   var settings=ReadWsus();var endpoint=ConnectionStore.Capture(row.Target);var text=await remote.Applicability54(settings,cache.Report.RequiredUpdates);
   if(!CurrentEndpoint(endpoint)||OverviewServer!=settings.Host+":"+settings.Port)return;
   Event31(row.Host,"applicability54",Guid.NewGuid().ToString(),"Прочитаны свойства спорных обновлений WSUS","Info",text);
   if(IsCurrent(row))WsusServerOutput.Text=CurrentReportText41(row)+"\n\n## Сверка применимости · свойства WSUS\n"+text;
  }finally{applicabilityReading54.Remove(row.Host);}
 });
}
