using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace DomainConsole;
public partial class MainWindow {
 void VerifyRelease58(){
  var row=Current();var prior=identities58.GetValueOrDefault(row.Host);var selected=MainTabs.SelectedItem;int theme=Theme.Current;var priorSnapshot=serverSnapshot58;
  var fixture=new ClientIdentity58{Host=row.Host,Name="identity-fixture.invalid",Machine="11111111-1111-1111-1111-111111111111",Server="http://wsus.invalid:8530",SusClientId="22222222-2222-2222-2222-222222222222",At=DateTimeOffset.UtcNow.ToString("o")};
  bool hadConflict=compromisedIds58.Contains(fixture.Key);
  try{
   identities58[row.Host]=fixture;
   serverSnapshot58=new(){Server=OverviewServer+(WsusSsl.IsChecked==true?"|https":"|http"),Complete=true,Records=new(){new(){Id=fixture.SusClientId,Name="other-fixture.invalid"}}};
   if(!IdentityMismatch58(row.Host)||!IdentityConflict58(row.Host))throw new Exception("Server ID/name mismatch hidden");
   ObserveIdentity58(row.Host,new(){At=DateTimeOffset.UtcNow.ToString("o"),Error="Fixture: registry read unavailable"});
   if(identities58[row.Host].SusClientId!=fixture.SusClientId||identities58[row.Host].Fresh(DateTimeOffset.UtcNow))throw new Exception("Failed identity probe lost saved ID or became fresh");
   compromisedIds58.Add(fixture.Key);
   foreach(int color in new[]{0,1,2}){
    Theme.Apply(color,false);RefreshOutcomes();ShowCurrent();RefreshIdentity58(row.Host);
    if(HeaderIdentity58.Visibility!=Visibility.Visible||!IdentityConflict58(row.Host))throw new Exception("Identity conflict is not visible");
    foreach(var tab in new[]{CommandsTab,WsusTab,HistoryTab,SettingsTab}){
     MainTabs.SelectedItem=tab;UpdateLayout();
     CaptureContent38("release58-"+color+"-"+tab.Name+"-compact",1280,720);
     CaptureContent38("release58-"+color+"-"+tab.Name+"-wide",1920,1080);
    }
    MainTabs.SelectedItem=WsusTab;
    foreach(var window in new[]{IdentityWindow58(),HistoryLimitsWindow58()}){
     try{
      window.Show();window.UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
      foreach(var button in Visual33<Button>(window).Where(b=>b.IsVisible)){
       var edge=button.TranslatePoint(new Point(button.ActualWidth,button.ActualHeight),window);
       if(edge.X>window.ActualWidth+1||edge.Y>window.ActualHeight+1)throw new Exception("New dialog action clipped: "+button.Content);
      }
      var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);
      var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
      using var file=File.Create(Path.Combine(Path.GetTempPath(),"ui-release58-"+color+"-"+(window.Title.StartsWith("Идентичность")?"identity":"history-limits")+".png"));encoder.Save(file);
     }finally{window.Close();}
    }
   }
  }finally{
   serverSnapshot58=priorSnapshot;if(prior==null)identities58.Remove(row.Host);else identities58[row.Host]=prior;Store.Write("client-identities58.json",identities58);
   if(!hadConflict)compromisedIds58.Remove(fixture.Key);Theme.Apply(theme,false);MainTabs.SelectedItem=selected;RefreshOutcomes();ShowCurrent();
  }
 }
}
