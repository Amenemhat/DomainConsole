using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class RemoteDataException:IOException {public RemoteDataException(string message,Exception? inner=null):base(message,inner){}}
public sealed partial class RemoteService {
 internal static T ParseData34<T>(string json,string operation){try{return JsonSerializer.Deserialize<T>(json.TrimStart('\uFEFF'),Store.Options)??throw new JsonException("Пустое значение JSON");}catch(JsonException ex){throw new RemoteDataException(operation+": некорректный снимок JSON; размер "+json.Length+" символов; строка "+ex.LineNumber+", позиция "+ex.BytePositionInLine+". "+ex.Message,ex);}}
 public const string PollScript34="""
$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';$warnings=@()
function Read-DcJson34([string]$file){
 if(!(Test-Path -LiteralPath $file)){return $null}
 for($attempt=0;$attempt -lt 3;$attempt++){$reader=$null;try{
 $stream=New-Object IO.FileStream($file,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete));$reader=New-Object IO.StreamReader($stream)
 if($stream.Length -gt 8388608){throw 'Снимок превышает лимит 8 МБ'};$body=$reader.ReadToEnd();if([string]::IsNullOrWhiteSpace($body)){throw 'Пустой снимок'}
 return ($body|ConvertFrom-Json -ErrorAction Stop)
 }catch{if($attempt -eq 2){throw ('DC_DATA_ERROR|'+(Split-Path $file -Leaf)+'|'+$_.Exception.Message)};Start-Sleep -Milliseconds 150}finally{if($reader){$reader.Dispose()}}}
}
$state=Read-DcJson34 (Join-Path $p 'status.json');if($state -and [string]$state.Id -ne $dcJobId34){throw 'DC_DATA_ERROR|status.json|Идентификатор снимка не совпадает с заданием'}
function Read-DcTail34([string]$file,[int]$limit){
 $stream=$null;try{$stream=New-Object IO.FileStream($file,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete));$count=[int][Math]::Min($stream.Length,$limit);$null=$stream.Seek(-$count,[IO.SeekOrigin]::End);$buffer=New-Object byte[] $count;$read=0;while($read -lt $count){$part=$stream.Read($buffer,$read,$count-$read);if($part -le 0){break};$read+=$part};$text=[Text.Encoding]::UTF8.GetString($buffer,0,$read).TrimStart([char]0xFEFF);return [string]::Join([Environment]::NewLine,@($text -split '\r?\n'|Select-Object -Last 80))}finally{if($stream){$stream.Dispose()}}
}
$text='';$files=@();$remaining=65536
if($includeOutput34 -or ($state -and $state.Ended)){
 foreach($file in @(Get-ChildItem -LiteralPath $p -File -ErrorAction SilentlyContinue|Where-Object {$_.Name -match '\.(log|out|err)$'}|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 16)){
  if($remaining -le 0){break};try{$body=Read-DcTail34 $file.FullName $remaining}catch{$warnings+=('Не удалось прочитать '+$file.Name+': '+$_.Exception.Message);continue};if($body.Length -gt $remaining){$body=$body.Substring($body.Length-$remaining)};$remaining-=$body.Length;$files+=@{Name=$file.Name;Body=$body};$text+=('--- '+$file.Name+' ---'+[Environment]::NewLine+$body+[Environment]::NewLine)
 }
 if($remaining -le 0){$text+='Краткий опрос ограничен 64 КБ. Полные исходные журналы доступны кнопкой «Журналы».'}
}
$taskState='Unknown';$taskResult=$null;$alive=$true
try{$task=Get-ScheduledTask -TaskName ('DomainConsoleCSharp-'+$dcJobId34) -ErrorAction SilentlyContinue;$taskState='Missing';if($task){$taskState=[string]$task.State;$taskResult=(Get-ScheduledTaskInfo -InputObject $task -ErrorAction Stop).LastTaskResult};$alive=@(Get-CimInstance Win32_Process -Filter "Name='DomainConsole.Agent.exe'"|Where-Object {$_.CommandLine -and $_.CommandLine.IndexOf($p,[StringComparison]::OrdinalIgnoreCase) -ge 0}).Count -gt 0}catch{$taskState='Unknown';$warnings+='Проверка исполнителя недоступна: '+$_.Exception.Message}
$updates=@();foreach($file in @(Get-ChildItem -LiteralPath $p -Filter '*.updates.json' -ErrorAction SilentlyContinue)){try{foreach($item in @(Read-DcJson34 $file.FullName)){if($null -ne $item){$updates+=$item}}}catch{$warnings+=$_.Exception.Message}}
$client=$null;try{$client=Read-DcJson34 (Join-Path $p 'delivery-client.json')}catch{$warnings+=$_.Exception.Message}
if($client){$quality=@(Get-ChildItem -LiteralPath $p -Filter '*.quality.log');$etl=@(Get-ChildItem -LiteralPath $p -Filter '*.WindowsUpdate.log');$client|Add-Member NoteProperty EtlStatus $(if($quality.Count){'Неполный / недоступен'}elseif($etl.Count){'Получен'}else{'Не получен'})}
$boot='';try{$boot=(Get-CimInstance Win32_OperatingSystem -ErrorAction Stop).LastBootUpTime.ToUniversalTime().ToString('o')}catch{}
$plan=$null;try{$plan=Read-DcJson34 (Join-Path $p 'reboot-plan.json')}catch{$warnings+=$_.Exception.Message}
if($warnings.Count){$text+=([Environment]::NewLine+'Ограничения дополнительного чтения: '+($warnings -join '; '))}
@{RebootPlan=$plan;ClientUtc=[DateTime]::UtcNow.ToString('o');BootUtc=$boot;Client=$client;State=$state;Output=$text;Files=@($files);Updates=@($updates);TaskState=$taskState;TaskResult=$taskResult;AgentAlive=$alive}|ConvertTo-Json -Depth 25 -Compress
""";
}
public partial class MainWindow {
 sealed class ReadFailure34 {public int Count;public DateTimeOffset First,Last,Next;public string Error="";}
 readonly Dictionary<string,ReadFailure34> readFailures34=new(StringComparer.OrdinalIgnoreCase);
 static bool IsReadFailure34(Exception ex)=>ex is RemoteDataException||Regex.IsMatch(ex.Message,@"(?i)DC_DATA_ERROR|JSON|ConvertFrom|недопустимый примитив|OutOfMemory|недостаточно.*памят|файл подкачки|нехват.*ресурс");
 bool ReadDue34(string key)=>!readFailures34.TryGetValue(key,out var failure)||DateTimeOffset.UtcNow>=failure.Next;
 void ReadFailed34(string host,string operation,Exception ex){string key=host+"|"+operation;var failure=readFailures34.GetValueOrDefault(key)??new(){First=DateTimeOffset.UtcNow};failure.Count++;failure.Last=DateTimeOffset.UtcNow;failure.Next=failure.Last.AddSeconds(Math.Min(300,30*Math.Pow(2,Math.Min(4,failure.Count-1))));failure.Error=ex.Message;readFailures34[key]=failure;string headline="Не удалось прочитать "+operation+"; предыдущие данные сохранены";statusLimitations[host]=headline+" · повторов: "+failure.Count+" · повтор после "+failure.Next.ToLocalTime().ToString("HH:mm:ss");Event31(host,"read-"+operation,ex.Message+"|"+failure.Count,headline,"Warning","Операция: "+operation+"\n"+ex.Message);var folder=Path.Combine(Store.Root,"Diagnostics");Directory.CreateDirectory(folder);string name=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));Store.Write("Diagnostics/read-"+name+".json",new{Operation=operation,Count=failure.Count,First=failure.First,Last=failure.Last,Next=failure.Next,Error=ex.ToString()});}
 void ReadSucceeded34(string host,string operation){if(readFailures34.Remove(host+"|"+operation)){Event31(host,"read-"+operation,"recovered","Чтение «"+operation+"» восстановлено","Success");if(!readFailures34.Keys.Any(k=>k.StartsWith(host+"|",StringComparison.OrdinalIgnoreCase)))statusLimitations.Remove(host);}}
}

public static partial class PowerShellBridge {
 static readonly object transportLogGate34=new();static int transportLogSlot34;
 static void SaveTransportError34(string error,string output){try{lock(transportLogGate34){var folder=Path.Combine(Store.Root,"Diagnostics");Directory.CreateDirectory(folder);static string Cap(string s)=>s.Length>32768?s[..32768]+"\n… исходный ответ ограничен":s;File.WriteAllText(Path.Combine(folder,"transport-"+(transportLogSlot34++%16)+".txt"),DateTimeOffset.Now.ToString("o")+"\nSTDERR:\n"+Cap(error)+"\nSTDOUT:\n"+Cap(output));}}catch{}}
}
