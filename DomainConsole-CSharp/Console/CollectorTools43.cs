using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed partial class RemoteService {
 public static string RemoveCollectorScript43(string mode="Remove")=>Resource42("remove43.ps1").Replace("@@MODE@@",mode);
 public async Task<string> RemoveCollector43(TargetRecord target,bool enable=false){using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));string result=await PowerShellBridge.Execute("# DC-MANUAL41\n"+Invoke(target,RemoveCollectorScript43(enable?"Enable":"Remove")),deadline.Token);snapshotCache42.TryRemove(TransportBudget41.Key(target),out _);snapshotWait42.TryRemove(TransportBudget41.Key(target),out _);return result;}
}
public partial class MainWindow {
 async void CollectorCleanup43_Click(object sender,RoutedEventArgs e){
  var window=new Window{Owner=this,Title="Клиентский сборщик",Width=760,Height=570,WindowStartupLocation=WindowStartupLocation.CenterOwner};var dock=new DockPanel{Margin=new(14)};window.Content=dock;
  var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);dock.Children.Add(top);
  top.Children.Add(new TextBlock{Text="Удаление сборщика и его данных",FontSize=17,FontWeight=FontWeights.Bold});
  top.Children.Add(new TextBlock{Text="Опрос выбранных клиентов будет запрещён до явного включения. Обновления, пользовательские задания и службы Windows сохраняются. Старые консоли необходимо закрыть.",TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,8)});
  var scope=new ComboBox{ItemsSource=new[]{"Текущий компьютер","Отмеченные компьютеры","Все компьютеры"},SelectedIndex=0,Margin=new(0,0,0,8)};top.Children.Add(scope);
  var actions=new WrapPanel();top.Children.Add(actions);var remove=new Button{Content="Удалить сборщик и данные…"};var enable=new Button{Content="Разрешить установку и опрос…"};var diagnostics=new Button{Content="Диагностика сборщика…"};actions.Children.Add(remove);actions.Children.Add(enable);actions.Children.Add(diagnostics);
  var log=new LogViewer{Text="Выберите область действия. Недоступные клиенты не считаются очищенными; отложенное удаление не создаётся.",ShellMode="Report"};log.CopyRequested+=(s,args)=>Clipboard.SetText(log.VisibleText());dock.Children.Add(log);
  bool busy=false;async Task Run(bool restore){if(busy)return;var rows=(scope.SelectedIndex==2?Computers.ToList():scope.SelectedIndex==1?Computers.Where(r=>r.Selected).ToList():ComputerGrid.SelectedItem is ComputerRow selected?new List<ComputerRow>{selected}:new()).DistinctBy(r=>r.Host,StringComparer.OrdinalIgnoreCase).ToList();if(rows.Count==0){log.Text="Компьютеры не выбраны.";return;}
   string names=string.Join("\n",rows.Take(12).Select(r=>r.Host))+(rows.Count>12?"\n…":"");if(MessageBox.Show(window,(restore?"Разрешить установку сборщика":"Удалить сборщик, его результаты и диагностику")+" на "+rows.Count+" компьютерах?\n"+names+"\nЗадания обновлений не останавливаются.","Клиентский сборщик",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
   busy=true;remove.IsEnabled=enable.IsEnabled=diagnostics.IsEnabled=scope.IsEnabled=false;var report=new StringBuilder();try{foreach(var row in rows){if(!window.IsVisible||closing)break;report.AppendLine("## "+row.Host);log.Text=report+"Выполняется команда очистки/включения…";try{string result=await remote.RemoveCollector43(ConnectionStore.Capture(row.Target),restore);report.AppendLine(result);Event31(row.Host,"collector43",DateTimeOffset.UtcNow.ToString("o"),restore?"Опрос клиента разрешён":"Удаление сборщика", "Info",result);if(restore){ReadSucceeded34(row.Host,"флаги и аптайм");ReadSucceeded34(row.Host,"фоновая активность");}if(!restore){report.AppendLine(ProbeInventoryText41(await remote.ProbeInventory41(row.Target,true)));}}catch(Exception ex){report.AppendLine("Результат не подтверждён: "+ShortConnectionError(ex.Message));}log.Text=report.ToString();await Task.Yield();}
   var folder=Path.Combine(Store.Root,"Diagnostics");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"collector-cleanup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt"),report.ToString());}finally{busy=false;remove.IsEnabled=enable.IsEnabled=diagnostics.IsEnabled=scope.IsEnabled=true;}}
  remove.Click+=async(s,args)=>await Guard(()=>Run(false));enable.Click+=async(s,args)=>await Guard(()=>Run(true));diagnostics.Click+=(s,args)=>ProbeTools41_Click(s!,args);window.ShowDialog();
 }
}
