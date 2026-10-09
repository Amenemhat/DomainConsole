using System.Windows;
using System.Windows.Media;
namespace DomainConsole;
public static class Theme {
 public static void Apply(int index,bool save=true){index=Math.Clamp(index,0,2);string[] accents={"#426888","#47796F","#74648B"};Application.Current.Resources["Accent"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(accents[index]));Application.Current.Resources["SelectionSurface"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(index==1?"#294C49":index==2?"#443C58":"#2A4863"));if(save)Store.Write("theme.json",index);}
}
