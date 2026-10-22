using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Data;
namespace DomainConsole;
public static class UiIcon32 {
 public static readonly DependencyProperty IconProperty=DependencyProperty.RegisterAttached("Icon",typeof(string),typeof(UiIcon32),new PropertyMetadata("",(d,e)=>{if(d is Button button&&e.NewValue is string kind&&kind.Length>0){button.Content=Create(kind);if(string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(button)))System.Windows.Automation.AutomationProperties.SetName(button,kind switch {"close"=>"Закрыть","add"=>"Добавить","more"=>"Подробности","reboot"=>"Перезагрузка","settings"=>"Настройки","up"=>"Переместить вверх","down"=>"Переместить вниз",_=>"Обновить"});}}));
 public static void SetIcon(DependencyObject target,string value)=>target.SetValue(IconProperty,value);
 public static string GetIcon(DependencyObject target)=>(string)target.GetValue(IconProperty);

 public static FrameworkElement Create(string kind){string data=kind switch {
 "close"=>"M 3,3 L 17,17 M 17,3 L 3,17",
 "add"=>"M 10,3 L 10,17 M 3,10 L 17,10",
 "more"=>"M 5,10 A 1,1 0 1 0 3,10 A 1,1 0 1 0 5,10 M 11,10 A 1,1 0 1 0 9,10 A 1,1 0 1 0 11,10 M 17,10 A 1,1 0 1 0 15,10 A 1,1 0 1 0 17,10",
 "reboot"=>"M 16,7 A 7,7 0 1 0 16,14 M 11,7 L 17,7 L 17,1",
 "settings"=>"M 8,1 L 12,1 L 12.5,4 L 14.5,5 L 17,3.5 L 19,7 L 16.5,9 L 16.5,11 L 19,13 L 17,16.5 L 14.5,15 L 12.5,16 L 12,19 L 8,19 L 7.5,16 L 5.5,15 L 3,16.5 L 1,13 L 3.5,11 L 3.5,9 L 1,7 L 3,3.5 L 5.5,5 L 7.5,4 Z M 13,10 A 3,3 0 1 0 7,10 A 3,3 0 1 0 13,10",
 "stop"=>"M 4,4 L 16,4 L 16,16 L 4,16 Z",
 "copy"=>"M 7,6 L 17,6 L 17,18 L 7,18 Z M 4,14 L 3,14 L 3,2 L 13,2 L 13,3",
 "folder"=>"M 2,5 L 8,5 L 10,7 L 18,7 L 18,17 L 2,17 Z",
 "clear"=>"M 3,5 L 17,5 M 7,5 L 7,2 L 13,2 L 13,5 M 5,5 L 6,18 L 14,18 L 15,5 M 8,8 L 8,15 M 12,8 L 12,15",
 "up"=>"M 10,17 L 10,3 M 4,9 L 10,3 L 16,9",
 "down"=>"M 10,3 L 10,17 M 4,11 L 10,17 L 16,11",
 _=>"M 16,6 A 7,7 0 1 0 17,13 M 11,6 L 17,6 L 17,1"};
 var geometry=Geometry.Parse(data).Clone();var bounds=geometry.GetRenderBounds(new Pen(Brushes.Black,2.3));geometry.Transform=new TranslateTransform(10-(bounds.Left+bounds.Right)/2,10-(bounds.Top+bounds.Bottom)/2);
 var path=new System.Windows.Shapes.Path{Data=geometry,StrokeThickness=2.3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};path.SetBinding(Shape.StrokeProperty,new Binding(nameof(Control.Foreground)){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Button),1),FallbackValue=Theme.Brush("TextPrimary")});var canvas=new Canvas{Width=20,Height=20};canvas.Children.Add(path);return new Viewbox{Width=18,Height=18,Child=canvas,IsHitTestVisible=false,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};}
}
