using System.Windows;
using System.Windows.Controls;
namespace DomainConsole;
public sealed class CacheChoice:Window {
 public string Mode {get;private set;}="Download";public bool Backup {get;private set;}public bool Catroot {get;private set;}
 public CacheChoice(Window owner){Owner=owner;Title="Очистка кэша обновлений";Width=520;SizeToContent=SizeToContent.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;var panel=new StackPanel{Margin=new(18)};var mode=new ComboBox{SelectedIndex=0,Margin=new(0,8,0,12)};foreach(var text in new[]{"Минимальная: скачанные файлы (Download)","Очередь отчётности: EventCache / EventCache.v2","Полный сброс SoftwareDistribution"})mode.Items.Add(text);panel.Children.Add(mode);panel.Children.Add(new TextBlock{Text="Полный сброс удаляет локальную базу и отображаемую историю Windows Update; установленные обновления сохраняются. Очистка очереди удаляет неотправленные события. Операция не запускается при занятом установщике или активном задании WSUS.",TextWrapping=TextWrapping.Wrap});var backup=new CheckBox{Content="Сохранить резервную копию",Margin=new(0,12,0,6)};var catroot=new CheckBox{Content="Дополнительно сбросить catroot2",Margin=new(0,0,0,12),ToolTip="Кэш каталогов подписи; используйте только для соответствующих ошибок."};panel.Children.Add(backup);panel.Children.Add(catroot);var buttons=new WrapPanel();var ok=new Button{Content="Продолжить к выбору компьютеров",Padding=new(10)};ok.Click+=(s,e)=>{Mode=mode.SelectedIndex==0?"Download":mode.SelectedIndex==1?"Queue":"Full";Backup=backup.IsChecked==true;Catroot=catroot.IsChecked==true;DialogResult=true;};buttons.Children.Add(ok);buttons.Children.Add(new Button{Content="Отмена",IsCancel=true,Padding=new(10)});panel.Children.Add(buttons);Content=panel;}
}
public static class CacheScripts {
 public static string Build(string mode,bool backup,bool catroot)=>"$mode='"+mode+"';$backup="+(backup?"$true":"$false")+";$catroot="+(catroot?"$true":"$false")+";"+Body;
 public const string Body="""
$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue'
if((New-Object -ComObject Microsoft.Update.Installer).IsBusy){throw 'Установщик WUA занят. Очистка не начата.'}
$root=Join-Path $env:windir 'SoftwareDistribution'
$paths=if($mode -eq 'Download'){@(Join-Path $root 'Download')}elseif($mode -eq 'Queue'){@((Join-Path $root 'EventCache'),(Join-Path $root 'EventCache.v2'))}elseif($mode -eq 'Full'){@($root)}else{throw 'Неизвестный режим очистки'}
if($catroot){$paths+=Join-Path $env:windir 'System32\catroot2'}
foreach($p in $paths){if(Test-Path -LiteralPath $p){if((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Отказ: путь является ссылкой: $p"};if(Get-ChildItem -LiteralPath $p -Force -Recurse -ErrorAction Stop|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}|Select-Object -First 1){throw "Отказ: внутри каталога есть ссылки: $p"}}}
$services=@('wuauserv','bits');if($catroot){$services+='cryptsvc'}
$running=@($services|Where-Object {(Get-Service $_).Status -eq 'Running'})
try {
 foreach($name in $services){Stop-Service $name -ErrorAction Stop;(Get-Service $name).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))}
 foreach($p in $paths){if(Test-Path -LiteralPath $p){if($backup){$dest=$p+'.DomainConsoleBackup_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,6);Move-Item -LiteralPath $p -Destination $dest -ErrorAction Stop;"Резервная копия: $dest"}else{Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction Stop;"Очищено: $p"}}else{"Каталог отсутствует: $p"}}
} finally {
 $restoreErrors=@();foreach($name in $running){try{Start-Service $name -ErrorAction Stop;(Get-Service $name).WaitForStatus('Running',[TimeSpan]::FromSeconds(30))}catch{$restoreErrors+=$name+': '+$_.Exception.Message}}
 if($restoreErrors.Count){throw ('Не восстановлены службы: '+($restoreErrors -join '; '))}
}
'Очистка завершена. Повторный поиск и установка запускаются отдельно.'
""";
}
