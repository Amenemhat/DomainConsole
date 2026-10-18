using System.Windows;
using System.Windows.Controls;
namespace DomainConsole;
public sealed class CacheChoice:Window {
 public string Mode {get;private set;}="Download";public bool Backup {get;private set;}public bool Catroot {get;private set;}
 public CacheChoice(Window owner){Owner=owner;Title="Очистка кэша обновлений";Width=520;SizeToContent=SizeToContent.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;var panel=new StackPanel{Margin=new(18)};var mode=new ComboBox{SelectedIndex=0,Margin=new(0,8,0,12)};foreach(var text in new[]{"Минимальная: скачанные файлы (Download)","Очередь отчётности: EventCache / EventCache.v2","Полный сброс SoftwareDistribution"})mode.Items.Add(text);panel.Children.Add(mode);panel.Children.Add(new TextBlock{Text="Полный сброс удаляет локальную базу и отображаемую историю Windows Update; установленные обновления сохраняются. Очистка очереди удаляет неотправленные события. Перед очисткой проверяется занятость Windows Update. При включённом автообслуживании после сброса выполняются поиск и установка найденных обновлений; перезагрузка отдельно по разрешению.",TextWrapping=TextWrapping.Wrap});var backup=new CheckBox{Content="Сохранить резервную копию",Margin=new(0,12,0,6)};var catroot=new CheckBox{Content="Дополнительно сбросить catroot2",Margin=new(0,0,0,12),ToolTip="Кэш каталогов подписи; используйте только для соответствующих ошибок."};panel.Children.Add(backup);panel.Children.Add(catroot);var buttons=new WrapPanel();var ok=new Button{Content="Продолжить к выбору компьютеров",Padding=new(10)};ok.Click+=(s,e)=>{Mode=mode.SelectedIndex==0?"Download":mode.SelectedIndex==1?"Queue":"Full";Backup=backup.IsChecked==true;Catroot=catroot.IsChecked==true;DialogResult=true;};buttons.Children.Add(ok);buttons.Children.Add(new Button{Content="Отмена",IsCancel=true,Padding=new(10)});panel.Children.Add(buttons);Content=panel;}
}
public static class CacheScripts {
 public static string Build(string mode,bool backup,bool catroot)=>"$mode='"+mode+"';$backup="+(backup?"$true":"$false")+";$catroot="+(catroot?"$true":"$false")+";"+Body;
 public const string Body="""
$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue'
if(!$dcAllowUpdateConflict -and (New-Object -ComObject Microsoft.Update.Installer).IsBusy){throw 'Установщик WUA занят. Очистка не начата.'}
$root=Join-Path $env:windir 'SoftwareDistribution'
$paths=if($mode -eq 'Download'){@(Join-Path $root 'Download')}elseif($mode -eq 'Queue'){@((Join-Path $root 'EventCache'),(Join-Path $root 'EventCache.v2'))}elseif($mode -eq 'Full'){@($root)}else{throw 'Неизвестный режим очистки'}
if($catroot){$paths+=Join-Path $env:windir 'System32\catroot2'}
$services=@('UsoSvc','wuauserv','bits','cryptsvc')
$services=@($services|Where-Object {Get-Service $_ -ErrorAction SilentlyContinue})
$stage='Проверка корневых путей';$currentPath='';$primary=$null;$restoreErrors=@();$detached=@()
function Assert-NoCacheLinks([string]$path){
 $items=[IO.Directory]::EnumerateFileSystemEntries($path)
 foreach($item in $items){$attributes=[IO.File]::GetAttributes($item);if($attributes -band [IO.FileAttributes]::ReparsePoint){throw 'В изолированном кэше есть ссылка; удаление отложено'};if($attributes -band [IO.FileAttributes]::Directory){Assert-NoCacheLinks $item}}
}
try {
 foreach($p in $paths){$currentPath=$p;if(Test-Path -LiteralPath $p){if((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Корневой путь кэша является ссылкой'}}}
 $stage='Остановка служб';$currentPath=''
 foreach($name in $services){$currentPath=$name;Stop-Service $name -ErrorAction Stop;(Get-Service $name).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(20))}
 foreach($p in $paths){$stage='Изоляция старого кэша';$currentPath=$p
  if(Test-Path -LiteralPath $p){
   if(!$backup -and @(Get-ChildItem -LiteralPath (Split-Path $p) -Directory -Filter ((Split-Path $p -Leaf)+'.DomainConsolePendingDelete_*') -ErrorAction Stop).Count){throw 'Осталась предыдущая изолированная копия. Выполните её очистку перед новым сбросом.'}
   $suffix=if($backup){'.DomainConsoleBackup_'}else{'.DomainConsolePendingDelete_'}
   $dest=$p+$suffix+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,6)
   [IO.Directory]::Move($p,$dest)
   if((Test-Path -LiteralPath $p) -or !(Test-Path -LiteralPath $dest)){throw 'Перенос каталога не подтверждён'}
   $detached+=@{Original=$p;Detached=$dest;Backup=$backup}
   if($backup){'Резервная копия: '+$dest}else{'Старый кэш изолирован: '+$dest}
  }else{'Каталог отсутствует: '+$p}
 }
 if($mode -eq 'Full'){[IO.Directory]::CreateDirectory($root)|Out-Null}
} catch {
 $primary=$_
 'Этап сбоя: '+$stage;'Объект: '+$currentPath
 'Тип: '+$_.Exception.GetType().FullName;'HRESULT: 0x'+$_.Exception.HResult.ToString('X8')
 'ErrorId: '+$_.FullyQualifiedErrorId;'Target: '+$_.TargetObject
 'Позиция: '+$_.InvocationInfo.PositionMessage
 if($_.Exception.InnerException){'Вложенная ошибка: '+$_.Exception.InnerException.ToString()}
} finally {
 foreach($name in $services){try{Start-Service $name -ErrorAction Stop;(Get-Service $name).WaitForStatus('Running',[TimeSpan]::FromSeconds(20))}catch{$restoreErrors+=$name+': '+$_.Exception.Message}}
}
if($primary){if($restoreErrors.Count){'Дополнительный сбой восстановления служб: '+($restoreErrors -join '; ')};throw $primary}
if($restoreErrors.Count){throw ('Не восстановлены службы: '+($restoreErrors -join '; '))}
# Logical reset is complete. Removing detached files is a separate, bounded cleanup task.
foreach($item in $detached){if(!$item.Backup){
 $manifest=Join-Path $env:ProgramData 'DomainConsoleCSharp\pending-cache-cleanup.jsonl'
 $entry=@{Path=$item.Detached;Original=$item.Original;CreatedUtc=[DateTime]::UtcNow.ToString('o')}
 try{[IO.Directory]::CreateDirectory((Split-Path $manifest))|Out-Null;($entry|ConvertTo-Json -Compress)|Add-Content -LiteralPath $manifest -Encoding UTF8;'Удаление изолированного кэша запланировано отдельной задачей очистки.'}catch{'WARNING: Кэш сброшен, но не удалось зарегистрировать удаление остатков: '+$item.Detached+'; '+$_.Exception.Message}
}}
try{Start-ScheduledTask -TaskName 'DomainConsoleCSharp-Maintenance' -ErrorAction SilentlyContinue}catch{}
'Кэш сброшен; службы запущены. Физическое удаление старых файлов выполняется отдельно и не меняет результат сброса.'

""";
}

