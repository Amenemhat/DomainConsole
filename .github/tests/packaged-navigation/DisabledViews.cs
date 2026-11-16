using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

internal static class DisabledViews
{
    [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
    static IEnumerable<FrameworkElement> Children(DependencyObject parent)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);
            if(child is FrameworkElement element)yield return element;
            foreach(var descendant in Children(child))yield return descendant;
        }
    }
    internal static void Verify(Window win,object[] disabled,Action<bool,string> check)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var type=win.GetType();
        var recent=(ListBox)win.FindName("RecentList");var grid=(DataGrid)win.FindName("ComputerGrid");
        var nodes=(IDictionary)type.GetField("computerNodes",flags)!.GetValue(win)!;
        var theme=type.Assembly.GetType("DomainConsole.Theme",true)!.GetMethod("Apply",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!;
        static object? Get(object o,string key)=>o.GetType().GetProperty(key)!.GetValue(o);
        void Pump(){win.UpdateLayout();win.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);}
        void Inspect(FrameworkElement root,object row,string context)
        {
            string name=(string)Get(row,"Name")!;
            var label=Children(root).OfType<TextBlock>().Single(t=>t.Text==name&&t.IsVisible);
            var point=label.PointToScreen(new Point(3,Math.Min(5,label.ActualHeight/2)));
            if(!SetCursorPos((int)point.X,(int)point.Y))throw new Exception("No Windows desktop cursor");
            var watch=Stopwatch.StartNew();
            do{Pump();System.Threading.Thread.Sleep(12);}while(!label.IsMouseOver&&watch.ElapsedMilliseconds<1400);
            var shown=Children(root).Where(e=>e.GetType().Name=="StatusGlyph"&&e.IsVisible).ToArray();
            if(shown.Length!=2 || !shown.Any(g=>Equals(Get(g,"Kind"),Get(row,"DeviceKind38"))) || !label.IsMouseOver)
                throw new Exception("Selected/hovered disabled device lacks its two glyphs: "+context+" "+name+" "+string.Join(",",shown.Select(g=>Get(g,"Kind")))+" hover="+label.IsMouseOver);
            var ad=shown.Single(e=>Equals(Get(e,"Kind"),"AdDisabled"));
            var actual=((SolidColorBrush)label.Foreground).Color;
            if(shown.Any(g=>((SolidColorBrush)Get(g,"Tint")!).Color!=actual) || UIElementAutomationPeer.CreatePeerForElement(ad)?.GetName()!="Учётная запись отключена в AD")
                throw new Exception("Mismatched muted colour or accessibility label: "+context);
            if(Children(root).OfType<Button>().Any(b=>Equals(b.Tag,"Needed37")&&b.IsVisible))
                throw new Exception("Stale updates button remains an active hit target: "+context);
        }
        win.WindowState=WindowState.Normal;win.Left=0;win.Top=0;win.Height=760;win.Topmost=true;win.Activate();
        foreach(int palette in new[]{0,1,2})foreach(int width in new[]{1000,1440})
        {
            win.Width=width;theme.Invoke(null,[palette,false]);Pump();
            foreach(var row in disabled)
            {
                string host=(string)Get(row,"Host")!;
                type.GetMethod("PromoteRecent",flags)!.Invoke(win,[new[]{host},true]);
                recent.SelectedItem=row;recent.ScrollIntoView(row);Pump();
                Inspect((ListBoxItem)recent.ItemContainerGenerator.ContainerFromItem(row),row,"recent");
                var leaf=(TreeViewItem)nodes[host]!;
                for(var ancestor=ItemsControl.ItemsControlFromItemContainer(leaf) as TreeViewItem;ancestor!=null;ancestor=ItemsControl.ItemsControlFromItemContainer(ancestor) as TreeViewItem)ancestor.IsExpanded=true;
                leaf.BringIntoView();leaf.IsSelected=true;grid.SelectedItem=row;Pump();
                Inspect((FrameworkElement)leaf.Header,row,"directory");
            }
            check(true,$"selected and physically hovered disabled workstation and server: identical name/device/AD colours, two glyphs and accessible AD mark in both lists, palette {palette}, width {width}");
        }
    }
}
