using System.Collections;
using System.Reflection;

internal static class IdentityCases
{
    internal static void Run(Assembly app, Action<bool,string> check)
    {
        var type=app.GetType("DomainConsole.ClientIdentity58", true)!;
        var policy=app.GetType("DomainConsole.IdentityPolicy58",true)!;
        var members=policy.GetMethod("Participants")!;
        var confirmed=policy.GetMethod("Confirmed")!;
        var verify=policy.GetMethod("RegistrationConfirmed")!;
        var independent=policy.GetMethod("IndependentNewId")!;
        var now=DateTimeOffset.UtcNow;
        string shared=Guid.NewGuid().ToString(), source="http://wsus.lab.invalid:8530";
        object Identity(string name,string clientId,string hardware,string server,DateTimeOffset time)
        {
            var item=Activator.CreateInstance(type)!;
            foreach(var (key,value) in new[] {("Host",name),("Name",name),("Machine",hardware),("SusClientId",clientId),("Server",server),("At",time.ToString("o"))})
                type.GetProperty(key)!.SetValue(item,value);
            return item;
        }
        string Field(object item,string name)=>(string)type.GetProperty(name)!.GetValue(item)!;
        foreach(int n in new[]{2,3,5,7})
        {
            var list=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
            for(int i=0;i<n;i++)list.Add(Identity($"pc{i}.lab.invalid",shared,Guid.NewGuid().ToString(),source,now));
            var first=list[0]!;
            list.Add(Identity(Field(first,"Name"),shared,Field(first,"Machine"),source,now));
            list.Add(Identity("other.lab.invalid",shared,Guid.NewGuid().ToString(),"http://other.lab.invalid:8530",now));
            list.Add(Identity("old.lab.invalid",shared,Guid.NewGuid().ToString(),source,now.AddMinutes(-10)));
            list.Add(Identity("empty.lab.invalid",shared,Guid.Empty.ToString(),source,now));
            var actual=(IList)members.Invoke(null,[list,first,now])!;
            check((bool)confirmed.Invoke(null,[list,first,now])!&&actual.Count==n, $"WSUS: {n} separate devices; aliases, foreign server, empty hardware and expired reading excluded");
            list.Add(Identity("renamed.lab.invalid",shared,Field(first,"Machine"),source,now));
            check(((IList)members.Invoke(null,[list,first,now])!).Count==n-1,"Duplicate hardware under a different name is ambiguous");
        }
        string newId=Guid.NewGuid().ToString(),name1="new.lab.invalid";
        var current=Identity(name1,newId,Guid.NewGuid().ToString(),source,now);
        bool Valid(string id,string host,DateTimeOffset date)=>(bool)verify.Invoke(null,[current,shared,now.AddMinutes(-1).ToString("o"),id,host,date.ToString("o"),now])!;
        check(Valid(newId,name1,now),"WSUS: fresh exact ID+name+report after repair");
        check(!Valid(shared,name1,now)&&!Valid(newId,"another.lab.invalid",now)&&!Valid(newId,name1,now.AddMinutes(-2)),"Wrong ID, name or report from before repair cannot confirm registration");
        var conflict=Array.CreateInstance(type,2);
        conflict.SetValue(current,0);
        conflict.SetValue(Identity("collision.lab.invalid",newId,Guid.NewGuid().ToString(),source,now),1);
        check(!(bool)independent.Invoke(null,[conflict,current,now])!,"A newly created ID shared by a different device cannot be confirmed");
    }
}
