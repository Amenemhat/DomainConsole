using System.Windows;
namespace DomainConsole;
public partial class MainWindow {
 public const string MemoryDiagnosticsScript="""
$ProgressPreference='SilentlyContinue'
'Память и подкачка · снимок '+(Get-Date).ToString('dd.MM.yyyy HH:mm:ss')
$sys=Get-CimInstance Win32_ComputerSystem
'RAM: '+[math]::Round($sys.TotalPhysicalMemory/1GB,2)+' ГБ; автоматическое управление подкачкой: '+$sys.AutomaticManagedPagefile
Get-CimInstance Win32_OperatingSystem|Select-Object FreePhysicalMemory,TotalVirtualMemorySize,FreeVirtualMemory|Format-List
Get-CimInstance Win32_PageFileUsage|Select-Object Name,AllocatedBaseSize,CurrentUsage,PeakUsage|Format-List
Get-CimInstance Win32_PageFileSetting|Select-Object Name,InitialSize,MaximumSize|Format-List
Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3'|Select-Object DeviceID,@{n='Свободно_ГБ';e={[math]::Round($_.FreeSpace/1GB,2)}}|Format-Table -AutoSize
try{Get-CimInstance Win32_PerfFormattedData_PerfOS_Memory -ErrorAction Stop|Select-Object AvailableMBytes,CommittedBytes,CommitLimit|Format-List}catch{'Счётчики памяти недоступны: '+$_.Exception.Message}
'Потребление процессов · PrivateMemorySize64 не равно занятой физической RAM'
Get-Process|Sort-Object PrivateMemorySize64 -Descending|Select-Object -First 8 Name,Id,@{n='Private_MB';e={[math]::Round($_.PrivateMemorySize64/1MB)}}|Format-Table -AutoSize
Get-WinEvent -FilterHashtable @{LogName='System';ProviderName='Microsoft-Windows-Resource-Exhaustion-Detector';Id=2004;StartTime=(Get-Date).AddDays(-1)} -MaxEvents 3 -ErrorAction SilentlyContinue|Format-List TimeCreated,Message
'Это текущий снимок. После перезагрузки прежняя нехватка памяти может быть не видна; учитывайте время события 2004. Настройки не изменялись.'
""";
 async void MemoryDiagnostics_Click(object s,RoutedEventArgs e)=>await Guard(async()=>{MicrosoftBox.IsChecked=false;await Submit(new(){new(){Name="Диагностика памяти и подкачки",Kind="PowerShell",Code=MemoryDiagnosticsScript}},"Commands",true);});
}
