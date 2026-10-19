using System.Windows;
using System.Windows.Media;
namespace DomainConsole;
public static class Theme {
 public static int Current {get;private set;}
 public static bool Dark=>Current==1;
 public static event Action? Changed;
 public static Brush Brush(string key)=>(Brush)Application.Current.FindResource(key);
 public static Brush Tone(string tone)=>Brush(tone switch {"Success"=>"SuccessText","Warning"=>"WarningText","Error"=>"ErrorText","Info"=>"InfoText",_=>"TextPrimary"});
 public static void Apply(int index,bool save=true){Current=Math.Clamp(index,0,2);
  var keys=new[]{"AppSurface","PanelSurface","SidebarSurface","EditorSurface","ReportSurface","StatusSurface","TextPrimary","TextMuted","Outline","SelectionSurface","SectionSurface","ScrollTrack","ScrollThumb","SuccessText","WarningText","ErrorText","InfoText","Accent","ActiveSurface","HeaderSurface","ButtonSurface","HoverSurface"};
  string[][] values={new[]{"#E3E8EE","#F0F3F6","#D5DDE6","#FAFBFD","#F1F6FB","#DFE9F3","#172B3D","#465C70","#9DAEC0","#C9E1F5","#D3E1EE","#DFE6EE","#899CB1","#087B3B","#A95500","#BA3038","#155BC2","#38688F","#D6E6F4","#E4EDF5","#D8E3EE","#D2E1F0"},new[]{"#20262E","#2C3540","#18212B","#29333E","#293643","#293D4C","#F0F4F8","#BBC8D5","#63768A","#314E69","#3C4D60","#28333E","#63788D","#64E695","#FFC06B","#FF929B","#70B7FF","#477FAE","#354D62","#344151","#364758","#40586D"},new[]{"#E8E2ED","#F2EDF6","#DDD2E7","#FCF9FE","#F4EEF9","#E7DDEF","#30213F","#665176","#B39CC6","#DAC5EE","#DDCDEB","#E8DFEF","#AC97BD","#087B4A","#AA5A04","#B82F53","#225DC5","#80609B","#E6D9F2","#E9DEF2","#E3D7ED","#DACCE6"}};
  for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[Current][i]));
  // Native popup/edit controls must use the palette too.
  foreach(var key in new[]{SystemColors.WindowBrushKey,SystemColors.ControlBrushKey})Application.Current.Resources[key]=Brush("PanelSurface");
  foreach(var key in new[]{SystemColors.WindowTextBrushKey,SystemColors.ControlTextBrushKey})Application.Current.Resources[key]=Brush("TextPrimary");
  if(save)Store.Write("theme.json",Current);Changed?.Invoke();
 }
}
