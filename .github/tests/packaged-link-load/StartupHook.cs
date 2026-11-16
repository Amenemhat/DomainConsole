using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

internal class StartupHook
{
    public static void Initialize()
    {
        var result=Environment.GetEnvironmentVariable("DC_LINK_LOAD");
        if(string.IsNullOrEmpty(result))return;
        EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler((s,_)=>{
            if(s is not Window window || window.GetType().FullName!="DomainConsole.MainWindow")return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async ()=>{
                try{await Exercise(window,result);Application.Current.Shutdown(0);}
                catch(Exception error){File.WriteAllText(result,error.ToString());Application.Current.Shutdown(1);}
            }));
        }));
    }

    static async Task Exercise(Window win,string result)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var type=win.GetType();
        var root=AppContext.BaseDirectory;
        var control=type.Assembly.GetType("DomainConsole.PollingControl43",true)!;
        object Field(string key)=>type.GetField(key,flags)!.GetValue(win)!;
        static object? Get(object instance,string key)=>instance.GetType().GetProperty(key)!.GetValue(instance);
        static void Set(object instance,string key,object value)=>instance.GetType().GetProperty(key)!.SetValue(instance,value);
        var load=type.GetMethod("PollBackgroundLinks31",flags)!;
        var read=type.GetMethod("CheckConnectionObserved47",flags)!;
        ((DispatcherTimer)Field("timer")).Stop();
        var rows=((IEnumerable)type.GetProperty("Computers")!.GetValue(win)!).Cast<object>().ToList();
        var history=(IList)type.GetProperty("History")!.GetValue(win)!;
        var links=(IDictionary)Field("clientConnections");
        var linkType=links.GetType().GetGenericArguments()[1];
        if(rows.Count!=4 || history.Count!=3) throw new Exception("Unexpected isolated fixture");
        var loss=DateTimeOffset.UtcNow;
        foreach(object job in history)
        {
            var target=((IList)Get(job,"Targets")!)[0]!;
            Set(target,"OutputReceivedUtc58",loss.AddMinutes(-1).ToString("o"));
            Set(job,"Created",loss.AddHours(-1).ToString("o"));
        }
        string Host(object row)=>(string)Get(row,"Host")!;
        var machines=rows.Take(3).ToArray();
        foreach(var row in machines)
        {
            var link=Activator.CreateInstance(linkType)!;
            var observe=linkType.GetMethod("Observe38")!;
            var start=loss.AddMinutes(-1);
            observe.Invoke(link,[true,"",start.AddSeconds(-10),start.AddSeconds(-10)]);
            foreach(var time in new[]{start,start.AddSeconds(30),loss}) observe.Invoke(link,[false,"WinRM timeout",time,time]);
            links[Host(row)]=link;
        }
        var unused=Activator.CreateInstance(linkType)!;
        Set(unused,"LastSuccess",loss);Set(unused,"LastAttempt",loss);
        links[Host(rows[3])]=unused;
        int Transports()=>File.Exists(Path.Combine(root,"poll-network43.txt"))?File.ReadAllLines(Path.Combine(root,"poll-network43.txt")).Length:0;
        await (Task)load.Invoke(win,null)!;
        if(Transports()!=0)throw new Exception("Persisted master OFF permitted a transport");
        var tests=new Dictionary<string,object>();
        var calls=machines.ToDictionary(Host,x=>new List<double>());
        var last=machines.ToDictionary(Host,x=>0);
        int maxPending=0,maxAgents=0,maxWinRm=0;
        int baseAgents=Process.GetProcessesByName("DomainConsole.Agent").Length;
        int baseWinRm=Process.GetProcessesByName("wsmprovhost").Length;
        control.GetMethod("Resume")!.Invoke(null,null);
        try
        {
            var watch=Stopwatch.StartNew();
            while(watch.Elapsed<TimeSpan.FromMinutes(6.5) && calls.Values.Any(list=>list.Count<3))
            {
                var task=(Task)load.Invoke(win,null)!;
                do
                {
                    foreach(var row in machines)
                    {
                        string host=Host(row);
                        int count=(int)Get(links[host]!,"RecoveryChecks")!;
                        if(count==last[host])continue;
                        if(count!=last[host]+1)throw new Exception("Unobserved or burst attempts on "+host);
                        calls[host].Add(watch.Elapsed.TotalSeconds);last[host]=count;
                    }
                    maxPending=Math.Max(maxPending,((IDictionary)Field("connectionReads")).Count);
                    maxAgents=Math.Max(maxAgents,Process.GetProcessesByName("DomainConsole.Agent").Length-baseAgents);
                    maxWinRm=Math.Max(maxWinRm,Process.GetProcessesByName("wsmprovhost").Length-baseWinRm);
                    await Task.Delay(130);
                }while(!task.IsCompleted);
                await task;
                await Task.Delay(500);
            }
            foreach(var (host,time) in calls)
                if(time.Count!=3 || time[0]<28 || time[1]-time[0]<58 || time[2]-time[1]<118)
                    throw new Exception("Incorrect actual elapsed retry sequence: "+host+" "+string.Join(",",time));
            int before=Transports();
            await (Task)load.Invoke(win,null)!;
            if(before!=9 || Transports()!=before || maxPending!=1 || maxAgents!=0 || maxWinRm!=0)
                throw new Exception($"Expected nine bounded checks, no additional work after the cap: transports={Transports()} high water pending={maxPending} client agents={maxAgents} WinRM provider increase={maxWinRm}");
            tests["actual_elapsed_seconds"]=calls;tests["native_powershell_transports"]=before;
            tests["max_in_flight"]=maxPending;tests["additional_client_agents"]=maxAgents;tests["additional_server_side_providers_while_disconnected"]=maxWinRm;
            // Coalesce two requests made on the UI thread before the first native transport completes.
            var first=(Task<bool>)read.Invoke(win,[rows[3],null])!;
            var second=(Task<bool>)read.Invoke(win,[rows[3],null])!;
            if(await first || await second || Transports()!=before+1)throw new Exception("Overlapping reads were not coalesced into a single failed WinRM transport");
            tests["overlapping_native_reads"]=Transports()-before;
            tests["client_jobs_after"]=history.Count;
            if(history.Count!=3)throw new Exception("Link probing created client jobs");
            var views=new List<string>();
            var wpfTabs=(System.Windows.Controls.TabControl)Field("MainTabs");
            var targetByHost=history.Cast<object>().Select(j=>((IList)Get(j,"Targets")!)[0]!)
                .ToDictionary(t=>(string)Get(t,"Host")!);
            foreach(var minutes in new[]{2,16})
            {
                var observation=DateTimeOffset.UtcNow.AddMinutes(-minutes);
                for(int index=0;index<2;index++)
                {
                    var row=machines[index];var host=Host(row);
                    var link=links[host]!;
                    Set(link,"LossConfirmedUtc",observation);
                    Set(link,"FailureObservedSince",observation.AddMinutes(-1));
                    Set(link,"LastSuccess",observation.AddMinutes(-2));
                    var target=targetByHost[host];var state=Get(target,"State")!;
                    Set(target,"OutputReceivedUtc58",observation.AddMinutes(-1).ToString("o"));
                    Set(state,"Stage",index==0?"Скачивание":"Установка");
                }
                foreach(var tab in new[]{"WsusTab","CommandsTab"})
                {
                    wpfTabs.SelectedItem=Field(tab);
                    type.GetMethod("RefreshOutcomes",flags)!.Invoke(win,null);
                    for(int index=0;index<2;index++)
                    {
                        var row=machines[index];var host=Host(row);
                        var work=type.GetMethod("WorkState60",flags)!.Invoke(win,[row,true,null])!;
                        var assessment=type.GetMethod("AssessClient41",flags)!.Invoke(win,[row])!;
                        var criteria=(IList)((IDictionary)Field("criteria45"))[host]!;
                        string expected=(string)Get(work,"Text")!;
                        string stage=index==0?"Скачивание":"Установка";
                        bool during=minutes<15;
                        if(!expected.Contains(stage)||!expected.Contains("24%")||
                            !(during?expected.Contains("временно отсутствует"):expected.Contains("последняя связь"))||
                            !Equals(Get(row,"RecentHint"),expected)||!Equals(Get(row,"NavigationOutcomeHint"),expected)||
                            !Equals(Get(assessment,"Title"),expected)||!Equals(Get(criteria[0]!,"Text"),"Обновления · "+expected)||
                            !Equals(Get(row,"LinkKind"),"Unreachable")||!Equals(Get(row,"Working"),during)||
                            !Equals(Get(row,"DisplayOutcome51"),during?(index==0?"Download":"Install"):"Unknown"))
                            throw new Exception($"Different connectivity facts after {minutes} min in {tab}: row={Get(row,"RecentHint")} glyph={Get(row,"DisplayOutcome51")} link={Get(row,"LinkKind")} card={expected} assessment={Get(assessment,"Title")} criterion={Get(criteria[0]!,"Text")}");
                        views.Add($"{stage}, {minutes} min, {tab}: {expected}");
                    }
                }
            }
            tests["card_criterion_recent_tree_and_connection"]=views;
            File.WriteAllText(result,JsonSerializer.Serialize(tests));
        }
        finally {control.GetMethod("Pause")!.Invoke(null,null);}
    }
}
