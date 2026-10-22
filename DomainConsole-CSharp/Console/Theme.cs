using System.Windows;
using System.Windows.Media;
namespace DomainConsole;
public static class Theme {
 public static int Current {get;private set;}
 public static bool Dark=>Current==1;
 public static event Action? Changed;
 public static Brush Brush(string key)=>(Brush)Application.Current.FindResource(key);
 public static Brush Tone(string tone)=>Brush(tone switch {"Success"=>"SuccessText","Warning"=>"WarningText","Error"=>"ErrorText","Info"=>"InfoText","Stale"=>"TextMuted",_=>"TextPrimary"});
 public static void Apply(int index,bool save=true){Current=Math.Clamp(index,0,2);
  var keys=new[]{"AppSurface","PanelSurface","SidebarSurface","EditorSurface","ReportSurface","StatusSurface","TextPrimary","TextMuted","Outline","SelectionSurface","SectionSurface","ScrollTrack","ScrollThumb","SuccessText","WarningText","ErrorText","InfoText","Accent","ActiveSurface","HeaderSurface","ButtonSurface","HoverSurface"};
  string[][] values={new[]{"#F8FAFD","#F0F4F9","#C4CFDD","#FFFFFF","#FBFCFE","#E2EAF3","#14283B","#344B60","#8B9FB6","#D7E7FA","#DEE8F4","#00FFFFFF","#7D91A8","#006329","#675400","#AA1730","#0048B5","#295A89","#DFEAF7","#E8EFF7","#CAD8E8","#B8CCE2"},new[]{"#20262E","#2C3540","#18212B","#29333E","#293643","#293D4C","#F0F4F8","#CCD6E1","#71849A","#314E69","#3C4D60","#00FFFFFF","#71869C","#6BF098","#F5CB45","#FFA1AA","#83C3FF","#477FAE","#354D62","#344151","#40566C","#4A6680"},new[]{"#FCFAFE","#F5F0FA","#D1C6DF","#FFFFFF","#FCF9FE","#ECE3F3","#30213F","#4E3C60","#9F87B5","#E2D1F2","#E9DFF3","#00FFFFFF","#9982AF","#005F28","#675000","#A1152B","#0046B0","#75528E","#EAE0F5","#F0E8F7","#D9C8E9","#CCB6E1"}};
  for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[Current][i]));
  Application.Current.Resources["ComputerName36"]=Brush("TextPrimary");Application.Current.Resources["DisabledName36"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(Dark?"#98A1AB":"#66717D"));
  // Native popup/edit controls must use the palette too.
  foreach(var key in new[]{SystemColors.WindowBrushKey,SystemColors.ControlBrushKey})Application.Current.Resources[key]=Brush("PanelSurface");
  foreach(var key in new[]{SystemColors.WindowTextBrushKey,SystemColors.ControlTextBrushKey})Application.Current.Resources[key]=Brush("TextPrimary");
  if(save)Store.Write("theme.json",Current);Changed?.Invoke();
 }
}
