using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;

internal static class RepairStopCases
{
    internal static async Task Exercise(Window window, object[] rows, Action<bool,string> check)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var type=window.GetType();
        object Field(string name)=>type.GetField(name,flags)!.GetValue(window)!;
        object? Call(string name,params object[] args)=>type.GetMethod(name,flags)!.Invoke(window,args);
        static object? Get(object item,string name)=>item.GetType().GetProperty(name)!.GetValue(item);
        static void Set(object item,string name,object value)=>item.GetType().GetProperty(name)!.SetValue(item,value);
        var hosts=rows.TakeLast(2).Select(row=>(string)Get(row,"Host")!).ToArray();
        var live=(HashSet<string>)Field("liveConnections");
        foreach(var host in hosts)live.Remove(host);
        var observations=(IDictionary)Field("identities58");
        var ledger=(IDictionary)Field("identityRepairs58");
        var permissions=(IDictionary)Field("manualIdentityScopes");
        var history=(IList)type.GetProperty("History")!.GetValue(window)!;
        int jobsBefore=history.Count;
        string shared=Guid.NewGuid().ToString();
        var identities=hosts.Select(h=>observations[h]!).ToArray();
        foreach(var identity in identities){
            Set(identity,"SusClientId",shared);Set(identity,"At",DateTimeOffset.UtcNow.ToString("o"));
        }
        string key=(string)Get(identities[0],"Key")!;
        ((HashSet<string>)Field("compromisedIds58")).Add(key);
        var member=type.GetMethod("MemberIdentity",BindingFlags.Static|BindingFlags.NonPublic)!;
        permissions[key]=identities.Select(i=>(string)member.Invoke(null,[i])!).ToList();
        string FirstKey(int i)=>key+"|"+((string)Get(identities[i],"Machine")!).ToLowerInvariant();
        var attempt=Activator.CreateInstance(ledger.GetType().GetGenericArguments()[1])!;
        string jobId=Guid.NewGuid().ToString();
        Set(attempt,"Host",hosts[0]);Set(attempt,"JobId",jobId);Set(attempt,"OldId",shared);Set(attempt,"Key",FirstKey(0));
        Set(attempt,"Phase","Передача не подтверждена");ledger.Add(FirstKey(0),attempt);
        Task Repair()=>(Task)Call("RepairIdentityGroup58",hosts[0],false)!;
        await Repair();
        check(Equals(Get(attempt,"JobId"),jobId)&&history.Count==jobsBefore,"a persisted uncertain write with the old ID still observed cannot schedule another reset");
        var deferred=ledger[FirstKey(1)]!;
        check(((string)Get(deferred,"Phase")!).Contains("Ожидается свежая проверка")&&string.IsNullOrEmpty((string?)Get(deferred,"JobId")),"an approved peer with unknown connectivity remains deferred without a submission");
        string newId=Guid.NewGuid().ToString();Set(attempt,"NewId",newId);
        var refresh=type.GetField("refreshSurvey",flags)!;
        var old=refresh.GetValue(window);
        var active=(IDictionary)Field("identityRepairStops");
        bool stopped=false;
        try{
            refresh.SetValue(window,(Action)(()=>{
                if(active[key] is CancellationTokenSource cancel&&!cancel.IsCancellationRequested){
                    Call("StopIdentityRepair58",hosts[0]);stopped=cancel.IsCancellationRequested;
                }
            }));
            await Repair();
        }finally{refresh.SetValue(window,old);}
        string scopeFile=Path.Combine(AppContext.BaseDirectory,"Data","identity-scope-approvals.json");
        using var saved=JsonDocument.Parse(File.ReadAllText(scopeFile));
        check(stopped&&((IList)permissions[key]!).Count==0&&saved.RootElement.GetProperty(key).GetArrayLength()==0,"stop during a running repair cancels the actual token, clears and persists permission for new submissions");
        await Repair();
        check(history.Count==jobsBefore&&Equals(Get(ledger[FirstKey(0)]!,"NewId"),newId)&&ReferenceEquals(ledger[FirstKey(1)],deferred),"after cancellation an accepted/new ID remains intact and the next unattended pass sends no additional reset");
    }
}
