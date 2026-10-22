using System.Windows;
using System.Windows.Media;
namespace DomainConsole;
public sealed class StatusGlyph:FrameworkElement {
 public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(StatusGlyph),new FrameworkPropertyMetadata("Unknown",FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty TintProperty=DependencyProperty.Register(nameof(Tint),typeof(Brush),typeof(StatusGlyph),new FrameworkPropertyMetadata(Brushes.LightSlateGray,FrameworkPropertyMetadataOptions.AffectsRender));
 public string Kind{get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}public Brush Tint{get=>(Brush)GetValue(TintProperty);set=>SetValue(TintProperty,value);}
 public StatusGlyph(){Width=20;Height=20;VerticalAlignment=VerticalAlignment.Center;SnapsToDevicePixels=true;}
 public static string Shape(string kind)=>kind switch {
 "Computer"=>"M 1,2 L 17,2 L 17,12 L 1,12 Z M 9,12 L 9,16 M 5,16 L 13,16",
 "Server"=>"M 3,1 L 15,1 L 15,17 L 3,17 Z M 6,5 L 12,5 M 6,9 L 12,9 M 6,13 L 7,13",
 "Device"=>"M 2,3 L 16,3 L 16,14 L 2,14 Z M 7,17 L 11,17",
 "Info"=>"M 9,1 A 8,8 0 1 1 8.99,1 M 9,8 L 9,13 M 9,5 L 9,5.2",
 "Working"=>"M 7,1 L 11,1 L 12,4 L 15,3 L 17,6 L 15,8 L 17,11 L 15,14 L 12,13 L 11,17 L 7,17 L 6,14 L 3,15 L 1,12 L 3,10 L 1,7 L 3,4 L 6,5 Z M 12,9 A 3,3 0 1 0 6,9 A 3,3 0 1 0 12,9",
 "Success" or "Needed"=>"M 2,9 L 7,14 L 16,3",
 "Stale"=>"M 9,1 A 8,8 0 1 1 8.99,1 M 9,4 L 9,9 L 13,11",
 "Error" or "SyncError"=>"M 3,3 L 15,15 M 15,3 L 3,15",
 "Reboot"=>"M 15,6 A 6,6 0 1 0 15,12 M 11,6 L 15,6 L 15,2",
 "Warning"=>"M 9,2 L 17,16 L 1,16 Z M 9,7 L 9,11 M 9,14 L 9,14.2",
 "Connected" or "SavedConnected" or "Delayed"=>"M 5,6 L 5,2 M 13,6 L 13,2 M 3,6 L 15,6 L 15,9 Q 15,13 9,13 Q 3,13 3,9 Z M 9,13 L 9,17",
 "Unreachable"=>"M 2,6 L 6,6 L 6,12 L 2,12 M 16,6 L 12,6 L 12,12 L 16,12 M 2,16 L 16,2",
 "Access"=>"M 4,8 L 14,8 L 14,16 L 4,16 Z M 6,8 L 6,5 A 3,3 0 0 1 12,5 L 12,8",
 "AdDisabled"=>"M 6,6 A 3,3 0 1 0 6,0 A 3,3 0 1 0 6,6 M 1,15 L 1,11 Q 1,7 6,7 L 8,7 M 17,12 A 5,5 0 1 0 7,12 A 5,5 0 1 0 17,12 M 9,15 L 15,9",
 "DisabledComputer"=>Shape("Computer")+" M 17,13 A 4,4 0 1 0 9,13 A 4,4 0 1 0 17,13 M 10,16 L 16,10",
 "DisabledServer"=>Shape("Server")+" M 17,13 A 4,4 0 1 0 9,13 A 4,4 0 1 0 17,13 M 10,16 L 16,10",
 "NoWsus"=>"M 9,2 A 7,7 0 1 1 8.99,2 M 4,14 L 14,4",
 _=>"M 5,5 C 5,0 15,0 13,7 L 9,10 L 9,12 M 9,15 L 9,16"};
 protected override void OnRender(DrawingContext drawing){base.OnRender(drawing);drawing.DrawRectangle(Brushes.Transparent,null,new Rect(RenderSize));double scale=Math.Min(ActualWidth,ActualHeight)/20;drawing.PushTransform(new TranslateTransform((ActualWidth-20*scale)/2,(ActualHeight-20*scale)/2));drawing.PushTransform(new ScaleTransform(scale,scale));drawing.PushTransform(new TranslateTransform(1,1));var pen=new Pen(Tint,2.6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};drawing.DrawGeometry(null,pen,Geometry.Parse(Shape(Kind)));drawing.Pop();drawing.Pop();drawing.Pop();}
}

