using System.IO;
using System.Windows.Controls;
namespace DomainConsole;
public partial class MainWindow {
 async Task VerifyAttentionReading52(){
  const string a="000-attention-a52.invalid",b="000-attention-b52.invalid";
  var saved=maintenancePlans.ToList();bool ticking=timer.IsEnabled;timer.Stop();
  System.Windows.Window? window=null;
  try{
   maintenancePlans.Clear();
   maintenancePlans[a]=new(){Host=a,Phase="Требуется действие",Detail="KEEP_SELECTED52\n"+new string('x',2000)};
   maintenancePlans[b]=new(){Host=b,Phase="Требуется действие",Detail="Other client"};
   window=AttentionWindow36();window.Show();window.UpdateLayout();
   var list=Visual33<ListBox>(window).Single();
   var first=list.Items.Cast<AttentionEntry36>().Single(x=>x.Host==a);var second=list.Items.Cast<AttentionEntry36>().Single(x=>x.Host==b);
   first.Expanded=second.Expanded=true;list.SelectedItem=first;list.ScrollIntoView(first);window.UpdateLayout();
   var container=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(first);
   var box=Visual33<TextBox>(container).Single();int at=box.Text.IndexOf("KEEP_SELECTED52",StringComparison.Ordinal);box.Select(at,15);
   var scroll=Visual33<ScrollViewer>(list).First();double offset=scroll.VerticalOffset;
   maintenancePlans[b].Detail="Other client changed";
   await Task.Delay(3300);window.UpdateLayout();
   if(!first.Expanded||!second.Expanded||list.SelectedItem!=first||box.SelectedText!="KEEP_SELECTED52"||Math.Abs(scroll.VerticalOffset-offset)>1||!second.Details.Contains("Other client changed"))throw new Exception("Attention tick changed reading state or failed to update another client");
   maintenancePlans[a].Detail+="\nCurrent client changed";await Task.Delay(3300);window.UpdateLayout();
   if(box.SelectedText!="KEEP_SELECTED52"||!box.Text.Contains("Current client changed")||!first.Expanded)throw new Exception("Current attention update lost selected text");
   maintenancePlans.Remove(a);await Task.Delay(3300);window.UpdateLayout();
   if(!list.Items.Contains(first)||first.Required||first.Action!="Внимание больше не требуется"||!first.Expanded)throw new Exception("Resolved attention row disappeared while reading");
   var toggle=Visual33<CheckBox>(window).Single();toggle.IsChecked=true;window.UpdateLayout();
   if(list.Items.Cast<AttentionEntry36>().Single(x=>x.Host==b)!=second||!second.Expanded)throw new Exception("Attention filter recreated remaining row");
   window.Close();string unchanged=second.Details;maintenancePlans[b].Detail="Must not reach closed window";
   await Task.Delay(3300);if(second.Details!=unchanged)throw new Exception("Closed attention window timer still running");
   File.WriteAllText(Path.Combine(Path.GetTempPath(),"ui-attention52.txt"),"PASS: two expanded clients; several real timer ticks; other/current changes; selected text and scroll retained; resolved row retained; filter preserves remaining row; closed timer stopped");
  }finally{window?.Close();maintenancePlans.Clear();foreach(var p in saved)maintenancePlans[p.Key]=p.Value;if(ticking)timer.Start();}
 }
}
