using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace DomainConsole;
public static class ActionIcon38 {
 public static readonly DependencyProperty KindProperty=DependencyProperty.RegisterAttached("Kind",typeof(string),typeof(ActionIcon38),new PropertyMetadata("",Changed));
 public static readonly DependencyProperty ToneProperty=DependencyProperty.RegisterAttached("Tone",typeof(string),typeof(ActionIcon38),new PropertyMetadata("InfoText",Changed));
 public static readonly DependencyProperty VisualProperty=DependencyProperty.RegisterAttached("Visual",typeof(FrameworkElement),typeof(ActionIcon38));
 public static void SetKind(DependencyObject o,string v)=>o.SetValue(KindProperty,v);public static string GetKind(DependencyObject o)=>(string)o.GetValue(KindProperty);
 public static void SetTone(DependencyObject o,string v)=>o.SetValue(ToneProperty,v);public static string GetTone(DependencyObject o)=>(string)o.GetValue(ToneProperty);
 public static void SetVisual(DependencyObject o,FrameworkElement v)=>o.SetValue(VisualProperty,v);public static FrameworkElement GetVisual(DependencyObject o)=>(FrameworkElement)o.GetValue(VisualProperty);
 static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e){if(o is not Button b)return;var visual=UiIcon32.Create(GetKind(o));visual.Width=23;visual.Height=23;var path=((Canvas)((Viewbox)visual).Child).Children.OfType<System.Windows.Shapes.Path>().First();path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,GetTone(o));SetVisual(o,visual);if(GetTone(o)=="ErrorText")b.SetResourceReference(Control.ForegroundProperty,"ErrorText");}
}
