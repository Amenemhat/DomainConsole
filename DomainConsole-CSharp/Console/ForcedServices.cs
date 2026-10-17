using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DomainConsole.Shared;
namespace DomainConsole;
public sealed class ServiceProcessCandidate {public int Pid {get;set;}public string CreatedUtc {get;set;}="";public string Path {get;set;}="";public List<string> Services {get;set;}=new();public List<string> Dependents {get;set;}=new();public string Reason {get;set;}="";public string Label=>"PID "+Pid+" · "+string.Join(", ",Services)+(Reason.Length>0?" · "+Reason:"");}
public sealed partial class RemoteService {
 public async Task<List<ServiceProcessCandidate>> ServiceCandidates(TargetRecord target){using var c=new CancellationTokenSource(TimeSpan.FromSeconds(30));var json=await PowerShellBridge.Execute(Invoke(target,ServiceProcessDiscovery),c.Token);return JsonSerializer.Deserialize<List<ServiceProcessCandidate>>(json,Store.Options)??new();}
 public const string ServiceProcessDiscovery="""
$allowed=@('UsoSvc','wuauserv','bits','cryptsvc');$all=@(Get-CimInstance Win32_Service -ErrorAction Stop);$rows=@()
foreach($procId in @($all|Where-Object {$_.Name -in $allowed -and $_.ProcessId -gt 4}|Select-Object -ExpandProperty ProcessId -Unique)){
 $services=@($all|Where-Object {$_.ProcessId -eq $procId}|Select-Object -ExpandProperty Name|Sort-Object);$reason='';$created='';$path=''
 try{$p=Get-Process -Id $procId -ErrorAction Stop;$created=$p.StartTime.ToUniversalTime().ToString('o');$path=$p.Path;if($path -ne (Join-Path $env:windir 'System32\svchost.exe')){$reason='Не стандартный процесс службы'};if(@($services|Where-Object {$_ -notin $allowed}).Count){$reason='Общий процесс с другими службами; рекомендуется перезагрузка'}}catch{$reason=$_.Exception.Message}
 $deps=@();foreach($n in $services){$deps+=@((Get-Service $n -ErrorAction SilentlyContinue).DependentServices|Where-Object {$_.Status -eq 'Running'}|Select-Object -ExpandProperty ServiceName)}
 $rows+=@{Dependents=@($deps|Sort-Object -Unique);Pid=[int]$procId;CreatedUtc=$created;Path=$path;Services=@($services);Reason=$reason}
}
ConvertTo-Json -InputObject @($rows) -Depth 5 -Compress
""";
 public static string ForcedServiceScript(ServiceProcessCandidate candidate)=>"$expected="+PowerShellBridge.Literal(JsonSerializer.Serialize(candidate))+"|ConvertFrom-Json;"+ForceServiceBody;
 public const string ForceServiceBody="""
$ErrorActionPreference='Stop';$allowed=@('UsoSvc','wuauserv','bits','cryptsvc')
if($expected.Pid -le 4 -or !$expected.CreatedUtc -or @($expected.Services).Count -eq 0){throw 'Нет подтверждённого адресата'}
Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices; public static class DcCriticalService { [DllImport("kernel32.dll",SetLastError=true)] public static extern bool IsProcessCritical(IntPtr process,out bool critical); }'
$p=Get-Process -Id $expected.Pid -ErrorAction Stop
try{
 $critical=$true;if(![DcCriticalService]::IsProcessCritical($p.Handle,[ref]$critical) -or $critical){throw 'Критический процесс либо проверка недоступна; нужна перезагрузка'}
 if($p.Path -ne (Join-Path $env:windir 'System32\svchost.exe') -or $p.StartTime.ToUniversalTime().ToString('o') -ne $expected.CreatedUtc){throw 'Процесс изменился; требуется новое подтверждение'}
 $current=@(Get-CimInstance Win32_Service -Filter ('ProcessId='+[int]$expected.Pid) -ErrorAction Stop|Sort-Object Name)
 $names=@($current|Select-Object -ExpandProperty Name)
 if(($names -join '|') -ne (@($expected.Services|Sort-Object) -join '|') -or @($names|Where-Object {$_ -notin $allowed}).Count){throw 'Изменился состав служб либо есть посторонние службы; процесс не завершён'}
 $running=@($current|Where-Object {$_.State -ne 'Stopped'}|Select-Object -ExpandProperty Name);$deps=@();foreach($n in $names){$deps+=@((Get-Service $n -ErrorAction Stop).DependentServices|Where-Object {$_.Status -eq 'Running'}|Select-Object -ExpandProperty ServiceName)};$deps=@($deps|Sort-Object -Unique);if(($deps -join '|') -ne (@($expected.Dependents|Sort-Object) -join '|')){throw 'Изменились зависимости; подтвердите заново'};$running+= $deps
 $p.Kill();if(!$p.WaitForExit(10000)){throw 'Выход процесса не подтверждён'}
 Write-Output ('Завершён подтверждённый PID '+$expected.Pid)
 $errors=@();foreach($name in $running){try{Start-Service $name -ErrorAction Stop;(Get-Service $name).WaitForStatus([ServiceProcess.ServiceControllerStatus]::Running,[TimeSpan]::FromSeconds(20));Write-Output ($name+': работает')}catch{$errors+=($name+': '+$_.Exception.Message)}}
 if($errors.Count){throw ('Часть служб не запущена: '+($errors -join '; '))}
 Write-Output 'Службы запущены. Возобновление загрузки необходимо проверить отдельно.'
}finally{$p.Dispose()}
""";
}
public partial class MainWindow {
 async void ForceServices_Click(object sender,RoutedEventArgs e)=>await Guard(async()=>{var row=Current();if(!await EnsureEndpointCredentials(new(){row}))return;var candidates=await remote.ServiceCandidates(row.Target);var window=new Window{Owner=this,Title="Зависшие службы · "+row.Name,Width=700,Height=400,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new DockPanel{Margin=new(16)};window.Content=panel;var note=new TextBlock{Text="Принудительное завершение — только для выбранного процесса служб этого ПК. При текущей установке оно может прервать обновление; предпочтительнее дождаться её завершения или согласовать перезагрузку. Общие процессы с посторонними службами не завершаются.",TextWrapping=TextWrapping.Wrap};DockPanel.SetDock(note,Dock.Top);panel.Children.Add(note);var actions=new WrapPanel();DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);var list=new ListBox{ItemsSource=candidates,DisplayMemberPath="Label"};panel.Children.Add(list);var apply=new Button{Content="Завершить процесс и запустить службы…"};actions.Children.Add(apply);actions.Children.Add(new Button{Content="Отмена",IsCancel=true});apply.Click+=(s,a)=>{if(list.SelectedItem is not ServiceProcessCandidate candidate||candidate.Reason.Length>0){MessageBox.Show(window,"Выберите допустимый процесс. Для общего/недоступного процесса используйте перезагрузку.");return;}if(MessageBox.Show(window,"Принудительно завершить PID "+candidate.Pid+"? Будут затронуты: "+string.Join(", ",candidate.Services)+"; восстановление зависимостей: "+string.Join(", ",candidate.Dependents)+". Активные операции могут завершиться ошибкой.","Подтверждение",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes)window.DialogResult=true;};if(window.ShowDialog()!=true||list.SelectedItem is not ServiceProcessCandidate chosen)return;
 var t=ConnectionStore.Capture(row.Target);t.State=null;t.Output="";t.Status="Queued";var job=new RemoteJob{Name="Принудительный перезапуск зависших служб",OutputChannel="WSUS",IndependentControl=true,AllowUpdateConflict=true,Targets=new(){t},Steps=new(){new(){Name="Принудительный перезапуск подтверждённого процесса служб",Kind="PowerShell",TouchesUpdates=true,Code=RemoteService.ForcedServiceScript(chosen)}}};History.Insert(0,job);queue.Enqueue((job,t));row.JobId=job.Id;RememberComputer(row.Host);Save();Sync(job);ShowCurrent();await SendQueue();});
}
