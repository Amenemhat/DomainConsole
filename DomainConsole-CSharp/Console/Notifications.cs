using System.Windows;
using System.Windows.Threading;
namespace DomainConsole;
public partial class MainWindow {
 DispatcherTimer? toastTimer;
 void Notify(string text){if(ToastText==null)return;ToastText.Text=text;Toast.Visibility=Visibility.Visible;toastTimer??=new DispatcherTimer{Interval=TimeSpan.FromSeconds(7)};toastTimer.Stop();toastTimer.Tick-=HideToast;toastTimer.Tick+=HideToast;toastTimer.Start();}
 void HideToast(object? sender,EventArgs e){toastTimer?.Stop();Toast.Visibility=Visibility.Collapsed;}
 void DismissToast_Click(object sender,RoutedEventArgs e)=>HideToast(sender,e);
}
