using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed partial class RemoteService {
 static string GuardJobControl56(string id,string body){
  var check="$manualJob56=Join-Path (Join-Path (Join-Path $env:ProgramData 'DomainConsoleCSharp') "+PowerShellBridge.Literal(id)+") 'job.json';$ownsManual56=$false;if(Test-Path -LiteralPath $manualJob56){$j56=Get-Content -LiteralPath $manualJob56 -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop;$ownsManual56=$j56.Manual56 -and $j56.OutputChannel -eq 'Commands' -and $j56.ConsoleId37 -eq "+PowerShellBridge.Literal(ConsoleId37)+"};if($ownsManual56){\n"+body+"\n}else{\n"+GuardControlScript37(body)+"\n}";
  return check;
 }
 public static string ControlScript56(bool repair=false)=>Resource42("control56.ps1").Replace("@@REPAIR@@",repair?"$true":"$false");
 public async Task<string> InspectControl56(TargetRecord target,bool repair=false){using var manual=PollingControl43.Manual56("control");using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));return await PowerShellBridge.Execute(Invoke(ConnectionStore.Capture(target),ControlScript56(repair)),timeout.Token);}
}
public partial class MainWindow {
 async void ManualResult56_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{
  CollectorPopup54.IsOpen=false;var row=Current();var active=OperationRow38(row);
  if(active.JobId.Length==0){Notify("Нет выбранного задания. Откройте «Задания и блокировки» для чтения записей клиента.");return;}
  await Poll(active,true);
 });
 void CancelRead56_Click(object sender,RoutedEventArgs e){TransportBudget41.CancelBackground56(ConnectionStore.Capture(Current().Target));Notify("Запрошена отмена фонового подключения. Клиентские команды и установки продолжаются.");}
 async void ManualConnection56_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{CollectorPopup54.IsOpen=false;using var manual=PollingControl43.Manual56("control");await EditConnection(Current(),"");});
 async void ManualJobs56_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{await ShowManualJobs56(Current());});
 async Task<Window> ShowManualJobs56(ComputerRow row,Func<bool,Task<string>>? inspect56=null){
  CollectorPopup54.IsOpen=false;var target=ConnectionStore.Capture(row.Target);
  var window=new Window{Owner=this,Title="Задания и блокировки · "+row.Name,Width=760,Height=540,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new DockPanel{Margin=new(12)};window.Content=panel;
  var buttons=new WrapPanel();DockPanel.SetDock(buttons,Dock.Bottom);panel.Children.Add(buttons);
  var choose=new ComboBox{Margin=new(0,0,0,8)};DockPanel.SetDock(choose,Dock.Top);panel.Children.Add(choose);
  var log=new LogViewer{ShellMode="Report",Text="Чтение записей клиента…"};panel.Children.Add(log);
  var refresh=new Button{Content="Прочитать сейчас"};var release=new Button{Content="Снять оставшиеся резервации"};var stop=new Button{Content="Остановить выбранное"};var force=new Button{Content="Завершить исполнитель…"};
  foreach(var button in new[]{refresh,release,stop,force})buttons.Children.Add(button);
  bool busy=false;
  async Task Read(bool repair=false){using var manual56=PollingControl43.Manual56("control");var raw=await (inspect56!=null?inspect56(repair):remote.InspectControl56(target,repair));if(!window.IsVisible||!CurrentEndpoint(target))return;using var doc=JsonDocument.Parse(raw);var root=doc.RootElement;var previous=choose.SelectedItem as string;choose.Items.Clear();var lines=new List<string>{root.GetProperty("Message").GetString()??""};
   if(root.TryGetProperty("Owner",out var owner))lines.Add("Владелец: "+owner.GetString());
   foreach(var item in root.GetProperty("Rows").EnumerateArray()){string id=item.GetProperty("Id").GetString()!;choose.Items.Add(id);lines.Add("\nЗадание: "+id+"\n"+item.GetProperty("Status").GetString()+" · "+item.GetProperty("Stage").GetString()+"\nПоследний статус: "+Display36.Date(item.GetProperty("Updated").GetString()??"")+"\nДиагностика: "+item.GetProperty("Diagnostics").GetString()+"; восстановление: "+item.GetProperty("Restore").GetString()+"\nЗадач: "+item.GetProperty("Tasks").GetArrayLength()+"; исполнителей: "+item.GetProperty("Processes").GetArrayLength()+"\nМожно снять оставшуюся резервацию: "+item.GetProperty("CanRelease").GetBoolean());}
   if(previous!=null&&choose.Items.Contains(previous))choose.SelectedItem=previous;else if(choose.Items.Count>0)choose.SelectedIndex=0;
   if(root.TryGetProperty("Backup",out var backup)&&backup.GetString()?.Length>0)lines.Add("Резервная копия: "+backup.GetString());log.Text=string.Join("\n",lines);
   if(repair)Event31(row.Host,"orphan-repair56",DateTime.UtcNow.ToString("o"),root.GetProperty("Message").GetString()??"Проверка резерваций","Info",log.Text);
  }
  async Task Run(Func<Task> action){if(busy)return;busy=true;foreach(var b in new[]{refresh,release,stop,force})b.IsEnabled=false;try{await action();}catch(Exception ex){log.Text="Действие не подтверждено\n"+ex.Message;}finally{busy=false;foreach(var b in new[]{refresh,release,stop,force})b.IsEnabled=true;}}
  refresh.Click+=async(s,a)=>await Run(()=>Read());release.Click+=async(s,a)=>await Run(()=>Read(true));
  async Task Cancel(string mode){if(choose.SelectedItem is not string id)return;if(mode=="executor"&&MessageBox.Show(window,"Принудительно завершить исполнитель задания "+id+"? Результат прерванной операции может остаться неизвестным.","Завершение исполнителя",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;var result=await remote.Cancel(target,id,mode);if(result.Status=="Cancelled")CompleteLocalCancellation(row.Host,id,result.Message);await Read();log.Text=result.Message+"\n\n"+log.Text;}
  stop.Click+=async(s,a)=>await Run(()=>Cancel("now"));force.Click+=async(s,a)=>await Run(()=>Cancel("executor"));
  window.Show();await Run(()=>Read());return window;
 }
}
