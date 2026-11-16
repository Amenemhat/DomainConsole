using System.Collections;
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
        string? path=Environment.GetEnvironmentVariable("DC_SURVEY_EVIDENCE");
        if(path is null)return;
        EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler((s,_)=>{
            if(s is not Window win||win.GetType().FullName!="DomainConsole.MainWindow")return;
            win.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async ()=>{
                try
                {
                    var survey=win.GetType().GetMethod("RunIdentitySurvey",BindingFlags.Instance|BindingFlags.NonPublic)!;
                    var args=survey.GetParameters();
                    var client=args[3].ParameterType.GetGenericArguments();
                    var server=args[4].ParameterType.GetGenericArguments();
                    var run=typeof(StartupHook).GetMethod(nameof(Exercise),BindingFlags.Static|BindingFlags.NonPublic)!
                      .MakeGenericMethod(client[0],client[2].GetGenericArguments()[0],server[0],server[3].GetGenericArguments()[0]);
                    await (Task)run.Invoke(null,[win,path,survey])!;
                    Application.Current.Shutdown(0);
                }
                catch(Exception e){File.WriteAllText(path,e.ToString());Application.Current.Shutdown(1);}
            }));
        }));
    }
    static IEnumerable<T> Visual<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            if(child is T match)yield return match;
            foreach(var down in Visual<T>(child))yield return down;
        }
    }
    static async Task Exercise<TTarget,TIdentity,TSettings,TReply>(Window win,string path,MethodInfo run) where TIdentity:new() where TReply:new()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var type=win.GetType();
        var rows=((IEnumerable)type.GetProperty("Computers")!.GetValue(win)!).Cast<object>().ToArray();
        var history=(IList)type.GetProperty("History")!.GetValue(win)!;
        object? Field(string name)=>type.GetField(name,flags)!.GetValue(win);
        object? Call(string name,params object[] a)=>type.GetMethod(name,flags)!.Invoke(win,a);
        static object? Get(object o,string key)=>o.GetType().GetProperty(key)!.GetValue(o);
        static void Set(object o,string key,object? value)=>o.GetType().GetProperty(key)!.SetValue(o,value);
        string Host(object o)=>(string)Get(o,"Host")!;
        var facts=new List<string>();
        void Check(bool ok,string what){if(!ok)throw new Exception(what);facts.Add(what);}
        var grid=(DataGrid)win.FindName("ComputerGrid");
        var name=Host(rows[0]);
        Window Dialog()=>(Window)Field("surveyWindow")!;
        void Open(object row)=>Call("OpenIdentitySurvey",row);
        Button Button(Window window,string content)=>Visual<Button>(window).Single(b=>Equals(b.Content,content));
        void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        string Combined(Window w)=>string.Join("\n",Visual<TextBlock>(w).Select(t=>t.Text));
        string Dat(string key)=>Path.Combine(AppContext.BaseDirectory,"Data",key);
        int originalJobs=history.Count;
        if(Environment.GetEnvironmentVariable("DC_SURVEY_RESTART")=="1")
        {
            var restored=Field("survey")!;
            Check((string)Get(restored,"Owner")! == name && ((IDictionary)Get(restored,"Results")!).Count==6,"all six findings restored from Data on a separate application process");
            Open(rows[0]);var d=Dialog();d.UpdateLayout();
            Check(Visual<DataGrid>(d).Single().Items.Count==6&&Combined(d).Contains("Серверный ID не проверен"),"reopened domain result displays the saved server outage and six actual survey rows");
            Check(history.Count==originalJobs && !File.Exists(Path.Combine(AppContext.BaseDirectory,"poll-network43.txt")),"opening saved diagnostics with master OFF creates no network transport");
            RepairCases.Exercise(win,rows,true,Check);d.Close();File.WriteAllText(path,JsonSerializer.Serialize(facts));return;
        }
        string shared=Guid.NewGuid().ToString(),mode="cancel";
        var hardware=rows.ToDictionary(Host,_=>Guid.NewGuid().ToString());
        var different=rows.ToDictionary(Host,_=>Guid.NewGuid().ToString());
        var reads=new List<string>();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<TIdentity> Client(TTarget target,CancellationToken token)
        {
            var host=Host(target!);reads.Add(host);
            if(mode=="cancel"&&host==Host(rows[2])){entered.TrySetResult();await Task.Delay(Timeout.InfiniteTimeSpan,token);}
            if(mode!="distinct"&&host==Host(rows[4]))throw new IOException("isolated WinRM timeout");
            var value=new TIdentity();
            Set(value!,"Host",host);Set(value!,"Name",host);Set(value!,"Machine",hardware[host]);Set(value!,"At",DateTimeOffset.UtcNow.ToString("o"));
            Set(value!,"Server","http://wsus.survey.invalid:8530");Set(value!,"UseWUServer",1);
            Set(value!,"SusClientId",mode=="distinct"?different[host]:host==Host(rows[5])?"":shared);
            return value;
        }
        Task<TReply> Server(TSettings _,string id,CancellationToken token)
        {
            if(mode=="distinct")throw new IOException("isolated WSUS endpoint unavailable");
            var reply=new TReply();Set(reply!,"Id",id);Set(reply!,"Name",name);return Task.FromResult(reply);
        }
        Task Run(bool resume)=>(Task)run.Invoke(win,[rows[0],true,resume,(Func<TTarget,CancellationToken,Task<TIdentity>>)Client,(Func<TSettings,string,CancellationToken,Task<TReply>>)Server,false])!;
        var task=Run(false);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Open(rows[0]);var dialog=Dialog();dialog.UpdateLayout();dialog.Close();
        grid.SelectedItem=rows[3];Open(rows[3]);dialog=Dialog();dialog.UpdateLayout();
        Check(dialog.Title.Contains((string)Get(rows[0],"Name")!) && grid.SelectedItem==rows[3]&&!task.IsCompleted,"closing and reopening an in-flight domain survey preserves its owner and the main selection");
        Click(Button(dialog,"Остановить"));await task;
        var stopped=Field("survey")!;
        Check(((IDictionary)Get(stopped,"Results")!).Count==2 && ((string)Get(stopped,"ScopeMessage")!).Contains("приостановлен"),"stop preserves two completed reads and schedules no successors");
        mode="resume";await Run(true);dialog.UpdateLayout();
        var results=(IDictionary)Get(Field("survey")!,"Results")!;
        Check(results.Count==6 && reads.Count(x=>x==name)==1 && reads.Count(x=>x==Host(rows[1]))==1,"resume reads only the remaining hosts across both OUs");
        Check(((string)results[Host(rows[4])]!).Contains("Не удалось")&&((string)results[Host(rows[5])]!).Contains("некорректен"),"unavailable and invalid-ID candidates remain visible with distinct reasons");
        var heading=Combined(dialog);
        Check(heading.Contains("без подтверждения 2")&&heading.Contains("Одинаковый SusClientId")&&heading.Contains(Host(rows[3])),"the real window separates the four confirmed devices from the two unread or invalid candidates");
        Check(history.Count==originalJobs && ((IDictionary)Field("identityRepairs58")!).Count==0,"master OFF with automatic servicing enabled permits manual diagnosis and creates zero repair jobs");
        mode="distinct";await Run(false);dialog.UpdateLayout();heading=Combined(dialog);
        Check(heading.Contains("Идентификаторы проверенных устройств различаются")&&heading.Contains("Серверный ID не проверен"),"distinct client IDs and an unavailable server have independent visible findings");
        var table=Visual<DataGrid>(dialog).Single();
        Check(table.Items.Count==6&&File.Exists(Dat("identity-survey.json")),"all six new observations saved with the UI still open");
        RepairCases.Exercise(win,rows,false,Check);dialog.Close();File.WriteAllText(path,JsonSerializer.Serialize(facts));
    }
}
