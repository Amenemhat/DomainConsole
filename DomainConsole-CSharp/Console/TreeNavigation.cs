using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace DomainConsole;
public partial class MainWindow {
 bool togglingTree;
 static T? Ancestor<T>(DependencyObject? source) where T:DependencyObject {
  for(var node=source;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))if(node is T found)return found;
  return null;
 }
 void SetupTreeNavigation(){
  OuTree.AddHandler(FrameworkElement.RequestBringIntoViewEvent,new RequestBringIntoViewEventHandler((sender,e)=>{if(togglingTree)e.Handled=true;}),true);
 }
 void WireBranch(TreeViewItem item){
  item.AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler((sender,e)=>{
   if(e.Handled||e.ChangedButton!=MouseButton.Left)return;
   var toggle=Ancestor<ToggleButton>(e.OriginalSource as DependencyObject);
   // The expander belongs to this header, not to a nested OU or its checkbox.
   if(toggle?.Name!="Expander"||!ReferenceEquals(toggle.TemplatedParent,item))return;
   e.Handled=true;ToggleBranch(item);
  }),true);
 }
 void ToggleBranch(TreeViewItem item){
  var scroll=FindTreeScroll(OuTree);double offset=scroll?.VerticalOffset??0;
  togglingTree=true;item.IsExpanded=!item.IsExpanded;
  Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>{scroll?.ScrollToVerticalOffset(offset);togglingTree=false;}));
 }
 static ScrollViewer? FindTreeScroll(DependencyObject node){
  if(node is ScrollViewer scroll)return scroll;
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var result=FindTreeScroll(VisualTreeHelper.GetChild(node,i));if(result!=null)return result;}return null;
 }
}
