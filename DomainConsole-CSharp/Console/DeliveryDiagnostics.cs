using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ReportQueueProbe {public string Name {get;set;}="";public int Files {get;set;}public long Bytes {get;set;}public string OldestUtc {get;set;}="";}
public sealed class ClientReportProbe {
 public string EtlStatus {get;set;}="Не получен";public List<ReportQueueProbe> Queues {get;set;}=new();public string QueueError {get;set;}="";
 public string Computer {get;set;}="";public string Os {get;set;}="";public string Build {get;set;}="";public string WUServer {get;set;}="";public string WUStatusServer {get;set;}="";public int UseWUServer {get;set;}public string SusClientId {get;set;}="";public string[] IPs {get;set;}=Array.Empty<string>();public string CollectedUtc {get;set;}="";
}
public sealed class HttpReportRequest {public string Method {get;set;}="";public string Utc {get;set;}="";public string IP {get;set;}="";public string Path {get;set;}="";public int Status {get;set;}public string SubStatus {get;set;}="";public string Win32 {get;set;}="";public string Milliseconds {get;set;}="";}
public sealed class ServerReportEvidence {
 public string Error {get;set;}="";public string SinceUtc {get;set;}="";public string UntilUtc {get;set;}="";public string LogDirectory {get;set;}="";public string PoolState {get;set;}="";public bool Complete {get;set;}public List<HttpReportRequest> Requests {get;set;}=new();public WsusReport? Report {get;set;}public string Events {get;set;}="";public string HttpErrors {get;set;}="";public string ValidationErrors {get;set;}="";public string ServerLog {get;set;}="";public bool IisRead {get;set;}public bool EventsRead {get;set;}public bool HttpErrorsRead {get;set;}public bool ProcessingLogRead {get;set;}
}
public static class DeliveryPresentation {
 public static string Format(ClientReportProbe client,ServerReportEvidence server,DateTimeOffset now){
  var reporting=server.Requests.Where(r=>r.Method=="POST"&&r.Path.Contains("ReportingWebService",StringComparison.OrdinalIgnoreCase)).ToList();
  var failures=reporting.Where(r=>r.Status>=400&&!(r.Status==401&&reporting.Any(after=>after.IP==r.IP&&after.Utc.CompareTo(r.Utc)>=0&&after.Status>=200&&after.Status<300))).ToList();
  bool hasReport=DateTimeOffset.TryParse(server.Report?.LastReportedUtc,out var reported)&&reported.Year>2000;
  bool recent=hasReport&&now-reported<=TimeSpan.FromHours(24);
  bool fresh=server.Report?.Fresh(client.CollectedUtc)==true;
  var oldQueues=client.Queues.Where(q=>q.Files>0&&DateTimeOffset.TryParse(q.OldestUtc,out var oldest)&&now-oldest>=TimeSpan.FromHours(24)).ToList();
  bool backlog=!recent&&hasReport&&oldQueues.Count>0&&client.QueueError.Length==0;
  string finding=fresh?"Отчётность работает: WSUS получил отчёт после сбора данных клиента. Точная связь с нашим запросом не доказана.":
   backlog?"Отчётность задерживается: отчёт WSUS старше 24 часов, в очереди остаются события старше 24 часов.":
   server.ValidationErrors.Length>0?"WSUS отклонил событие клиента: "+server.ValidationErrors:
   failures.Count>0?"Зафиксированы HTTP-ошибки службы отчётности. Проверьте коды и серверные события ниже.":
   recent?"Последний отчёт WSUS получен менее 24 часов назад. Новый отчёт после этой диагностики пока не подтверждён; это само по себе не означает неисправность.":
   server.Error.Length>0||!server.Complete?"Данные диагностики неполны; текущий сбой доставки не установлен.":
   reporting.Any(r=>r.Status>=200&&r.Status<300)?"Запрос отчётности обслужен без HTTP-ошибки, но свежий статус не появился. HTTP 200 не подтверждает обработку содержимого отчёта.":
   "В прочитанных IIS-журналах запрос отчётности не найден. Проверьте клиентский REPORT, адрес отчётности и NAT; это не доказывает сетевую блокировку.";
  string action=backlog||(!fresh&&server.ValidationErrors.Length>0)?"Рекомендация: нажмите «Восстановить отчётность», затем проверьте дату отчёта WSUS. Очистка удалит старые неотправленные события; автоматическая очистка не выполняется.":
   fresh||recent?"Очистка очереди по этим данным не требуется.":"Сопоставьте дату отчёта WSUS, возраст очереди и серверные ошибки. Одна только неполнота диагностики не является основанием для очистки.";
  return "## Диагностика доставки отчёта · итог\n"+finding+"\n"+action+
   "\nПоследний отчёт WSUS: "+(hasReport?LocalDateConverter.Format(server.Report!.LastReportedUtc):"не получен; давность не установлена")+
   "\nОчередь: "+(client.Queues.Count==0?"данные отсутствуют":string.Join("; ",client.Queues.Select(q=>q.Name+": "+q.Files+" файлов, "+q.Bytes+" байт"+(q.Files>0?", старейшее событие: "+LocalDateConverter.Format(q.OldestUtc):" (пуста)"))))+
   (client.QueueError.Length>0?"\nОграничения очереди: "+client.QueueError:"")+
   "\nПолнота диагностики: "+(server.Complete&&server.Error.Length==0?"серверные источники прочитаны; это не подтверждение исправности клиента":"частичная; это не оценка исправности клиента")+
   "\nИсточники: очередь — "+(client.QueueError.Length==0&&client.Queues.Count>0?"получена":"неполная / недоступна")+"; WSUS API — "+(server.Report!=null?"получен":"недоступен")+"; IIS — "+(server.IisRead?"прочитан":"неполный / недоступен")+"\nСобытия сервера — "+(server.EventsRead?"прочитаны":"недоступны")+"; HTTPERR — "+(server.HttpErrorsRead?"прочитан":"недоступен")+"; журнал обработки WSUS — "+(server.ProcessingLogRead?"прочитан":"недоступен")+"; ETL — "+client.EtlStatus+"\nКлиент: "+client.Computer+"; ОС: "+client.Os+"; сборка: "+client.Build+"\nИсточник: "+client.WUServer+"\nАдрес отчётности: "+client.WUStatusServer+
   "\nUseWUServer: "+client.UseWUServer+"; IP: "+string.Join(", ",client.IPs)+"\nSusClientId: "+client.SusClientId+
   "\nИнтервал: "+LocalDateConverter.Format(server.SinceUtc)+" — "+LocalDateConverter.Format(server.UntilUtc)+"\nПул IIS: "+server.PoolState+
   (server.Error.Length>0?"\nОграничения диагностики: "+server.Error:"")+"\n## Запросы IIS выбранного клиента\n"+
   (server.Requests.Count==0?(server.IisRead?"Подходящих запросов в просмотренном интервале нет.":"Чтение неполное; отсутствие запросов не установлено."):string.Join("\n",server.Requests.Select(r=>LocalDateConverter.Format(r.Utc)+"; "+r.IP+"; "+r.Method+" "+r.Path+"; HTTP "+r.Status+"."+r.SubStatus+"; Win32 "+r.Win32+"; "+r.Milliseconds+" мс")))+
   "\n## Технический журнал · WSUS и HTTPERR\nИсточник IIS: "+server.LogDirectory+"\n"+LogPresentation.LocalTimes(LogPresentation.Compact(server.ServerLog+"\n"+server.Events+"\n"+server.HttpErrors));
 }
}
public static class DeliveryScripts {

