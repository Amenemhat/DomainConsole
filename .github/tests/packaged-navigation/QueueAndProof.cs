using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Windows;

internal static class QueueAndProof
{
    internal static void Run(Window window, IList history, object row, object peer, Action<bool,string> check)
    {
        const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        var win=window.GetType();
        object Field(string name)=>win.GetField(name,flags)!.GetValue(window)!;
        object? Call(string name,params object[] values)=>win.GetMethod(name,flags)!.Invoke(window,values);
        static object? Value(object obj,string name)=>obj.GetType().GetProperty(name)!.GetValue(obj);
        static void Set(object obj,string name,object? value)=>obj.GetType().GetProperty(name)!.SetValue(obj,value);
        string host=(string)Value(row,"Host")!,other=(string)Value(peer,"Host")!;
        var queue=Field("queue");
        var pair=queue.GetType().GetGenericArguments()[0];
        var types=pair.GetGenericArguments();
        var watches=(IDictionary)Field("rebootWatches");
        var watch=Activator.CreateInstance(watches.GetType().GetGenericArguments()[1])!;
        object Job(string name,string target,string state)
        {
            var id=Guid.NewGuid().ToString();
            var fixture=new {Id=id,Name=name,Created=DateTimeOffset.UtcNow.ToString("o"),OutputChannel="Commands",
                  Targets=new[]{new {Host=target,Name=target,Status=state,Output=state=="Submitted"?"accepted remotely":""}}};
            var job=JsonSerializer.Deserialize(JsonSerializer.Serialize(fixture),types[0])!;
            history.Insert(0,job);
            return job;
        }
        object Target(object job)=>((IList)Value(job,"Targets")!)[0]!;
        void Enqueue(object job)=>queue.GetType().GetMethod("Enqueue")!.Invoke(queue,new[]{Activator.CreateInstance(pair,job,Target(job))});
        bool Held(object job)=>(bool)Call("HoldForHeaderReboot",job,Target(job))!;
        var first=Job("old command",host,"Queued");
        var sent=Job("already accepted",host,"Submitted");
        var second=Job("other client's command",other,"Queued");
        var follow=Job("validated follow-up",host,"Queued");
        var reboot=Job("Перезагрузка текущего компьютера",host,"Submitted");
        Set(watch,"JobId",Value(reboot,"Id"));
        Set(watch,"FollowupId",Value(follow,"Id"));
        watches[host]=watch;
        Enqueue(first);Enqueue(second);Enqueue(follow);
        check(Held(first)&&!Held(reboot)&&!Held(second)&&!Held(follow),"reboot pauses only ordinary work on the selected PC; the second PC and authorized follow-up remain eligible");
        Set(watch,"Done",true);
        check(!Held(first)&&Equals(Value(Target(first),"Status"),"Queued"),"confirmed cancellation releases the original queued item");
        Set(watch,"Done",false);
        Call("RetireQueuedAfterBoot",host,watch);
        check(Equals(Value(Target(first),"Status"),"InterventionRequired") && Equals(Value(Target(second),"Status"),"Queued")
          && Equals(Value(Target(follow),"Status"),"Queued") && Equals(Value(Target(sent),"Status"),"Submitted")
          && Equals(Value(Target(sent),"Output"),"accepted remotely"),"new boot retires unsubmitted commands only and preserves the accepted command, independent client and follow-up");
        var remaining=((IEnumerable)queue).Cast<object>().Select(x=>pair.GetField("Item1")!.GetValue(x)).ToList();
        check(remaining.SequenceEqual(new[]{second,follow}),"queue order preserved for unaffected commands");
        queue.GetType().GetMethod("Clear")!.Invoke(queue,null);watches.Remove(host);
        foreach(var item in new[]{first,sent,second,follow,reboot}) history.Remove(item);

        var snapshots=(IDictionary)Field("rebootSnapshots");
        var snap=Activator.CreateInstance(snapshots.GetType().GetGenericArguments()[1])!;
        var stamp=DateTimeOffset.UtcNow.AddDays(-8).ToString("o");
        Set(snap,"CollectedUtc",stamp);Set(snap,"Required",true);Set(snap,"Sources",new List<string>{"CBS"});
        snapshots[host]=snap;
        object View()=>Call("RebootViewFor40",row)!;
        bool Tag()=>((IEnumerable<string>)Call("StatusTags",row)!).Contains("Reboot");
        var expired=View();
        check(Value(expired,"Required") is null && Equals(Value(expired,"Stamp"),stamp) && !Tag(),"eight-day reboot requirement excluded from current flags and filter, with original date retained");
        Call("ConnectionSucceeded",host,DateTimeOffset.UtcNow,true);
        check(Value(View(),"Required") is null && Equals(Value(View(),"Stamp"),stamp) && !Tag(),"fresh authenticated transport alone does not renew a historical reboot flag");
        Set(snap,"CollectedUtc",DateTimeOffset.UtcNow.ToString("o"));
        check(Equals(Value(View(),"Required"),true)&&Tag(),"fresh positive client flags immediately restore the reboot requirement and filter");
        Set(snap,"Required",false);
        check(Equals(Value(View(),"Required"),false)&&!Tag(),"fresh negative flags clear the current reboot requirement");
        snapshots.Remove(host);
    }
}
