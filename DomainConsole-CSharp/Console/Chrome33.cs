using System.Windows;
using System.Windows.Automation;
namespace DomainConsole;
public partial class MainWindow {
 void SetOverviewAction33(bool running){OverviewRefresh.Content=UiIcon32.Create(running?"stop":"refresh");OverviewRefresh.ToolTip=running?"Остановить текущий обзор статусов. Уже отправленные задания продолжаются.":"Обновить статусы WSUS, связь, флаги перезагрузки и аптайм. Поиск и установка не запускаются.";AutomationProperties.SetName(OverviewRefresh,running?"Остановить обзор статусов":"Обновить статусы WSUS");}
}
