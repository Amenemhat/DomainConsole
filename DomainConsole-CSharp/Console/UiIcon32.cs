using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace DomainConsole;
public static class UiIcon32 {
 public static FrameworkElement Create(string kind){string data=kind switch {
 "stop"=>"M 4,4 L 16,4 L 16,16 L 4,16 Z",
 "copy"=>"M 7,6 L 17,6 L 17,18 L 7,18 Z M 4,14 L 3,14 L 3,2 L 13,2 L 13,3",
 "folder"=>"M 2,5 L 8,5 L 10,7 L 18,7 L 18,17 L 2,17 Z",
 "clear"=>"M 3,5 L 17,5 M 7,5 L 7,2 L 13,2 L 13,5 M 5,5 L 6,18 L 14,18 L 15,5 M 8,8 L 8,15 M 12,8 L 12,15",
 "up"=>"M 10,17 L 10,3 M 4,9 L 10,3 L 16,9",
 "down"=>"M 10,3 L 10,17 M 4,11 L 10,17 L 16,11",
 _=>"M 16,6 A 7,7 0 1 0 17,13 M 11,6 L 17,6 L 17,1"};
 var path=new System.Windows.Shapes.Path{Data=Geometry.Parse(data),StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};path.SetResourceReference(Shape.StrokeProperty,"TextPrimary");var canvas=new Canvas{Width=20,Height=20};canvas.Children.Add(path);return new Viewbox{Width=18,Height=18,Child=canvas,IsHitTestVisible=false};}
}
