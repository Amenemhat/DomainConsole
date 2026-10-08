using System.Text.Json;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ClientReportProbe {
 public string Computer {get;set;}="";public string Os {get;set;}="";public string Build {get;set;}="";public string WUServer {get;set;}="";public string WUStatusServer {get;set;}="";public int UseWUServer {get;set;}public string SusClientId {get;set;}="";public string[] IPs {get;set;}=Array.Empty<string>();public string CollectedUtc {get;set;}="";
}
public sealed class HttpReportRequest {public string Method {get;set;}="";public string Utc {get;set;}="";public string IP {get;set;}="";public string Path {get;set;}="";public int Status {get;set;}public string SubStatus {get;set;}="";public string Win32 {get;set;}="";public string Milliseconds {get;set;}="";}
public sealed class ServerReportEvidence {
 public string Error {get;set;}="";public string SinceUtc {get;set;}="";public string UntilUtc {get;set;}="";public string LogDirectory {get;set;}="";public string PoolState {get;set;}="";public bool Complete {get;set;}public List<HttpReportRequest> Requests {get;set;}=new();public WsusReport? Report {get;set;}public string Events {get;set;}="";public string HttpErrors {get;set;}="";public string ValidationErrors {get;set;}="";public string ServerLog {get;set;}="";
}
public static class DeliveryPresentation {
 public static string Format(ClientReportProbe client,ServerReportEvidence server,DateTimeOffset now){var reporting=server.Requests.Where(r=>r.Method=="POST"&&r.Path.Contains("ReportingWebService",StringComparison.OrdinalIgnoreCase)).ToList();var failures=reporting.Where(r=>r.Status>=400&&!(r.Status==401&&reporting.Any(after=>after.IP==r.IP&&after.Utc.CompareTo(r.Utc)>=0&&after.Status>=200&&after.Status<300))).ToList();string finding=server.Report?.Fresh(client.CollectedUtc)==true?"На WSUS появился отчёт после сбора данных клиента. Точная связь с нашим запросом не доказана.":server.ValidationErrors.Length>0?"WSUS отклонил событие клиента: "+server.ValidationErrors:failures.Count>0?"Зафиксированы HTTP-ошибки службы отчётности. Проверьте коды и серверные события ниже.":server.Error.Length>0||!server.Complete?"Серверные данные неполны; место сбоя пока не определено.":reporting.Any(r=>r.Status>=200&&r.Status<300)?"Запрос к службе отчётности обслужен без HTTP-ошибки, но свежий статус не появился. HTTP 200 не подтверждает обработку содержимого отчёта.":"В прочитанных IIS-журналах запрос отчётности от указанных IP не найден. Проверьте клиентский REPORT, адрес отчётности и возможный NAT; это не доказывает сетевую блокировку.";
  return "## Диагностика доставки отчёта · вывод\n"+finding+"\nКлиент: "+client.Computer+"; ОС: "+client.Os+"; сборка: "+client.Build+"\nИсточник: "+client.WUServer+"\nАдрес отчётности WUStatusServer: "+client.WUStatusServer+"\nUseWUServer: "+client.UseWUServer+"; IP: "+string.Join(", ",client.IPs)+"\nSusClientId: "+client.SusClientId+" (собран для проверки идентичности, совпадения с другими ПК автоматически не проверялись)\nИнтервал: "+LocalDateConverter.Format(server.SinceUtc)+" — "+LocalDateConverter.Format(server.UntilUtc)+"\nПул IIS: "+server.PoolState+(server.Error.Length>0?"\nОграничения: "+server.Error:"")+"\n## Запросы IIS выбранного клиента\n"+(server.Requests.Count==0?"Подходящих запросов нет.":string.Join("\n",server.Requests.Select(r=>LocalDateConverter.Format(r.Utc)+"; "+r.IP+"; "+r.Method+" "+r.Path+"; HTTP "+r.Status+"."+r.SubStatus+"; Win32 "+r.Win32+"; "+r.Milliseconds+" мс")))+"\n## Технический журнал · WSUS и HTTPERR\nИсточник IIS: "+server.LogDirectory+"\n"+LogPresentation.LocalTimes(LogPresentation.Compact(server.ServerLog+"\n"+server.Events+"\n"+server.HttpErrors));
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
$probe|ConvertTo-Json -Depth 5|Set-Content (Join-Path $dcFolder 'delivery-client.json') -Encoding UTF8
$probe|Format-List|Out-String
foreach($name in @('EventCache','EventCache.v2')){$path=Join-Path "$env:windir\SoftwareDistribution" $name;if(Test-Path $path){$files=@(Get-ChildItem $path -File -Recurse -ErrorAction Stop);'Очередь '+$name+': файлов '+$files.Count+'; байт '+[long](($files|Measure-Object Length -Sum).Sum);$files|Sort-Object CreationTime|Select-Object -First 5 Name,Length,CreationTime,LastWriteTime|Format-Table|Out-String}}
$reportPath=Join-Path $env:windir 'SoftwareDistribution\ReportingEvents.log';if(Test-Path $reportPath){'Последние локальные события (не подтверждают приём сервером):';Get-Content $reportPath -Encoding UTF8 -Tail 40}
Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-WindowsUpdateClient/Operational';StartTime=(Get-Date).AddHours(-1)} -MaxEvents 60 -ErrorAction SilentlyContinue|Select-Object TimeCreated,Id,LevelDisplayName,Message|Format-List|Out-String
Get-Service wuauserv,bits,cryptsvc,UsoSvc -ErrorAction SilentlyContinue|Select-Object Name,Status,StartType|Format-Table|Out-String
netsh winhttp show proxy
'Данные собраны; это не подтверждение исправности клиента. После этого будет запрошена только отправка отчёта, поиск и установка не запускаются.'
""";
 public const string Server="""
$since=[DateTime]::Parse($sinceText).ToUniversalTime().AddMinutes(-$lookback);$until=[DateTime]::UtcNow
$result=@{SinceUtc=$since.ToString('o');UntilUtc=$until.ToString('o');LogDirectory='';PoolState='Не определён';Complete=$true;Error='';Requests=@();Events='';HttpErrors='';ValidationErrors='';ServerLog='';Report=$null}
try {
 [void][Reflection.Assembly]::LoadFrom((Join-Path $env:windir 'System32\inetsrv\Microsoft.Web.Administration.dll'))
 $manager=New-Object Microsoft.Web.Administration.ServerManager
 $site=@($manager.Sites|Where-Object {@($_.Bindings|Where-Object {$_.EndPoint.Port -eq $port}).Count -gt 0})
 if($site.Count -ne 1){throw 'Не найден единственный IIS-сайт с указанным портом WSUS'}
 $site=$site[0];$pool=$site.Applications['/'].ApplicationPoolName;$result.PoolState=$pool+': '+$manager.ApplicationPools[$pool].State
 $log=Join-Path ([Environment]::ExpandEnvironmentVariables($site.LogFile.Directory)) ('W3SVC'+$site.Id);$result.LogDirectory=$log
 $files=@(Get-ChildItem $log -Filter '*.log' -ErrorAction Stop|Sort-Object Name -Descending|Select-Object -First 3)
 if(!$site.LogFile.Enabled){throw 'Журналирование IIS отключено'};if(!$files.Count){throw 'Файлы IIS не найдены в каталоге'};if([string]$site.LogFile.LogFormat -ne 'W3C'){throw 'Формат журнала IIS не W3C'}
 $watch=[Diagnostics.Stopwatch]::StartNew();$requestRows=New-Object 'System.Collections.Generic.Queue[object]'
 foreach($file in $files){$fields=@();foreach($line in [IO.File]::ReadLines($file.FullName)){if($watch.Elapsed.TotalSeconds -gt 45){throw 'Чтение IIS ограничено 45 секундами; показаны частичные данные'};if($line.StartsWith('#Fields: ')){$fields=$line.Substring(9).Split(' ');continue};if($line.StartsWith('#') -or !$fields.Count){continue};$values=$line.Split(' ');if($values.Length -lt $fields.Count){continue};$row=@{};for($i=0;$i -lt $fields.Count;$i++){$row[$fields[$i]]=$values[$i]};if(!$row.ContainsKey('date') -or !$row.ContainsKey('time') -or !$row.ContainsKey('c-ip') -or !$row.ContainsKey('cs-uri-stem') -or !$row.ContainsKey('sc-status') -or !$row.ContainsKey('cs-method')){throw 'В IIS отсутствуют необходимые поля date/time/c-ip/cs-uri-stem/sc-status'}
 $stamp=[DateTime]::ParseExact(($row['date']+' '+$row['time']),'yyyy-MM-dd HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime()
 if($stamp -lt $since -or $stamp -gt $until -or $ips -notcontains $row['c-ip']){continue};$path=$row['cs-uri-stem'];if($path -notmatch '(?i)ReportingWebService|ClientWebService|SimpleAuthWebService'){continue}
 $requestRows.Enqueue(@{Utc=$stamp.ToString('o');IP=$row['c-ip'];Method=$row['cs-method'];Path=$path;Status=[int]$row['sc-status'];SubStatus=[string]$row['sc-substatus'];Win32=[string]$row['sc-win32-status'];Milliseconds=[string]$row['time-taken']});if($requestRows.Count -gt 1000){$null=$requestRows.Dequeue();$result.Complete=$false;$result.Error='Показаны последние 1000 подходящих запросов.'}
 }};$result.Requests=@($requestRows.ToArray())
}catch{$result.Complete=$false;$result.Error+=$_.Exception.Message;if($requestRows){$result.Requests=@($requestRows.ToArray())}}
try {$result.Events=(Get-WinEvent -FilterHashtable @{LogName=@('Application','System');StartTime=$since.ToLocalTime();EndTime=$until.ToLocalTime()} -MaxEvents 2000 -ErrorAction Stop|Where-Object {$_.ProviderName -match 'Update Services|Windows Server Update|WAS|W3SVC|IIS|MSSQL'}|Select-Object -First 50 @{Name='Utc';Expression={$_.TimeCreated.ToUniversalTime().ToString('o')}},ProviderName,Id,LevelDisplayName,Message|Format-List|Out-String)}catch{$result.Error+='; События: '+$_.Exception.Message}
try {$result.HttpErrors=(Get-ChildItem (Join-Path $env:windir 'System32\LogFiles\HTTPERR') -Filter '*.log' -ErrorAction Stop|Where-Object {$_.LastWriteTimeUtc -ge $since}|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 2|ForEach-Object {Get-Content $_.FullName -Tail 2000}|Where-Object {$line=$_;$utc=[DateTime]::MinValue;$valid=$line.Length -ge 19 -and [DateTime]::TryParseExact($line.Substring(0,19),'yyyy-MM-dd HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::AssumeUniversal,[ref]$utc);$valid -and $utc.ToUniversalTime() -ge $since -and $utc.ToUniversalTime() -le $until -and @($ips|Where-Object {$line -match ('(?:^| )'+[regex]::Escape($_)+'(?: |$)')}).Count -gt 0}|Select-Object -Last 100|ForEach-Object {$stamp=[DateTime]::ParseExact($_.Substring(0,19),'yyyy-MM-dd HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime();$stamp.ToString('o')+$_.Substring(19)}|Out-String)}catch{$result.Error+='; HTTPERR: '+$_.Exception.Message}
try {[void][Reflection.Assembly]::LoadWithPartialName('Microsoft.UpdateServices.Administration');$wsus=[Microsoft.UpdateServices.Administration.AdminProxy]::GetUpdateServer($env:COMPUTERNAME,$ssl,$port);$scope=New-Object Microsoft.UpdateServices.Administration.ComputerTargetScope;$scope.NameIncludes=$computer;$targets=@($wsus.GetComputerTargets($scope)|Where-Object {$_.FullDomainName.Split('.')[0] -eq $computer});if($targets.Count -ne 1){throw 'WSUS: клиент не найден однозначно'};$target=$targets[0];$result.Report=@{LastReportedUtc=$target.LastReportedStatusTime.ToUniversalTime().ToString('o');LastSyncUtc=$target.LastSyncTime.ToUniversalTime().ToString('o');LastSyncResult=[string]$target.LastSyncResult;FullDomainName=$target.FullDomainName}}catch{$result.Error+='; WSUS API: '+$_.Exception.Message}

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
 $result.ServerLog=(@($blocks)|Select-Object -Last 5)-join [Environment]::NewLine
}catch{$result.Error+='; Журнал обработки WSUS: '+$_.Exception.Message}
$result|ConvertTo-Json -Depth 10 -Compress
""";
}
public sealed partial class RemoteService {
 public async Task<ServerReportEvidence> DeliveryServer(WsusSettings settings,ClientReportProbe probe,string started,int lookback=15){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));var code="$lookback="+Math.Clamp(lookback,0,1440)+";$clientId="+PowerShellBridge.Literal(probe.SusClientId)+";$port="+settings.Port+";$ssl="+(settings.Ssl?"$true":"$false")+";$computer="+PowerShellBridge.Literal(probe.Computer)+";$sinceText="+PowerShellBridge.Literal(string.IsNullOrEmpty(started)?DateTime.UtcNow.AddHours(-1).ToString("o"):started)+";$ips=@("+string.Join(",",probe.IPs.Select(PowerShellBridge.Literal))+");"+DeliveryScripts.Server;return JsonSerializer.Deserialize<ServerReportEvidence>(await PowerShellBridge.Execute(Invoke(settings.Host,code),timeout.Token),Store.Options)??throw new Exception("Пустой ответ диагностики сервера.");}
}
