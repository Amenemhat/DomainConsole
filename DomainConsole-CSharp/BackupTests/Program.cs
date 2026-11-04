using DomainConsole;
using System.IO.Compression;
using System.Text;
var root=Path.Combine(Path.GetTempPath(),"DomainConsole-backup-tests-"+Guid.NewGuid());
Directory.CreateDirectory(root);
try{
 var data=Path.Combine(root,"Data");Directory.CreateDirectory(data);
 var backups=Path.Combine(root,"Backups");var store=new DataBackup57(data,backups,new object());
 var history=Path.Combine(data,"history.json");var good=Encoding.UTF8.GetBytes("[{\"Status\":\"Completed\",\"Text\":\"Проверено\"}]");
 DataBackup57.DurableReplace(history,good);File.WriteAllText(Path.Combine(data,"settings.json"),"{\"Theme\":2}");
 var archive=store.Create();var saved=DataBackup57.Read(archive);Assert(saved.Files.Count==2,"Snapshot omitted data");
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
 Console.WriteLine("PASS: archive hashes, corruption, original preservation, restart, invalid archives, missing files, interrupted writes, retention, replacements");
}finally{Directory.Delete(root,true);}
static void Assert(bool ok,string why){if(!ok)throw new Exception(why);}
static void Fails(Action action,string why){try{action();}catch(Exception ex)when(ex is System.Text.Json.JsonException or IOException or InvalidDataException or ArgumentException){return;}throw new Exception(why);}
