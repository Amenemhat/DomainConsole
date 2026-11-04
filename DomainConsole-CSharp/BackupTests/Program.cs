using DomainConsole;
using System.IO.Compression;
using System.Text;
var root=Path.Combine(Path.GetTempPath(),"DomainConsole-backup-tests-"+Guid.NewGuid());
Directory.CreateDirectory(root);
try{
 var writes58=new LatestWrite58();var oldTicket58=writes58.Reserve();var newTicket58=writes58.Reserve();var saved58="";
 Assert(writes58.Commit(newTicket58,()=>saved58="manual"),"Current report write skipped");
 Assert(!writes58.Commit(oldTicket58,()=>saved58="old batch")&&saved58=="manual","Delayed overview overwrote newer manual report");
 var finalTicket58=writes58.Reserve();Assert(writes58.Commit(finalTicket58,()=>saved58="next batch")&&saved58=="next batch","Later overview failed to persist");
 var data=Path.Combine(root,"Data");Directory.CreateDirectory(data);
 var backups=Path.Combine(root,"Backups");var store=new DataBackup57(data,backups,new object());
 var history=Path.Combine(data,"history.json");var good=Encoding.UTF8.GetBytes("[{\"Status\":\"Completed\",\"Text\":\"Проверено\"}]");
 DataBackup57.DurableReplace(history,good);File.WriteAllText(Path.Combine(data,"settings.json"),"{\"Theme\":2}");
 Directory.CreateDirectory(Path.Combine(data,"Events","fixture"));File.WriteAllText(Path.Combine(data,"Events","fixture","2026-01-01.jsonl"),"{\"Text\":\"event\"}\n");
 var archive=store.Create();var saved=DataBackup57.Read(archive);Assert(saved.Files.Count==3,"Snapshot omitted data");
 Assert(saved.Files.ContainsKey("Events/fixture/2026-01-01.jsonl"),"Client event history omitted");
 var damaged=new byte[8000000];Array.Copy(good,damaged,good.Length);File.WriteAllBytes(history,damaged);
 Assert(store.DamagedFiles().SequenceEqual(new[]{"history.json"}),"Zero-filled tail not detected");
 Fails(()=>store.Create(),"Corrupt snapshot accepted");Assert(Directory.GetFiles(backups,"*.zip").Length==1,"Invalid snapshot replaced backup");
 var candidate=store.LatestFor(new[]{"history.json"})!;var preserved=store.Restore(candidate,new[]{"history.json"});
 Assert(File.ReadAllBytes(Path.Combine(preserved,"history.json")).SequenceEqual(damaged),"Original corrupted bytes lost");
 Assert(File.ReadAllBytes(history).SequenceEqual(good),"Restore differs from original");Assert(store.DamagedFiles().Length==0,"Restart still sees damaged data");
 Assert(File.ReadAllText(Path.Combine(data,"settings.json"))=="{\"Theme\":2}","Unrelated file changed");
 // A corrupted new archive must not hide the previous valid one.
 var badZip=Path.Combine(backups,"broken.zip");File.WriteAllText(badZip,"broken");
 Assert(store.LatestFor(new[]{"history.json"})?.Path==archive,"Invalid newest archive used");
 var altered=Path.Combine(backups,"altered.zip");File.Copy(archive,altered);
 using(var zip=ZipFile.Open(altered,ZipArchiveMode.Update)){zip.GetEntry("Data/history.json")!.Delete();using var writer=new StreamWriter(zip.CreateEntry("Data/history.json").Open());writer.Write("[]");}
 Fails(()=>DataBackup57.Read(altered),"Hash mismatch accepted");
 Fails(()=>store.Restore(saved,new[]{"missing.json"}),"Missing archive member accepted");
 Assert(File.ReadAllBytes(history).SequenceEqual(good),"Rejected restore changed data");
 // Interrupted writes leave the last destination intact and are not archived as JSON.
 File.WriteAllText(history+".interrupted.tmp","[");Assert(store.DamagedFiles().Length==0,"Temporary file treated as live data");
 for(int i=0;i<32;i++)store.Create(true);
 Assert(Directory.GetFiles(backups,"auto-*.zip").Length==30,"Automatic retention not bounded");Assert(File.Exists(archive),"Manual backup removed by retention");
 // Competing replacements use unique temporary files and always leave complete JSON.
 Parallel.For(0,40,i=>{lock(store)DataBackup57.DurableReplace(history,Encoding.UTF8.GetBytes("["+i+"]"));});
 Assert(store.DamagedFiles().Length==0,"Concurrent writes corrupted JSON");
 var events58=Path.Combine(root,"events58");Directory.CreateDirectory(events58);
 var eventFile58=Path.Combine(events58,"2026-01-01.jsonl");var now58=DateTimeOffset.UtcNow;
 string Event58(string op,int age,string text,string kind="Info")=>System.Text.Json.JsonSerializer.Serialize(new{Operation36=op,At=now58.AddDays(-age).ToString("o"),Text=text,Kind=kind,Details=""});
 File.WriteAllLines(eventFile58,new[]{Event58("poll42",40,"expired"),Event58("update-installed54",100,"installed"),Event58("job-service50|active",200,"active"),Event58("identity58",200,"conflict","Warning"),Event58("poll42",0,"one"),Event58("poll42",1,"two"),Event58("poll42",2,"three"),"malformed"});
 HistoryRetention58.Compact(events58,new(){OrdinaryCount=2},now58,new HashSet<string>{"active"},true);
 var retained58=File.ReadAllText(eventFile58);
 Assert(!retained58.Contains("expired")&&!retained58.Contains("three"),"Ordinary retention failed");
 Assert(retained58.Contains("installed")&&retained58.Contains("active")&&retained58.Contains("conflict")&&retained58.Contains("malformed"),"Retention lost protected evidence");
 var once58=File.ReadAllBytes(eventFile58);HistoryRetention58.Compact(events58,new(){OrdinaryCount=2},now58,new HashSet<string>{"active"},true);Assert(once58.SequenceEqual(File.ReadAllBytes(eventFile58)),"Retention is not idempotent");
 var shared58=Guid.NewGuid().ToString();var a58=new ClientIdentity58{Host="a",Name="a.example.invalid",Machine=Guid.NewGuid().ToString(),Server="http://wsus.invalid:8530",SusClientId=shared58,At=now58.ToString("o")};
 var b58=new ClientIdentity58{Host="b",Name="b.example.invalid",Machine=Guid.NewGuid().ToString(),Server=a58.Server,SusClientId=shared58,At=now58.ToString("o")};
 Assert(IdentityPolicy58.Confirmed(new[]{a58,b58},a58,now58),"Fresh shared ID not detected");
 b58.Machine=a58.Machine;Assert(!IdentityPolicy58.Confirmed(new[]{a58,b58},a58,now58),"Alias/rename treated as two machines");
 b58.Machine=Guid.NewGuid().ToString();b58.At=now58.AddDays(-1).ToString("o");Assert(!IdentityPolicy58.Confirmed(new[]{a58,b58},a58,now58),"Stale identity triggered repair");
 b58.At=now58.ToString("o");b58.Server="http://other.invalid:8530";Assert(!IdentityPolicy58.Confirmed(new[]{a58,b58},a58,now58),"Different WSUS servers mixed");
 var cap58=new HistoryLimits58{OutputMiB=1,TotalOutputMiB=2};
 var expired58=OutputRetention58.Expired(new[]{new OutputCandidate58("new","a",now58,800000),new OutputCandidate58("old","a",now58.AddDays(-1),800000),new OutputCandidate58("other","b",now58,800000),new OutputCandidate58("expired","b",now58.AddDays(-15),1)},now58,cap58);
 Assert(expired58.SetEquals(new[]{"old","expired"}),"Output age/host quota selection failed");
 Console.WriteLine("PASS: archive hashes, corruption, original preservation, restart, invalid archives, missing files, interrupted writes, retention, replacements");
}finally{Directory.Delete(root,true);}
static void Assert(bool ok,string why){if(!ok)throw new Exception(why);}
static void Fails(Action action,string why){try{action();}catch(Exception ex)when(ex is System.Text.Json.JsonException or IOException or InvalidDataException or ArgumentException){return;}throw new Exception(why);}
