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
  var keys=new[]{"AppSurface","PanelSurface","SidebarSurface","EditorSurface","ReportSurface","StatusSurface","TextPrimary","TextMuted","Outline","SelectionSurface","SectionSurface","ScrollTrack","ScrollThumb","SuccessText","WarningText","ErrorText","InfoText","Accent"};
  string[][] values={
   new[]{"#E8EBEF","#EDF0F3","#DFE4EA","#F4F6F8","#EAF0F2","#E0E7ED","#293340","#596879","#C8D0DA","#C4D8EA","#DCE5EE","#E9EDF1","#B0BAC6","#247348","#986000","#B53D3D","#356D9B","#436987"},
   new[]{"#24282F","#2B3038","#20242A","#303640","#2D343B","#343C47","#D5DBE3","#A8B3C2","#46505E","#354E67","#394452","#2D333B","#535F6D","#9BCB9F","#E2B56F","#F39A96","#9CBFE3","#517DA5"},
   new[]{"#EAE7EF","#F0EDF4","#E3DDEB","#F7F4F9","#EDE8F3","#E4DCEC","#3C3449","#6B607B","#CFC4DA","#D9C8E7","#E5DDEC","#EFEAF4","#BAADC8","#2E7657","#946100","#AC3E50","#70518F","#80609B"}};
  for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[Current][i]));
  // Native popup/edit controls must use the palette too.
  foreach(var key in new[]{SystemColors.WindowBrushKey,SystemColors.ControlBrushKey})Application.Current.Resources[key]=Brush("PanelSurface");
  foreach(var key in new[]{SystemColors.WindowTextBrushKey,SystemColors.ControlTextBrushKey})Application.Current.Resources[key]=Brush("TextPrimary");
  if(save)Store.Write("theme.json",Current);Changed?.Invoke();
 }
}
