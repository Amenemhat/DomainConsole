using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DomainConsole;

// Independent of WPF so recovery can be tested before constructing any window.
public sealed class DataBackup57 {
 readonly string data, archives;
 readonly object sync;
 readonly object archiveGate=new();
 public DataBackup57(string data, string archives, object sync) { this.data=data;this.archives=archives;this.sync=sync; }
 public string ArchiveDirectory => archives;
 public sealed record Entry(string Name, string Sha256, long Length);
 public sealed record Manifest(int Format, DateTimeOffset CreatedUtc, Entry[] Files);
 public sealed record Snapshot(string Path, Manifest Info, Dictionary<string,byte[]> Files);
 static readonly JsonSerializerOptions json=new(){WriteIndented=true};
 const long MaxBytes=1024L*1024*1024;
 public static void Validate(byte[] bytes) {
  var span=bytes.AsMemory();if(bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191)span=span[3..];
  using var parsed=JsonDocument.Parse(span);
 }
 static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
 static bool SafeName(string name)=>!string.IsNullOrEmpty(name)&&name==Path.GetFileName(name)&&!name.Contains('/')&&!name.Contains('\\')&&name.EndsWith(".json",StringComparison.OrdinalIgnoreCase)&&name!="manifest.json";
 public string[] DamagedFiles() {
  Directory.CreateDirectory(data);
  return Directory.GetFiles(data,"*.json").Where(path=>{try{Validate(File.ReadAllBytes(path));return false;}catch(JsonException){return true;}catch(InvalidDataException){return true;}}).Select(Path.GetFileName).Cast<string>().ToArray();
 }
 public static void DurableReplace(string path, byte[] bytes) {
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {
   using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.WriteThrough)){stream.Write(bytes);stream.Flush(true);}
   if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
  } finally {if(File.Exists(temp))File.Delete(temp);}
 }
 public string Create(bool automatic=false) {lock(archiveGate)return CreateCore(automatic);}
 string CreateCore(bool automatic) {
  Dictionary<string,byte[]> files=new(StringComparer.OrdinalIgnoreCase);
  lock(sync) {
   Directory.CreateDirectory(data);
   foreach(var path in Directory.GetFiles(data,"*.json")) {
    var name=Path.GetFileName(path);if(!SafeName(name))throw new InvalidDataException("Недопустимое имя файла данных.");
    var bytes=File.ReadAllBytes(path);Validate(bytes);files.Add(name,bytes);
   }
  }
  if(files.Count==0)throw new InvalidOperationException("Нет сохранённых JSON для архивирования.");
  if(files.Values.Sum(x=>(long)x.Length)>MaxBytes)throw new InvalidDataException("Объём данных превышает 1 ГиБ.");
  Directory.CreateDirectory(archives);
  var manifest=new Manifest(1,DateTimeOffset.UtcNow,files.Select(x=>new Entry(x.Key,Hash(x.Value),x.Value.Length)).ToArray());
  var pathOut=Path.Combine(archives,(automatic?"auto-":"manual-")+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")[..8]+".zip");
  var temp=pathOut+".tmp";
  try {
   using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)) {
    using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)) {
     foreach(var file in files){using var entry=zip.CreateEntry("Data/"+file.Key,CompressionLevel.Optimal).Open();entry.Write(file.Value);}
     using var index=zip.CreateEntry("manifest.json").Open();JsonSerializer.Serialize(index,manifest,json);
    }
    stream.Flush(true);
   }
   _=Read(temp);File.Move(temp,pathOut);
   if(automatic)foreach(var old in Directory.GetFiles(archives,"auto-*.zip").OrderByDescending(x=>x,StringComparer.Ordinal).Skip(30))File.Delete(old);
   return pathOut;
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static Snapshot Read(string path) {
  using var zip=ZipFile.OpenRead(path);
  var index=zip.GetEntry("manifest.json")??throw new InvalidDataException("В архиве нет описания резервной копии DomainConsole.");
  if(index.Length>1024*1024)throw new InvalidDataException("Слишком большое описание архива.");
  Manifest manifest;using(var stream=index.Open())manifest=JsonSerializer.Deserialize<Manifest>(stream)??throw new InvalidDataException("Пустое описание архива.");
  if(manifest.Format!=1||manifest.Files==null||manifest.Files.Length==0||manifest.Files.Length>10000)throw new InvalidDataException("Неизвестный формат архива.");
  Dictionary<string,byte[]> files=new(StringComparer.OrdinalIgnoreCase);long total=0;
  foreach(var file in manifest.Files){
   if(file==null||!SafeName(file.Name)||file.Length<0||file.Length>MaxBytes||(total+=file.Length)>MaxBytes)throw new InvalidDataException("Недопустимое содержимое архива.");
   var entry=zip.GetEntry("Data/"+file.Name)??throw new InvalidDataException("В архиве отсутствует "+file.Name);
   if(entry.Length!=file.Length)throw new InvalidDataException("Длина файла не совпадает: "+file.Name);
   using var stream=entry.Open();using var output=new MemoryStream();stream.CopyTo(output);var bytes=output.ToArray();
   if(Hash(bytes)!=file.Sha256)throw new InvalidDataException("Контрольная сумма не совпадает: "+file.Name);
   Validate(bytes);files.Add(file.Name,bytes);
  }
  if(zip.Entries.Count!=files.Count+1)throw new InvalidDataException("Лишние или повторяющиеся записи архива.");
  return new(path,manifest,files);
 }
 public Snapshot? LatestFor(string[] damaged) {
  if(!Directory.Exists(archives))return null;
  foreach(var path in Directory.GetFiles(archives,"*.zip").OrderByDescending(File.GetLastWriteTimeUtc)) {
   try {var snapshot=Read(path);if(damaged.All(snapshot.Files.ContainsKey))return snapshot;}
   catch(Exception ex) when(ex is IOException or InvalidDataException or JsonException or ArgumentException){ /* Never use an incomplete backup. */ }
  }
  return null;
 }
 public string Restore(Snapshot snapshot,string[] names) {
  // Re-read and validate the entire archive immediately before changing any file.
  snapshot=Read(snapshot.Path);
  if(names.Length==0||names.Any(n=>!snapshot.Files.ContainsKey(n)))throw new InvalidDataException("Архив не содержит все повреждённые файлы.");
  var preserved=Path.Combine(archives,"damaged-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")[..8]);
  lock(sync){
   Directory.CreateDirectory(preserved);
   foreach(var name in names){var original=Path.Combine(data,name);if(File.Exists(original))File.Copy(original,Path.Combine(preserved,name));}
   // Provenance is written before replacement; a failed restore remains diagnosable.
   DurableReplace(Path.Combine(preserved,"recovery.txt"),Encoding.UTF8.GetBytes("Archive: "+snapshot.Path+"\nCreated UTC: "+snapshot.Info.CreatedUtc.ToString("O")+"\nRestore UTC: "+DateTimeOffset.UtcNow.ToString("O")+"\nFiles: "+string.Join(", ",names)));
   foreach(var name in names)DurableReplace(Path.Combine(data,name),snapshot.Files[name]);
  }
  return preserved;
 }
}
