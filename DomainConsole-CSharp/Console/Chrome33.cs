using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
namespace DomainConsole;
public partial class MainWindow {
 void HeaderNavigation33_Click(object sender,RoutedEventArgs e){if(sender is RadioButton button){MainTabs.SelectedItem=(string)button.Tag switch {"History"=>HistoryTab,"Wsus"=>WsusTab,_=>CommandsTab};RefreshHeaderNavigation33();}}
 void RefreshHeaderNavigation33(){CommandsNavigation33.IsChecked=MainTabs.SelectedItem==CommandsTab;HistoryNavigation33.IsChecked=MainTabs.SelectedItem==HistoryTab;WsusNavigation33.IsChecked=MainTabs.SelectedItem==WsusTab;}
 void SetOverviewAction33(bool running){OverviewRefresh.Content=UiIcon32.Create(running?"stop":"refresh");OverviewRefresh.ToolTip=running?"Остановить текущий обзор статусов. Уже отправленные задания продолжаются.":"Обновить статусы WSUS, связь, флаги перезагрузки и аптайм. Поиск и установка не запускаются.";AutomationProperties.SetName(OverviewRefresh,running?"Остановить обзор статусов":"Обновить статусы WSUS");}
}
