using System.Net;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class HasText40:System.Windows.Data.IValueConverter {public object Convert(object value,Type type,object parameter,System.Globalization.CultureInfo culture)=>!string.IsNullOrWhiteSpace(value?.ToString());public object ConvertBack(object value,Type type,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();}
public static class Identity40 {
 public static string ShortName(string host){host=host.Trim().TrimEnd('.');return IPAddress.TryParse(host,out _)?host:host.Split('.')[0];}
 public static string EndpointKey(TargetRecord target){return target.Host.ToUpperInvariant()+"|"+ConnectionStore.Address(target)+"|"+ConnectionStore.Identity(target);}
 public static string ConfiguredAddress(TargetRecord target){string value=ConnectionStore.Address(target);return IPAddress.TryParse(value,out var ip)?ip.ToString():IPAddress.TryParse(target.Host,out ip)?ip.ToString():"";}
}
public sealed record RebootView40(bool? Required,bool Fresh,string Stamp,string[] Sources,string Limitation){
 public string Title=>!Fresh?"Необходимость перезагрузки не подтверждена · данные устарели":Required==true?"Требуется перезагрузка":Required==false?"Перезагрузка не требуется":"Необходимость перезагрузки не уточнена";
 public string Details=>(Sources.Length>0?"Подтвердили источники: "+Sources.Length+"\n"+string.Join("; ",Sources)+"\n":"")+"Проверено: "+Display36.Date(Stamp,true)+(Limitation.Length>0?"\n"+Limitation:"");
 public string Tone=>Fresh&&Required==false?"Success":"Warning";
}
public partial class MainWindow {
 async void OpenRawEvents40()=>await Guard(async()=>{var row=Current();if(eventWrites39.TryGetValue(row.Host,out var pending))await pending;var folder=EventFolder31(row.Host);if(!System.IO.Directory.Exists(folder)){Notify("Сохранённых событий этого клиента пока нет");return;}System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder){UseShellExecute=true});});
 // One bounded freshness policy for the card, header, tree and read-only dialog.
 static readonly TimeSpan RebootFresh40=TimeSpan.FromMinutes(5);
 readonly HashSet<string> rebootProbeFailures40=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> rebootChecks40=new(StringComparer.OrdinalIgnoreCase);
 void RefreshAddress40(ComputerRow row){
  string address=Identity40.ConfiguredAddress(row.Target),hint=address.Length>0?"Заданный адрес подключения; доступность проверяется отдельно":"";
  if(rebootSnapshots.TryGetValue(row.Host,out var snapshot)&&snapshot.Address40.Length>0&&snapshot.AddressKey40==Identity40.EndpointKey(row.Target)){
   address=snapshot.Address40;hint="Адрес TCP-подключения WinRM\nПолучен: "+Display36.Date(snapshot.AddressReceived40,true)+"\nСохранённый результат проверки; при смене VPN-адреса обновится после следующего подключения.";
  }
  row.SetAddress40(address,hint);
 }
 static string MaintenanceBadge40(string phase,string detail)=>phase switch {
  "После перезагрузки: ожидание готовности"=>"Ждёт готовности",
  "Ожидается перезагрузка"=>"Ждёт перезагрузки",
  "Флаги перезагрузки сняты"=>"Перезагрузка не требуется",
  _=>phase.Contains("связ",StringComparison.OrdinalIgnoreCase)||detail.Contains("Связь",StringComparison.OrdinalIgnoreCase)?"Ждёт связи":"Обслуживание"
 };
 static RebootView40 DescribeReboot40(RebootSnapshot? proof,string boot,DateTimeOffset now){
  if(proof==null)return new(null,false,"",Array.Empty<string>(),"Снимок клиента ещё не получен");
  bool afterBoot=!DateTimeOffset.TryParse(boot,out var b)||DateTimeOffset.TryParse(proof.CollectedUtc,out var c)&&c>=b;
  bool fresh=DateTimeOffset.TryParse(proof.CollectedUtc,out var at)&&now>=at.AddMinutes(-1)&&now-at<=RebootFresh40&&afterBoot;
  var sources=proof.Sources.Where(x=>!x.StartsWith("PendingFileRenameOperations",StringComparison.OrdinalIgnoreCase)).Select(ShortRebootSource).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  bool? required=QueueOnly32(proof.Sources)?null:proof.Required;
  // A false value with a failed source check is not a complete negative result.
  if(proof.Error.Length>0&&required==false)required=null;
  string limit=proof.Error.Length>0?"Проверка неполная; подробности в журнале":!afterBoot?"Снимок получен до последней подтверждённой загрузки Windows":"";
  return new(required,fresh,proof.CollectedUtc,required==true?sources:Array.Empty<string>(),limit);
 }
 RebootView40 RebootViewFor40(ComputerRow row){
  rebootSnapshots.TryGetValue(row.Host,out var current);RebootSnapshot? latest=current;
  foreach(var t in HostJobs39(row.Host).Select(x=>x.Target).Where(CurrentIdentity)){
   var p=t.State?.RebootEvidence;if(p==null)continue;
   if(!DateTimeOffset.TryParse(p.CollectedUtc,out var stamp)||DateTimeOffset.TryParse(latest?.CollectedUtc,out var existing)&&existing>=stamp)continue;
   latest=new(){Required=p.Required,CollectedUtc=p.CollectedUtc,Sources=p.Sources.ToList(),Error=p.Error};
  }
  var view=DescribeReboot40(latest,current?.BootUtc??"",DateTimeOffset.UtcNow);
  if(reportCache.TryGetValue(row.Host,out var cache)&&CacheMatches(cache)&&cache.Found&&cache.Report.InstalledPendingRebootCount>0&&DateTimeOffset.TryParse(cache.Report.LastReportedUtc,out var report)&&(!DateTimeOffset.TryParse(latest?.CollectedUtc,out var checkedAt)||report>checkedAt)&&(!DateTimeOffset.TryParse(current?.BootUtc,out var boot)||report>=boot))return new(null,false,cache.Report.LastReportedUtc,Array.Empty<string>(),"Требование сохранено в отчёте WSUS. Нужна проверка флагов на клиенте.");
  if(rebootProbeFailures40.Contains(row.Host))return view with {Fresh=false,Limitation="Последняя проверка не удалась; сохранённый снимок не подтверждает текущее состояние. Повторите проверку; подробности в журнале."};
  return view;
 }
 void RefreshRebootCard40(ComputerRow row){
  var proof=RebootViewFor40(row);bool busy=rebootChecks40.Contains(row.Host);RebootCard40.Visibility=proof.Stamp.Length>0?Visibility.Visible:Visibility.Collapsed;
  RebootTitle40.Text=proof.Title;RebootTitle40.Foreground=Theme.Tone(proof.Tone);RebootGlyph40.Kind=proof.Fresh&&proof.Required==false?"Success":"Reboot";RebootGlyph40.Tint=Theme.Tone(proof.Tone);RebootDetails40.Text=proof.Details;
  RebootCheck40.Visibility=!proof.Fresh||proof.Required==null||proof.Limitation.Length>0?Visibility.Visible:Visibility.Collapsed;RebootCheck40.IsEnabled=!busy;RebootCheck40.Content=busy?"Проверяем…":"Проверить сейчас";
 }
 async Task CheckRebootNow40(ComputerRow row){
  if(!rebootChecks40.Add(row.Host))return;
  try{RefreshRebootCard40(row);var result=await ReadClientStatus(row,force:true);if(result==null)rebootProbeFailures40.Add(row.Host);RefreshOutcomes();if(IsCurrent(row))ShowCurrent();}
  finally{rebootChecks40.Remove(row.Host);if(IsCurrent(row))RefreshRebootCard40(row);}
 }
 async void RebootCheck40_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{if(ComputerGrid.SelectedItem is ComputerRow row)await CheckRebootNow40(row);});
 FrameworkElement RebootDialogEvidence40(ComputerRow row){
  var panel=new StackPanel{Margin=new(0,8,0,10)};var title=new TextBlock{FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap};var detail=new TextBlock{TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new(0,3,0,4)};var check=new Button{Content="Проверить сейчас",HorizontalAlignment=HorizontalAlignment.Left};check.SetResourceReference(StyleProperty,"GhostButton");
  void Refresh(){var proof=RebootViewFor40(row);title.Text=proof.Title;title.Foreground=Theme.Tone(proof.Tone);detail.Text=proof.Details;}
  check.Click+=async(s,e)=>{check.IsEnabled=false;check.Content="Проверяем…";try{await CheckRebootNow40(row);}finally{check.IsEnabled=true;check.Content="Проверить сейчас";Refresh();}};
  panel.Children.Add(title);panel.Children.Add(detail);panel.Children.Add(check);Refresh();var border=new Border{Child=panel,CornerRadius=new(5),Padding=new(8,0,8,0),BorderThickness=new(1)};border.SetResourceReference(Border.BackgroundProperty,"RebootSurface40");border.SetResourceReference(Border.BorderBrushProperty,"Outline");return border;
 }
 // The command result and the server's evidence answer different questions.
 (string Text,string Tone) WsusConcern40(ComputerRow row){
  if(!reportCache.TryGetValue(row.Host,out var cache)||!CacheMatches(cache)||!cache.Found)return ("","");
  var report=cache.Report;string at=" · отчёт "+Display36.Date(report.LastReportedUtc,true);
  if(report.FailedCount>0)return ("Ошибок по отчёту WSUS: "+report.FailedCount+at,"Error");
  if(report.LastSyncResult is "Failed" or "FailedWithErrors")return ("Последний поиск WSUS завершился с ошибкой · "+Display36.Date(report.LastSyncUtc,true),"Error");
  if(ReportPresentation35.Tone(report.LastReportedUtc,DateTimeOffset.UtcNow,ReportPresentation35.FreshHours)!="Success")return ("Отчёт WSUS устарел · получение нового не подтверждено"+at,"Warning");
  var execution=ExecutionRow(row,true).Target.State;
  if(execution?.ReportRequested==true&&!report.Fresh(execution.Started))return ("Получение нового отчёта WSUS не подтверждено"+at,"Warning");
  if(overviewErrors.ContainsKey(row.Host))return ("Не удалось обновить данные WSUS · показан сохранённый отчёт"+at,"Warning");
  if(report.NeededCount>0)return ("К установке: "+(report.RequiredComplete?"":"≥ ")+report.NeededCount+" — по отчёту WSUS",report.RequiredComplete?"Info":"Warning");
  return ("","");
 }
 void ApplyStatus40(ComputerRow row,bool active){
  RefreshRebootCard40(row);if(MainTabs.SelectedItem!=WsusTab)return;
  var (text,tone)=WsusConcern40(row);if(text.Length==0)return;
  if(active){if(tone=="Info")return;OperationDetail38.Text+=(OperationDetail38.Text.Length>0?"\n":"")+text;OperationDetail38.Visibility=Visibility.Visible;return;}
  var previous=CurrentStage.Text;var previousDetail40=OperationDetail38.Text;CurrentStage.Text=text;CurrentStage.Foreground=Theme.Tone(tone);OperationGlyph38.Kind=tone=="Error"?"Error":tone=="Info"?"Info":"Warning";OperationGlyph38.Tint=Theme.Tone(tone);
  OperationDetail38.Text="Результат команды: "+previous+"\nДалее: "+(tone=="Error"?"открыть сохранённую причину в отчёте WSUS; после исправления проверить новый отчёт.":tone=="Info"?"установить одобренные применимые обновления в согласованное время.":"проверить доставку отчёта клиента и время получения на сервере.");if(previousDetail40.Length>0)OperationDetail38.Text+="\n"+previousDetail40;OperationDetail38.Foreground=Theme.Brush("TextMuted");OperationDetail38.Visibility=Visibility.Visible;
 }
 string WsusSummary40(ComputerRow row,string text){
  var concern=WsusConcern40(row);if(concern.Text.Length==0)return text;
  var sections=LogViewer.Sections(ResultPresentation36.Format(text));var main=sections.FindIndex(x=>x.Title=="Главное");
  string next="Далее: "+(concern.Tone=="Error"?"проверить причину в отчёте WSUS и подтвердить новый отчёт после исправления.":concern.Tone=="Info"?"установить одобренные применимые обновления в согласованное время.":"проверить доставку отчёта; повторное чтение старого отчёта не подтверждает его обновление.");
  if(main>=0){var body=sections[main].Body;sections[main]=("Главное",concern.Text+"\n"+next+"\nРезультат отдельной операции:\n"+body);}
  else sections.Insert(0,("Главное",concern.Text+"\n"+next));
  return string.Join("\n\n",sections.Select(s=>"## "+s.Title+"\n"+s.Body));
 }
}
