using System.Windows;
using System.Windows.Media;
namespace DomainConsole;
public sealed class StatusGlyph:FrameworkElement {
 public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(StatusGlyph),new FrameworkPropertyMetadata("Unknown",FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty TintProperty=DependencyProperty.Register(nameof(Tint),typeof(Brush),typeof(StatusGlyph),new FrameworkPropertyMetadata(Brushes.LightSlateGray,FrameworkPropertyMetadataOptions.AffectsRender));
 public string Kind{get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}public Brush Tint{get=>(Brush)GetValue(TintProperty);set=>SetValue(TintProperty,value);}
 public StatusGlyph(){Width=18;Height=18;VerticalAlignment=VerticalAlignment.Center;SnapsToDevicePixels=true;}
 public static string Shape(string kind)=>kind switch {
 "Success" or "Stale"=>"M 2,9 L 7,14 L 16,3",
 "Error" or "SyncError"=>"M 3,3 L 15,15 M 15,3 L 3,15",
 "Reboot"=>"M 15,6 A 6,6 0 1 0 15,12 M 11,6 L 15,6 L 15,2",
 "Warning"=>"M 9,2 L 17,16 L 1,16 Z M 9,7 L 9,11 M 9,14 L 9,14.2",
 "Connected"=>"M 5,6 L 5,2 M 13,6 L 13,2 M 3,6 L 15,6 L 15,9 Q 15,13 9,13 Q 3,13 3,9 Z M 9,13 L 9,17",
 "Unreachable"=>"M 2,6 L 6,6 L 6,12 L 2,12 M 16,6 L 12,6 L 12,12 L 16,12 M 2,16 L 16,2",
 "Access"=>"M 4,8 L 14,8 L 14,16 L 4,16 Z M 6,8 L 6,5 A 3,3 0 0 1 12,5 L 12,8",
 "AdDisabled"=>"M 2,3 L 16,3 L 16,13 L 2,13 Z M 6,16 L 12,16 M 9,13 L 9,16 M 1,17 L 17,1",
 "NoWsus"=>"M 9,2 A 7,7 0 1 1 8.99,2 M 4,14 L 14,4",
 _=>"M 5,5 C 5,0 15,0 13,7 L 9,10 L 9,12 M 9,15 L 9,16"};
 protected override void OnRender(DrawingContext drawing){base.OnRender(drawing);drawing.DrawRectangle(Brushes.Transparent,null,new Rect(RenderSize));var pen=new Pen(Tint,2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};drawing.DrawGeometry(null,pen,Geometry.Parse(Shape(Kind)));}
}