 public const string Recovery="""
$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue'
$source=Join-Path $env:windir 'SoftwareDistribution\EventCache.v2'
$installer=New-Object -ComObject Microsoft.Update.Installer;if($installer.IsBusy){throw 'Установка обновлений активна; очистка отменена'}
if(!(Test-Path $source)){'Очередь отсутствует; очистка не требуется';exit 0}
if((Get-Item $source).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Очередь является ссылкой; очистка запрещена'}
$items=@(Get-ChildItem $source -Recurse -Force -ErrorAction Stop);if(@($items|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'В очереди обнаружены ссылки; очистка запрещена'}
$files=@($items|Where-Object {!$_.PSIsContainer});$count=$files.Count;$bytes=[long](($files|Measure-Object Length -Sum).Sum)
if(!$count){'Очередь пуста; очистка не требуется';exit 0}
$root=Join-Path $env:ProgramData 'DomainConsoleCSharp';$stage=Join-Path $root ('ReportQueue_'+[guid]::NewGuid().ToString())
$wasRunning=(Get-Service wuauserv).Status -eq 'Running';$moved=$false
'Временное сохранение очереди: '+$stage
try {Stop-Service wuauserv -ErrorAction Stop;(Get-Service wuauserv).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30));Move-Item -LiteralPath $source -Destination $stage -ErrorAction Stop;$moved=$true}
finally {if($wasRunning){Start-Service wuauserv -ErrorAction Stop;(Get-Service wuauserv).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))}}
if($moved){if($keepBackup){'Резервная копия: '+$stage}else{Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction Stop;'Очередь очищена без сохранения копии'}}
'Время: '+(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')+'; файлов: '+$count+'; байт: '+$bytes
'Служба wuauserv: '+(Get-Service wuauserv).Status
""";
 public const string Client="""
$policy=Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate' -ErrorAction SilentlyContinue
$au=Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -ErrorAction SilentlyContinue
$identity=Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate' -ErrorAction SilentlyContinue
$os=Get-CimInstance Win32_OperatingSystem
$ips=@(Get-CimInstance Win32_NetworkAdapterConfiguration -Filter 'IPEnabled=True'|ForEach-Object {$_.IPAddress}|Where-Object {$_ -notmatch ':' -and $_ -ne '127.0.0.1'})
$probe=@{Computer=$env:COMPUTERNAME;Os=$os.Caption;Build=$os.BuildNumber;WUServer=[string]$policy.WUServer;WUStatusServer=[string]$policy.WUStatusServer;UseWUServer=[int]$au.UseWUServer;SusClientId=[string]$identity.SusClientId;IPs=$ips;CollectedUtc=[DateTime]::UtcNow.ToString('o')}
$probe.Queues=@();$probe.QueueError=''
foreach($name in @('EventCache','EventCache.v2')){
 try {$path=Join-Path "$env:windir\SoftwareDistribution" $name;$files=@();if(Test-Path $path){$files=@(Get-ChildItem $path -File -Recurse -ErrorAction Stop)}
 $oldest='';if($files.Count){$oldest=($files|Sort-Object CreationTimeUtc|Select-Object -First 1).CreationTimeUtc.ToString('o')}
 $probe.Queues+=@{Name=$name;Files=$files.Count;Bytes=[long](($files|Measure-Object Length -Sum).Sum);OldestUtc=$oldest}
 }catch{$probe.QueueError+=$name+': '+$_.Exception.Message+'; '}
}
$probe|ConvertTo-Json -Depth 5|Set-Content (Join-Path $dcFolder 'delivery-client.json') -Encoding UTF8
'Клиент: '+$probe.Computer+'; ОС: '+$probe.Os+'; сборка: '+$probe.Build
'Источник: '+$probe.WUServer+'; адрес отчётности: '+$probe.WUStatusServer+'; UseWUServer: '+$probe.UseWUServer
'IP: '+($ips -join ', ')+'; SusClientId: '+$probe.SusClientId
foreach($q in $probe.Queues){'Очередь '+$q.Name+': файлов '+$q.Files+'; байт '+$q.Bytes;if($q.Files){'Старейшее событие: '+([DateTime]::Parse($q.OldestUtc).ToLocalTime().ToString('dd.MM.yyyy HH:mm:ss zzz'))}}
if($probe.QueueError){'Ограничения сбора очереди: '+$probe.QueueError}
$reportPath=Join-Path $env:windir 'SoftwareDistribution\ReportingEvents.log';if(Test-Path $reportPath){'## Технический журнал · локальные события отчётности';$recentEvents=@(Get-Content $reportPath -Encoding UTF8 -Tail 200 | Where-Object {$parts=$_ -split '\t';$at=[DateTimeOffset]::MinValue;$parts.Length -gt 1 -and [DateTimeOffset]::TryParse($parts[1],[ref]$at) -and $at.UtcDateTime -ge [DateTime]::UtcNow.AddHours(-1)});if($recentEvents.Count){$recentEvents}else{'За последний час локальных событий отчётности нет.'}}
'## Технический журнал · события Windows Update';
$events=@(Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-WindowsUpdateClient/Operational';StartTime=(Get-Date).AddHours(-1)} -MaxEvents 60 -ErrorAction SilentlyContinue);if($events.Count){$events|Select-Object TimeCreated,Id,LevelDisplayName,Message|Format-List|Out-String}else{'За последний час событий Windows Update не получено.'}
'## Службы и прокси до запроса отчёта'
Get-Service wuauserv,bits,cryptsvc,UsoSvc -ErrorAction SilentlyContinue|Select-Object Name,Status,StartType|Format-Table|Out-String
netsh winhttp show proxy
'Данные собраны; это не подтверждение исправности клиента. После этого будет запрошена только отправка отчёта, поиск и установка не запускаются.'
""";
 public const string Server="""
$ProgressPreference='SilentlyContinue'
$since=[DateTime]::Parse($sinceText).ToUniversalTime().AddMinutes(-$lookback);$until=[DateTime]::UtcNow
$result=@{SinceUtc=$since.ToString('o');UntilUtc=$until.ToString('o');LogDirectory='';PoolState='Не определён';Complete=$true;Error='';Requests=@();Events='';HttpErrors='';ValidationErrors='';ServerLog='';IisRead=$false;EventsRead=$false;HttpErrorsRead=$false;ProcessingLogRead=$false;Report=$null}
try {
 [void][Reflection.Assembly]::LoadFrom((Join-Path $env:windir 'System32\inetsrv\Microsoft.Web.Administration.dll'))
 $manager=New-Object Microsoft.Web.Administration.ServerManager
 $site=@($manager.Sites|Where-Object {@($_.Bindings|Where-Object {$_.EndPoint.Port -eq $port}).Count -gt 0})
 if($site.Count -ne 1){throw 'Не найден единственный IIS-сайт с указанным портом WSUS'}
 $site=$site[0];$pool=$site.Applications['/'].ApplicationPoolName;$result.PoolState=$pool+': '+$manager.ApplicationPools[$pool].State
 $log=Join-Path ([Environment]::ExpandEnvironmentVariables($site.LogFile.Directory)) ('W3SVC'+$site.Id);$result.LogDirectory=$log
 $allFiles=@(Get-ChildItem $log -Filter '*.log' -ErrorAction Stop)
 $files=@($allFiles|Where-Object {$byName=$false;if($_.Name -match '^u_ex(\d{6})(?:_|\.)'){$day=[DateTime]::ParseExact($matches[1],'yyMMdd',[Globalization.CultureInfo]::InvariantCulture);$byName=$day.Date -ge $since.Date.AddDays(-1) -and $day.Date -le $until.Date.AddDays(1)};$byName -or $_.LastWriteTimeUtc -ge $since}|Sort-Object Name -Descending)
 if(!$files.Count -and $allFiles.Count){$files=@($allFiles|Sort-Object Name -Descending|Select-Object -First 2)}
 if(!$site.LogFile.Enabled){throw 'Журналирование IIS отключено'};if(!$allFiles.Count){throw 'В каталоге IIS действительно отсутствуют файлы *.log'};if([string]$site.LogFile.LogFormat -ne 'W3C'){throw 'Формат журнала IIS не W3C'}
 $watch=[Diagnostics.Stopwatch]::StartNew();$requestRows=New-Object 'System.Collections.Generic.Queue[object]'
 if(-not ('DomainConsoleIisReader16' -as [type])){Add-Type -TypeDefinition @'
using System;using System.IO;using System.Text;using System.Collections.Generic;using System.Diagnostics;using System.Globalization;
public sealed class DcIisRow16 {public string Utc,IP,Method,Path,SubStatus,Win32,Milliseconds;public int Status;}
public sealed class DcBoundedRead16:Stream {
 readonly Stream inner;long left;public DcBoundedRead16(Stream s,long size){inner=s;left=size;}
 public override int Read(byte[] b,int o,int n){int got=inner.Read(b,o,(int)Math.Min(n,left));left-=got;return got;}
 public override bool CanRead{get{return true;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}
 public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
 public override void Flush(){}public override long Seek(long o,SeekOrigin s){throw new NotSupportedException();}public override void SetLength(long n){throw new NotSupportedException();}public override void Write(byte[] b,int o,int n){throw new NotSupportedException();}
 protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
}
public static class DomainConsoleIisReader16 {
 public static void Read(string path,string[] ips,DateTime since,DateTime until,Stopwatch watch,Queue<object> rows){
 var allowed=new HashSet<string>(ips,StringComparer.OrdinalIgnoreCase);bool header=false;
 using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
 using(var reader=new StreamReader(new DcBoundedRead16(file,file.Length),Encoding.UTF8)){
 int date=-1,time=-1,ip=-1,method=-1,uri=-1,status=-1,sub=-1,win=-1,ms=-1;string line;
 while((line=reader.ReadLine())!=null){if(watch.Elapsed.TotalSeconds>45)throw new TimeoutException("Чтение IIS превысило 45 секунд; сохранены частичные данные");
 if(line.StartsWith("#Fields: ")){var f=line.Substring(9).Split(' ');date=Array.IndexOf(f,"date");time=Array.IndexOf(f,"time");ip=Array.IndexOf(f,"c-ip");method=Array.IndexOf(f,"cs-method");uri=Array.IndexOf(f,"cs-uri-stem");status=Array.IndexOf(f,"sc-status");sub=Array.IndexOf(f,"sc-substatus");win=Array.IndexOf(f,"sc-win32-status");ms=Array.IndexOf(f,"time-taken");if(date<0||time<0||ip<0||method<0||uri<0||status<0)throw new InvalidDataException("В IIS отсутствуют необходимые поля");header=true;continue;}
 if(line.StartsWith("#")||!header)continue;var v=line.Split(' ');if(v.Length<=Math.Max(status,Math.Max(uri,Math.Max(method,Math.Max(ip,Math.Max(date,time))))))continue;
 if(!allowed.Contains(v[ip]))continue;string p=v[uri];if(p.IndexOf("ReportingWebService",StringComparison.OrdinalIgnoreCase)<0&&p.IndexOf("ClientWebService",StringComparison.OrdinalIgnoreCase)<0&&p.IndexOf("SimpleAuthWebService",StringComparison.OrdinalIgnoreCase)<0)continue;
 DateTime at;if(!DateTime.TryParseExact(v[date]+" "+v[time],"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out at))throw new InvalidDataException("Некорректная дата IIS");if(at<since||at>until)continue;
 int code;if(!int.TryParse(v[status],out code))throw new InvalidDataException("Некорректный HTTP-код IIS");
 rows.Enqueue(new DcIisRow16{Utc=at.ToString("o"),IP=v[ip],Method=v[method],Path=p,Status=code,SubStatus=sub>=0&&sub<v.Length?v[sub]:"",Win32=win>=0&&win<v.Length?v[win]:"",Milliseconds=ms>=0&&ms<v.Length?v[ms]:""});if(rows.Count>1000)rows.Dequeue();
 }if(!header)throw new InvalidDataException("Заголовок W3C IIS не найден");}
 }
}
'@}
 foreach($file in $files){[DomainConsoleIisReader16]::Read($file.FullName,[string[]]$ips,$since,$until,$watch,$requestRows)}
 $result.IisRead=$true;$result.Requests=@($requestRows.ToArray());if($requestRows.Count -eq 1000){$result.Error+='; Показаны последние 1000 запросов; возможны более ранние записи';$result.Complete=$false}

}catch{$result.Complete=$false;$result.Error+=$_.Exception.Message;if($requestRows){$result.Requests=@($requestRows.ToArray())}}
try {$result.Events=(Get-WinEvent -FilterHashtable @{LogName=@('Application','System');StartTime=$since.ToLocalTime();EndTime=$until.ToLocalTime()} -MaxEvents 2000 -ErrorAction Stop|Where-Object {$_.ProviderName -match 'Update Services|Windows Server Update|WAS|W3SVC|IIS|MSSQL'}|Select-Object -First 50 @{Name='Utc';Expression={$_.TimeCreated.ToUniversalTime().ToString('o')}},ProviderName,Id,LevelDisplayName,Message|Format-List|Out-String);$result.EventsRead=$true}catch{if($_.FullyQualifiedErrorId -notmatch 'NoMatchingEventsFound'){$result.Complete=$false;$result.Error+='; События: '+$_.Exception.Message}else{$result.EventsRead=$true}}
try {$result.HttpErrors=(Get-ChildItem (Join-Path $env:windir 'System32\LogFiles\HTTPERR') -Filter '*.log' -ErrorAction Stop|Where-Object {$_.LastWriteTimeUtc -ge $since}|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 2|ForEach-Object {Get-Content $_.FullName -Tail 2000}|Where-Object {$line=$_;$utc=[DateTime]::MinValue;$valid=$line.Length -ge 19 -and [DateTime]::TryParseExact($line.Substring(0,19),'yyyy-MM-dd HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::AssumeUniversal,[ref]$utc);$valid -and $utc.ToUniversalTime() -ge $since -and $utc.ToUniversalTime() -le $until -and @($ips|Where-Object {$line -match ('(?:^| )'+[regex]::Escape($_)+'(?: |$)')}).Count -gt 0}|Select-Object -Last 100|ForEach-Object {$stamp=[DateTime]::ParseExact($_.Substring(0,19),'yyyy-MM-dd HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime();$stamp.ToString('o')+$_.Substring(19)}|Out-String);$result.HttpErrorsRead=$true}catch{$result.Complete=$false;$result.Error+='; HTTPERR: '+$_.Exception.Message}
try {[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');$wsus=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,$ssl,$port);$scope=New-Object Microsoft.UpdateServices.Administration.ComputerTargetScope;$scope.NameIncludes=$computer;$targets=@($wsus.GetComputerTargets($scope)|Where-Object {$_.FullDomainName.Split('.')[0] -eq $computer});if($targets.Count -ne 1){throw 'WSUS: клиент не найден однозначно'};$target=$targets[0];$result.Report=@{LastReportedUtc=$target.LastReportedStatusTime.ToUniversalTime().ToString('o');LastSyncUtc=$target.LastSyncTime.ToUniversalTime().ToString('o');LastSyncResult=[string]$target.LastSyncResult;FullDomainName=$target.FullDomainName}}catch{$result.Complete=$false;$result.Error+='; WSUS API: '+$_.Exception.Message}

try {
 $serverPath=Join-Path $env:ProgramFiles 'Update Services\LogFiles\SoftwareDistribution.log'
 $lines=@(Get-Content $serverPath -Encoding UTF8 -Tail 30000 -ErrorAction Stop);$blocks=New-Object 'System.Collections.Generic.List[string]'
 for($i=0;$i -lt $lines.Count;$i++){
  if($lines[$i] -notmatch ('TargetId=\[Id=\['+[regex]::Escape($clientId)+'\]')){continue}
  $stamp=$null;for($j=$i-1;$j -ge [Math]::Max(0,$i-60);$j--){if($lines[$j] -match '^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) UTC.*ValidateEventBatch'){$stamp=[DateTime]::ParseExact($matches[1],'yyyy-MM-dd HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime();break}}
  if(!$stamp -or $stamp -lt $since -or $stamp -gt $until){continue}
  $event=$lines[$i];$detail='Событие не прошло серверную проверку';if($event -match '(?:,|\[)g=([^,\]]+)'){$value=$matches[1];if($value.Contains('|')){$detail='Ошибка формата MiscData: g содержит GUID, разделённые |'}}
  $result.ValidationErrors=$detail;$blocks.Add($stamp.ToString('o')+' '+$detail+[Environment]::NewLine+$event)
 }
 $result.ServerLog=(@($blocks)|Select-Object -Last 5)-join [Environment]::NewLine;$result.ProcessingLogRead=$true
}catch{$result.Complete=$false;$result.Error+='; Журнал обработки WSUS: '+$_.Exception.Message}
$result|ConvertTo-Json -Depth 10 -Compress
""";
}
public sealed partial class RemoteService {
 public async Task<ServerReportEvidence> DeliveryServer(WsusSettings settings,ClientReportProbe probe,string started,int lookback=15){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));var code="$lookback="+Math.Clamp(lookback,0,1440)+";$clientId="+PowerShellBridge.Literal(probe.SusClientId)+";$port="+settings.Port+";$ssl="+(settings.Ssl?"$true":"$false")+";$computer="+PowerShellBridge.Literal(probe.Computer)+";$sinceText="+PowerShellBridge.Literal(string.IsNullOrEmpty(started)?DateTime.UtcNow.AddHours(-1).ToString("o"):started)+";$ips=@("+string.Join(",",probe.IPs.Select(PowerShellBridge.Literal))+");"+DeliveryScripts.Server;return JsonSerializer.Deserialize<ServerReportEvidence>(await PowerShellBridge.Execute(Invoke(settings.Host,code),timeout.Token),Store.Options)??throw new Exception("Пустой ответ диагностики сервера.");}
}
