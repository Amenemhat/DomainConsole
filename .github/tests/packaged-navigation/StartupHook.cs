using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

internal class StartupHook
{
    public static void Initialize()
    {
        var result=Environment.GetEnvironmentVariable("DC_NAV_EVIDENCE");
        if (string.IsNullOrEmpty(result)) return;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is not Window window || window.GetType().FullName != "DomainConsole.MainWindow") return;
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
                {
                    try { Exercise(window,result); Application.Current.Shutdown(0); }
                    catch(Exception error) { File.WriteAllText(result,error.ToString()); Application.Current.Shutdown(1); }
                }));
            }));
    }

    static IEnumerable<T> Visual<T>(DependencyObject owner) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(owner);i++)
        {
            var child=VisualTreeHelper.GetChild(owner,i);
            if(child is T match) yield return match;
            foreach(var descendant in Visual<T>(child)) yield return descendant;
        }
    }
    static void Exercise(Window win,string result)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var type=win.GetType();
        object Field(string name)=>type.GetField(name,flags)!.GetValue(win)!;
        object? Call(string name,params object?[] args)=>type.GetMethod(name,flags)!.Invoke(win,args);
        object Prop(object row,string name)=>row.GetType().GetProperty(name)!.GetValue(row)!;
        var all=(IList)type.GetProperty("Computers")!.GetValue(win)!;
        var shown=(IList)type.GetProperty("RecentComputers")!.GetValue(win)!;
        var list=(ListBox)win.FindName("RecentList");
        var search=(TextBox)win.FindName("SearchBox");
        var history=(IList)type.GetProperty("History")!.GetValue(win)!;
        var names=(List<string>)Field("recentHosts");
        var pinned=(List<string>)Field("workHosts");
        var prep=(IDictionary)Field("preparing60");
        var rows=all.Cast<object>().ToList();
        var facts=new List<string>();
        void Check(bool ok,string text){if(!ok)throw new InvalidDataException(text);facts.Add(text);}
        void Outcome(object row,string status)=>row.GetType().GetMethod("SetOutcome")!.Invoke(row,[status,"exercise"]);
        string Host(object row)=>(string)Prop(row,"Host");
        bool Divider(object row)=>(bool)Prop(row,"StartsReady");
        void Refresh(){Call("RefreshRecent");win.UpdateLayout();}
        Check(rows.Count==420,"420 synthetic AD computers loaded by normal Windows startup");
        Check(pinned.Count==1 && shown.Cast<object>().Any(r=>Host(r)==pinned[0]),"pre-upgrade work-computers.json pin restored");
        Check(history.Count>=80,"persisted history restored before navigation test");
        int jobs=history.Count;
        var seed=rows.Skip(10).Take(5).ToArray();
        names.Clear();names.AddRange(seed.Select(Host));
        var callerPin=pinned.Single();
        pinned.Clear();
        foreach(var row in seed) Outcome(row,"Error");
        Refresh();
        Check(shown.Count==5&&!shown.Cast<object>().Any(Divider),"all five ordinary slots in work, no divider");
        foreach(var row in seed) Outcome(row,"Success");
        Refresh();
        Check(shown.Count==5 && Divider(shown[0]!) && shown.Cast<object>().Count(Divider)==1,"all five ready, first computer carries the only divider");
        Outcome(seed[1],"Error");Outcome(seed[3],"Unknown");
        Refresh();
        Check(shown.Cast<object>().SequenceEqual(new[]{seed[1],seed[3],seed[0],seed[2],seed[4]}) && Divider(seed[0]),"stable order within work and ready with one common N=5");
        var range=typeof(Enumerable).GetMethod("Cast")!.MakeGenericMethod(seed[0].GetType()).Invoke(null,[shown])!;
        var typedList=typeof(Enumerable).GetMethod("ToList")!.MakeGenericMethod(seed[0].GetType()).Invoke(null,[range])!;
        Call("MarkRange",shown[1],typedList,true,false,true);
        Call("MarkRange",shown[3],typedList,true,true,true);
        Check(new[]{1,2,3}.All(i=>(bool)Prop(shown[i]!,"Selected")),"shift marks computers across the ready divider");
        var overlap=rows[21];string extra=Host(overlap);
        prep[extra+"|WSUS"]=("WSUS","Подготовка",DateTimeOffset.UtcNow);
        pinned.Add(extra);
        Refresh();
        Check(shown.Count==6 && shown.Cast<object>().Count(r=>Host(r)==extra)==1,"one pinned and active PC consumes exactly one additional row");
        Call("PromoteRecent",new[]{extra},true);
        Check(shown[0]==overlap,"first visible position changes immediately on preparation");
        Outcome(seed[0],"Warning");Refresh();
        Check(shown.IndexOf(seed[0])<shown.IndexOf(seed[2])&&(bool)Prop(seed[0],"Selected"),"new problem returns a ready PC to work and preserves its mark");
        prep.Clear();pinned.Clear();pinned.Add(callerPin);
        names.Clear();names.AddRange(rows.Take(200).Select(Host));type.GetField("recentLimit",flags)!.SetValue(win,200);
        Refresh();
        var viewer=Visual<ScrollViewer>(list).First();
        viewer.ScrollToVerticalOffset(25);win.UpdateLayout();
        Check(viewer.VerticalOffset>=1,"actual scroll viewport contains more PCs than fit on screen");
        var anchor=shown[(int)viewer.VerticalOffset]!;
        var watch=Stopwatch.StartNew();
        for(int i=0;i<12;i++) Call("RefreshRecent");
        watch.Stop();
        Check(shown[(int)viewer.VerticalOffset]==anchor,"12 background redraws retain the reading anchor");
        Check(watch.Elapsed<TimeSpan.FromSeconds(10),"12 local refreshes in "+watch.ElapsedMilliseconds+" ms with 420 PCs and restored history");
        search.Text=Host(rows[20]);win.UpdateLayout();
        Check(shown.Count==1 && shown[0]==rows[20],"search restricts both sections to the matching client");
        win.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
            var dialog=Application.Current.Windows.Cast<Window>().Single(x=>x.Title=="Добавить в работу");
            var button=Visual<Button>(dialog).First(b=>Equals(b.Content,"Добавить"));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        Call("AddStatusRows",false,false);
        search.Text="";Refresh();
        Check(names[0]==Host(rows[20]) && !pinned.Contains(names[0]),"actual overview submission promotes a client without pinning it");
        Check(history.Count==jobs && !File.Exists(Path.Combine(AppContext.BaseDirectory,"poll-network43.txt")),"local changes create zero jobs and zero remote transports");
        QueueAndProof.Run(win,history,rows[160],rows[161],Check);
        DisabledViews.Verify(win,new[]{rows[6],rows[8]},Check);
        File.WriteAllText(result,JsonSerializer.Serialize(facts));
    }
}
