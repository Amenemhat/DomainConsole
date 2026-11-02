using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifySearchInput52(){
  var tab=MainTabs.SelectedItem;var selected=ComputerGrid.SelectedItem;int palette=Theme.Current;
  double width=Width,height=Height;bool ticking=timer.IsEnabled;timer.Stop();
  var proof=new List<string>();
  const string sample="Йц Дд gypq Latin Кириллица 0123456789 ";string longText=string.Concat(Enumerable.Repeat(sample,16));
  async Task Key(byte key){Key51(key);Key51(key,true);await Task.Delay(35);}
  async Task Check(TextBox box,string label){
   string old=box.Text;int start=box.SelectionStart,length=box.SelectionLength;
   try{
    box.Focus();Keyboard.Focus(box);await Task.Delay(40);
    if(!box.IsKeyboardFocused)throw new Exception("Search focus: "+label);
    Clipboard.SetText(longText);Key51(0x11);await Key(0x41);await Key(0x56);Key51(0x11,true);await Task.Delay(50);
    if(box.Text!=longText)throw new Exception("Native paste lost text: "+label);
    await Key(0x23);await Key(0x08);
    if(box.Text!=longText[..^1]||box.CaretIndex!=longText.Length-1)throw new Exception("Native end/backspace: "+label);
    await Key(0x24);await Key(0x27);
    int caret=box.CaretIndex;if(caret!=1)throw new Exception("Native home/right: "+label);
    RefreshOutcomes();RefreshBranches();UpdateLayout();await Task.Delay(50);
    if(!box.IsKeyboardFocused||box.CaretIndex!=caret||box.Text!=longText[..^1])throw new Exception("Background refresh changed search input: "+label);
    if(box.ActualHeight<box.FontSize+box.Padding.Top+box.Padding.Bottom)throw new Exception("Search height clips glyphs: "+label);
    Key51(0x11);await Key(0x41);await Key(0x43);Key51(0x11,true);
    if(Clipboard.GetText()!=longText[..^1])throw new Exception("Native copy: "+label);
    proof.Add(label+": PASS paste "+longText.Length+" chars Cyrillic/Latin; caret/edit/copy; background preserves focus/text/caret; glyph height");
   }finally{Key51(0x11,true);box.Text=old;box.Select(Math.Min(start,old.Length),Math.Min(length,Math.Max(0,old.Length-start)));}
  }
  try{
   Activate();SetForegroundWindow(new WindowInteropHelper(this).Handle);
   foreach(int theme in new[]{0,1,2})foreach(double size in new[]{1480d,1000d}){
    Theme.Apply(theme,false);Width=size;Height=size>1000?940:640;
    MainTabs.SelectedItem=WsusTab;ShowCurrent();UpdateLayout();
    await Check(SearchBox,$"{theme}/{size}/computers");
    foreach(var mode in new[]{WsusTab,CommandsTab,HistoryTab}){
     MainTabs.SelectedItem=mode;ShowCurrent();UpdateLayout();await Task.Delay(50);
     var fields=Visual33<LogViewer>(this).Where(v=>v.IsVisible&&v.ShellMode!="Report").SelectMany(v=>Visual33<TextBox>(v).Where(b=>b.IsVisible&&!b.IsReadOnly)).Distinct().ToList();
     if(mode==HistoryTab){fields.Add(HistoryClient);fields.Add(HistoryQuery);}
     int i=0;foreach(var box in fields.Where(b=>b.IsVisible))await Check(box,$"{theme}/{size}/{mode.Name}/{i++}");
    }
   }
   File.WriteAllLines(Path.Combine(Path.GetTempPath(),"ui-search-input52.txt"),proof);
  }finally{
   Key51(0x11,true);Theme.Apply(palette,false);Width=width;Height=height;ComputerGrid.SelectedItem=selected;MainTabs.SelectedItem=tab;ShowCurrent();if(ticking)timer.Start();
  }
 }
}
