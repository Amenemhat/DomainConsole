using System.Collections;
using System.Reflection;
using System.Windows;

internal static class RepairCases
{
    internal static void Exercise(Window win,object[] rows,bool resumed,Action<bool,string> check)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
        var type=win.GetType();
        object Field(string name)=>type.GetField(name,flags)!.GetValue(win)!;
        object? Call(string name,params object[] args)=>type.GetMethod(name,flags)!.Invoke(win,args);
        static object? Get(object o,string key)=>o.GetType().GetProperty(key)!.GetValue(o);
        static void Set(object o,string key,object? value)=>o.GetType().GetProperty(key)!.SetValue(o,value);
        var attempts=(IDictionary)Field("identityRepairs58");
        var observations=(IDictionary)Field("identities58");
        var attemptType=attempts.GetType().GetGenericArguments()[1];
        var responseType=type.Assembly.GetType("DomainConsole.IdentityServerReply58",true)!;
        var members=rows.Take(2).Select(r=>(string)Get(r,"Host")!).ToArray();
        var now=DateTimeOffset.UtcNow;
        var history=(IList)type.GetProperty("History")!.GetValue(win)!;
        int before=history.Count;
        string groupId="521562f9-cee1-4709-a658-ce3b5f1b4e81";
        var values=new List<object>();
        foreach(var host in members)
        {
            var identity=observations[host]!;
            string key="http://wsus.survey.invalid:8530|"+groupId+"|"+Get(identity,"Machine");
            if(!resumed)
            {
                object attempt=Activator.CreateInstance(attemptType)!;
                Set(attempt,"Host",host);Set(attempt,"Key",key);Set(attempt,"OldId",groupId);
                Set(attempt,"JobId",Guid.NewGuid().ToString());Set(attempt,"StartedUtc",now.AddMinutes(-11).ToString("o"));
                Set(attempt,"DeadlineUtc",now.AddMinutes(1).ToString("o"));Set(attempt,"At",now.AddMinutes(-11).ToString("o"));
                attempts.Add(key,attempt);
            }
            values.Add(attempts[key]??throw new Exception("The checkpoint lost a group member on restart"));
        }
        int index=resumed?1:0;
        object person=observations[members[index]]!;
        object reply=Activator.CreateInstance(responseType)!;
        Set(reply,"Name",Get(person,"Name"));Set(reply,"Id",Get(person,"SusClientId"));
        Set(reply,"Groups",new List<string>{"All Computers"});Set(reply,"ReportedUtc",now.AddHours(-1).ToString("o"));
        if(resumed)
        {
            check(Equals(Get(values[0],"Phase"),"Подтверждено")&&((string)Get(values[1],"Phase")!).StartsWith("Регистрация задерживается"),"second process restores a confirmed group member and a separate expired member without another job");
            check(!string.IsNullOrEmpty((string?)Get(values[0],"NewId")),"new ID and original operation survive the process restart");
        }
        bool Complete()=>(bool)Call("CompleteIdentityRegistration58",values[index],person,reply)!;
        check(!Complete(),"server record reported before the repair cannot finish a member");
        Set(reply,"ReportedUtc",now.ToString("o"));
        Set(reply,"Name","unrelated.survey.invalid");check(!Complete(),"different server name cannot finish a member");
        Set(reply,"Name",Get(person,"Name"));
        Set(person,"TargetGroupEnabled",resumed?1:0);Set(person,"TargetGroup","Expected lab group");
        check(Complete(),"late exact name, new ID and fresh post-repair report confirm this member");
        string note=(string)Get(values[index],"GroupNote")!;
        check(note.Contains(resumed?"Expected lab group":"администратора WSUS"),"server assignment or missing expected group requires a visible follow-up");
        if(!resumed)
        {
            Set(values[1],"DeadlineUtc",now.AddSeconds(-1).ToString("o"));
            Call("ExpireIdentityRepairs58");
            check(((string)Get(values[1],"Phase")!).StartsWith("Регистрация задерживается")&&Equals(Get(values[0],"Phase"),"Подтверждено"),"expiry of the second member does not change the first registration");
        }
        var events=(IEnumerable)Call("AllEvents32",members[index])!;
        check(events.Cast<object>().Any(e=>((string?)Get(e,"Text"))?.Contains("регистрация и отчёт WSUS подтверждены")==true),"member history retains the exact successful WSUS registration");
        check(history.Count==before,"result reconciliation creates no reset or installation job");
    }
}
