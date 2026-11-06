using System.Windows;
using System.Windows.Controls;
namespace DomainConsole;
public sealed class OverviewRun61 {
 public string Server {get;set;}="";
 public string Started {get;set;}="";
 public string Ended {get;set;}="";
 public string LastPartial {get;set;}="";
 public string LastFull {get;set;}="";
 public string Phase {get;set;}="Обзор ещё не выполнялся";
 public string Error {get;set;}="";
 public int Total {get;set;}
 public int Processed {get;set;}
 public int Missing {get;set;}
 public int Errors {get;set;}
}
public partial class MainWindow {
 Window? overviewWindow61;TextBox? overviewText61;
 OverviewRun61 overviewRun61=Store.Read("overview-run61.json",new OverviewRun61());
 void RenderOverview61(){
  if(OverviewBusy61==null)return;
  if(overviewText61!=null){string text=OverviewDetailsText61();if(overviewText61.Text!=text){int start=overviewText61.SelectionStart,length=overviewText61.SelectionLength;double offset=overviewText61.VerticalOffset;overviewText61.Text=text;overviewText61.Select(Math.Min(start,text.Length),Math.Min(length,Math.Max(0,text.Length-start)));overviewText61.ScrollToVerticalOffset(offset);}}
  OverviewBusy61.Visibility=overviewRunning?Visibility.Visible:Visibility.Collapsed;OverviewBusy61.IsIndeterminate=overviewRunning;
  bool current=overviewRun61.Server==OverviewServer;
  OverviewProgress.Text=current?overviewRun61.Phase+(overviewRun61.Total>0?" · обработано "+overviewRun61.Processed+" / "+overviewRun61.Total:""):"Обзор выбранного WSUS ещё не выполнялся";
  OverviewDetails61.Content=current&&overviewRun61.Ended.Length>0?"Итоги · "+Display36.Date(overviewRun61.Ended):"Обзор WSUS · подробности";
 }
 string OverviewDetailsText61()=>"Сервер: "+overviewRun61.Server+"\n"+overviewRun61.Phase+"\nНачало: "+Display36.Date(overviewRun61.Started)+"\nЗавершение: "+Display36.Date(overviewRun61.Ended)+"\nПоследний полный обзор: "+Display36.Date(overviewRun61.LastFull)+"\nПоследний частичный обзор: "+Display36.Date(overviewRun61.LastPartial)+"\nОбработано: "+overviewRun61.Processed+" из "+overviewRun61.Total+"; не найдено: "+overviewRun61.Missing+"; ошибок: "+overviewRun61.Errors+(overviewRun61.Error.Length>0?"\nПричина: "+overviewRun61.Error:"")+"\nСледующий автоматический обзор: "+(!overviewSettings.Automatic?"выключен":overviewPaused||TransportBudget41.Paused?"приостановлен":Display36.Date(nextOverview.ToString("o")))+"\nСводки читаются общим запросом; эффективные одобрения уточняются пакетами по 10 клиентов. Подключения к клиентам, поиск и установка обновлений не выполняются.";
 void OverviewDetails61_Click(object sender,RoutedEventArgs e){
  if(overviewWindow61!=null){RenderOverview61();overviewWindow61.Activate();return;}
  var window=new Window{Owner=this,Title="Обзор WSUS",Width=580,Height=380,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  overviewWindow61=window;overviewText61=new TextBox{Text=OverviewDetailsText61(),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new(12)};window.Content=overviewText61;window.Closed+=(s,a)=>{overviewWindow61=null;overviewText61=null;};window.Show();
 }
}
