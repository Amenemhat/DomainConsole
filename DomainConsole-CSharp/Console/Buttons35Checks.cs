using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyButtons35(){
  foreach(var kind in new[]{"close","add","more","reboot","settings","stop","copy","folder","clear","up","down","refresh"}){
   var icon=UiIcon32.Create(kind);icon.Measure(new Size(18,18));icon.Arrange(new Rect(0,0,18,18));var path=Visual33<System.Windows.Shapes.Path>(icon).Single();var bounds=path.Data.GetRenderBounds(new Pen(Brushes.Black,path.StrokeThickness));if(Math.Abs(bounds.X+bounds.Width/2-10)>.01||Math.Abs(bounds.Y+bounds.Height/2-10)>.01)throw new Exception("Icon geometry is not centered: "+kind);
  }
  static void CheckButton(Button button){if(!button.IsVisible||button.ActualWidth<=0||button.ActualHeight<=0)return;var presenter=Visual33<ContentPresenter>(button).FirstOrDefault(x=>x.TemplatedParent==button);if(presenter==null)throw new Exception("Button content presenter missing: "+button.Name);var center=presenter.TranslatePoint(new Point(presenter.ActualWidth/2,presenter.ActualHeight/2),button);if(Math.Abs(center.X-button.ActualWidth/2)>1||Math.Abs(center.Y-button.ActualHeight/2)>1)throw new Exception("Button content offset: "+button.Name+" "+button.Content+" "+center);}
  int palette=Theme.Current;double width=Width,height=Height;var tab=MainTabs.SelectedItem;var cancel=HeaderAbortReboot.Visibility;var info=PostRebootInfo.Visibility;bool running=timer.IsEnabled;timer.Stop();
  try{Width=1000;Height=640;
   foreach(int color in new[]{0,1,2}){Theme.Apply(color,false);MainTabs.SelectedItem=CommandsTab;RefreshRebootHeader();HeaderAbortReboot.Visibility=Visibility.Visible;PostRebootInfo.Visibility=Visibility.Visible;ArrangeIdentity34();UpdateLayout();Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);UpdateLayout();foreach(var button in Visual33<Button>(this))CheckButton(button);
    if(HeaderAbortReboot.Content is not Viewbox)throw new Exception("Cancel reboot still uses a font glyph");if(HeaderAbortReboot.InputHitTest(new Point(4,15))==null)throw new Exception("Cancel button click target is limited to icon");var path=Visual33<System.Windows.Shapes.Path>(HeaderAbortReboot).Single();if(path.Stroke.ToString()!=Theme.Brush("ErrorText").ToString())throw new Exception("Cancel icon does not inherit palette foreground");if(HeaderAbortReboot.TranslatePoint(new Point(0,0),ConnectionPanel32).X<HeaderReboot.TranslatePoint(new Point(HeaderReboot.ActualWidth,0),ConnectionPanel32).X-1)throw new Exception("Cancel reboot is not after reboot");Capture32("buttons35-"+color+"-cancel");
    foreach(int dpi in new[]{96,120,144,192}){var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)(HeaderAbortReboot.ActualWidth*dpi/96),(int)(HeaderAbortReboot.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle(new VisualBrush(HeaderAbortReboot){Stretch=Stretch.Fill},null,new Rect(0,0,HeaderAbortReboot.ActualWidth,HeaderAbortReboot.ActualHeight));bitmap.Render(drawing);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);int left=bitmap.PixelWidth,right=-1,top=bitmap.PixelHeight,bottom=-1;for(int y=0;y<bitmap.PixelHeight;y++)for(int x=0;x<bitmap.PixelWidth;x++)if(pixels[(y*bitmap.PixelWidth+x)*4+3]>20){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}if(right<0||Math.Abs((left+right+1)/2d-bitmap.PixelWidth/2d)>1||Math.Abs((top+bottom+1)/2d-bitmap.PixelHeight/2d)>1)throw new Exception("Cancel icon pixel bounds are empty or off-center at DPI "+dpi);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ui-cancel35-"+color+"-"+dpi+".png"));encoder.Save(file);}
   }
   MainTabs.SelectedItem=SettingsTab;UpdateLayout();foreach(var button in Visual33<Button>(this))CheckButton(button);
   var dialog=new CacheChoice(this);try{dialog.Show();dialog.UpdateLayout();foreach(var button in Visual33<Button>(dialog))CheckButton(button);}finally{dialog.Close();}
  }finally{Width=width;Height=height;MainTabs.SelectedItem=tab;Theme.Apply(palette,false);HeaderAbortReboot.Visibility=cancel;PostRebootInfo.Visibility=info;RefreshRebootHeader();ArrangeIdentity34();if(running)timer.Start();}
 }
}
