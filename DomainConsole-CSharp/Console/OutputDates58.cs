namespace DomainConsole;
public partial class MainWindow {
 void RefreshOutputDates58(ComputerRow row){
  string Stamp(bool wsus){var target=ExecutionRow(row,wsus).Target;return target.OutputReceivedUtc58.Length>0?"получено "+Display36.Date(target.OutputReceivedUtc58):target.State?.Updated is {Length:>0} at?"данные "+Display36.Date(at):"дата не сохранена";}
  OutputBox.Caption="Вывод · "+Stamp(false);WsusOutput.Caption="Операция · "+Stamp(true);
 }
}
