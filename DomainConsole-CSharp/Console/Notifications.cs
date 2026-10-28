using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
namespace DomainConsole;
public partial class MainWindow {
 DispatcherTimer? toastTimer;
 void Notify(string text){if(ToastText==null)return;ToastText.Text=text;ToastInfo47.ToolTip=new ToolTip{Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,MaxWidth=320}};Toast.Visibility=Visibility.Visible;ArrangeToast47(null,EventArgs.Empty);toastTimer??=new DispatcherTimer{Interval=TimeSpan.FromSeconds(7)};toastTimer.Stop();toastTimer.Tick-=HideToast;toastTimer.Tick+=HideToast;toastTimer.Start();}
 bool arrangingToast47;
 List<Rect> HeaderOccupied47()=>Visual33<FrameworkElement>(IdentityHeader34).Where(e=>e.IsVisible&&e.ActualWidth>0&&e.ActualHeight>0&&(e is Button||e is TextBlock t&&!string.IsNullOrWhiteSpace(t.Text)||e is StatusGlyph)).Select(e=>new Rect(e.TranslatePoint(new Point(),ToastLayer47),new Size(e.ActualWidth,e.ActualHeight))).ToList();
 void ArrangeToast47(object? sender,EventArgs e){
  if(arrangingToast47||Toast==null||Toast.Visibility!=Visibility.Visible||CompactHeader32.ActualWidth<40)return;
  arrangingToast47=true;try{
   var occupied=HeaderOccupied47();double right=CompactHeader32.ActualWidth-5,height=24;Rect? location=null;
   foreach(double width in new[]{330d,240d,180d}){if(right-width<4)continue;for(double y=4;y+height<=CompactHeader32.ActualHeight-4;y+=2){var candidate=new Rect(right-width,y,width,height);var padded=candidate;padded.Inflate(2,2);if(!occupied.Any(r=>r.IntersectsWith(padded))){location=candidate;break;}}if(location.HasValue)break;}
   bool compact=!location.HasValue;var box=location??new Rect(right-26,5,26,24);
   ToastText.Visibility=ToastClose47.Visibility=compact?Visibility.Collapsed:Visibility.Visible;ToastInfo47.Visibility=compact?Visibility.Visible:Visibility.Collapsed;
   Toast.Width=box.Width;Toast.Height=box.Height;Canvas.SetRight(Toast,double.NaN);Canvas.SetLeft(Toast,box.X);Canvas.SetTop(Toast,box.Y);
  }finally{arrangingToast47=false;}
 }
 void ShowToastDetails47(object sender,RoutedEventArgs e){if(ToastInfo47.ToolTip is ToolTip tip){tip.PlacementTarget=ToastInfo47;tip.IsOpen=true;}}
 void HideToast(object? sender,EventArgs e){toastTimer?.Stop();if(ToastInfo47?.ToolTip is ToolTip tip)tip.IsOpen=false;Toast.Visibility=Visibility.Collapsed;}
 void VerifyToastPlacement47(bool updateWindow=true){
  ArrangeToast47(null,EventArgs.Empty);
  if(updateWindow)UpdateLayout();
  else { // Offscreen fixture: updating Window layout would restore its on-screen size.
   Toast.Measure(new Size(Toast.Width,Toast.Height));
   Toast.Arrange(new Rect(Canvas.GetLeft(Toast),Canvas.GetTop(Toast),Toast.Width,Toast.Height));
  }
  var bounds=new Rect(Toast.TranslatePoint(new Point(),ToastLayer47),new Size(Toast.ActualWidth,Toast.ActualHeight));
  if(HeaderOccupied47().Any(r=>r.IntersectsWith(bounds)))throw new Exception("Notification overlaps client header facts/actions");
  if(bounds.Top<0||bounds.Bottom>CompactHeader32.ActualHeight||bounds.Left<0||bounds.Right>CompactHeader32.ActualWidth)throw new Exception("Notification escapes fixed header area");
 }
 void DismissToast_Click(object sender,RoutedEventArgs e)=>HideToast(sender,e);
}
