using System.Windows;
using System.Windows.Controls;
namespace DomainConsole;
public partial class MainWindow {
 void HistoryLimits58_Click(object sender,RoutedEventArgs e){
  var saved=Store.Read("history-limits58.json",new HistoryLimits58());
  var window=new Window{Owner=this,Title="Хранение истории клиента",Width=500,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new StackPanel{Margin=new(20)};window.Content=panel;
  TextBox Field(string label,int value){panel.Children.Add(new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap});var box=new TextBox{Text=value.ToString()};panel.Children.Add(box);return box;}
  var ordinary=Field("Обычных событий на клиента (дни задаются в настройках связи)",saved.OrdinaryCount);
  var days=Field("Значимые события: срок, дней",saved.ImportantDays);
  var count=Field("Значимые события: максимум на клиента",saved.ImportantCount);
  var outputDays=Field("Подробные журналы: срок, дней",saved.OutputDays);
  var outputSize=Field("Подробные журналы: МиБ на клиента",saved.OutputMiB);
  var totalSize=Field("Подробные журналы: общий предел, МиБ",saved.TotalOutputMiB);
  panel.Children.Add(new TextBlock{Text="Ошибки, результаты установки и ручные действия хранятся дольше. Текущие статусы, незавершённые задания и открытые конфликты ID не очищаются. Перед ручной очисткой историю можно экспортировать кнопкой копирования всех событий.",TextWrapping=TextWrapping.Wrap,Margin=new(0,10,0,10)});
  var save=new Button{Content="Сохранить",IsDefault=true};panel.Children.Add(save);panel.Children.Add(new Button{Content="Отмена",IsCancel=true});
  save.Click+=(s,a)=>{try{saved.OrdinaryCount=int.Parse(ordinary.Text);saved.ImportantDays=int.Parse(days.Text);saved.ImportantCount=int.Parse(count.Text);saved.OutputDays=int.Parse(outputDays.Text);saved.OutputMiB=int.Parse(outputSize.Text);saved.TotalOutputMiB=int.Parse(totalSize.Text);saved.OrdinaryDays=Store.Read("event-days31.json",30);saved.Validate();Store.Write("history-limits58.json",saved);historyCompacted58.Clear();window.DialogResult=true;}catch(Exception ex){MessageBox.Show(window,ex.Message,"Лимиты истории");}};
  window.ShowDialog();
 }
}
