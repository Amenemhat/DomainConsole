using System.Windows;
using System.Windows.Controls;
namespace DomainConsole;
public partial class MainWindow {
 public static readonly (string Group,string Label,string Hint,string Key)[] ServiceActions36={
  ("Диагностика","Клиент WSUS","Политики, службы, прокси и события Windows Update","client"),
  ("Диагностика","Windows Update · очереди","BITS, политики и очереди Windows Update","wizard"),
  ("Диагностика","Опрос и остатки Probe…","Просмотр завершённых Probe, адресная очистка и возобновление опроса клиента","probe41"),
  ("Диагностика","Память и подкачка","RAM, подкачка, лимит памяти и события нехватки ресурсов","memory"),
  ("Диагностика","Загрузка и установка","Процессы и события параллельно с активной операцией","waiting"),
  ("Диагностика","Доставка отчёта","Запрос отправки отчёта и диагностика клиента/IIS","delivery"),
  ("Восстановление","Отчётность…","Восстановление очереди отчётности с последующей проверкой","report"),
  ("Восстановление","Defender…","Диагностика Defender и выбор вариантов восстановления","defender"),
  ("Восстановление","Перезапуск служб…","Выбор служб и параметров штатного перезапуска","services"),
  ("Восстановление","Зависшие службы…","Выбор подтверждённых процессов через Ctrl/Shift","force"),
  ("Восстановление","Очистка кэша…","Все варианты очистки и удаление старых кэшей","cache")};
 Window ServiceWindow36(Action<string> choose){var window=new Window{Owner=this,Title="Диагностика и восстановление",Width=720,Height=Math.Min(540,SystemParameters.WorkArea.Height-60),MinWidth=540,WindowStartupLocation=WindowStartupLocation.CenterOwner};var grid=new Grid{Margin=new(12)};grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new());window.Content=grid;
  foreach(var group in ServiceActions36.Select(x=>x.Group).Distinct()){var dock=new DockPanel{Margin=new(5)};Grid.SetColumn(dock,group=="Диагностика"?0:1);grid.Children.Add(dock);var caption=new TextBlock{Text=group,FontSize=15,FontWeight=FontWeights.SemiBold,Margin=new(4,0,0,8)};DockPanel.SetDock(caption,Dock.Top);dock.Children.Add(caption);var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new(0,0,6,0)};dock.Children.Add(scroll);var stack=new StackPanel();scroll.Content=stack;foreach(var action in ServiceActions36.Where(x=>x.Group==group)){var content=new StackPanel();content.Children.Add(new TextBlock{Text=action.Label,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});content.Children.Add(new TextBlock{Text=action.Hint,FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new(0,3,0,0)});var button=new Button{Content=content,ToolTip=action.Hint,Tag=action.Key,Padding=new(8,6,8,6),Margin=new(0,0,0,5),HorizontalContentAlignment=HorizontalAlignment.Stretch};button.Click+=(s,e)=>choose(action.Key);stack.Children.Add(button);}}
  return window;
 }
 async void ServiceTools36_Click(object sender,RoutedEventArgs e){string action="";Window? window=null;window=ServiceWindow36(key=>{action=key;window!.DialogResult=true;});if(window.ShowDialog()!=true)return;
  switch(action){case "probe41":ProbeTools41_Click(sender,e);break;case "client":WsusDiagnostics_Click(sender,e);break;case "memory":MemoryDiagnostics_Click(sender,e);break;case "waiting":WaitingDiagnostics_Click(sender,e);break;case "delivery":DeliveryDiagnostics_Click(sender,e);break;case "report":RecoverReport_Click(sender,e);break;case "defender":Defender_Click(sender,e);break;case "services":RestartServices_Click(sender,e);break;case "force":ForceServices_Click(sender,e);break;case "cache":CacheCleanup_Click(sender,e);break;case "wizard":await Guard(async()=>{MicrosoftBox.IsChecked=false;RebootBox.IsChecked=false;await Submit(new(){new(){Name="Диагностика Windows Update · очереди",Kind="PowerShell",Code=UpdateWizardScript}},"WSUS");});break;}
 }
}
