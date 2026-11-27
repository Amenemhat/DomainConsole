$ErrorActionPreference='Stop'
if ($env:COMPUTERNAME -ne 'DCAD' -or (Get-ADDomain).DNSRoot -ne 'dc057.test') { throw 'Isolated lab domain required' }
$lab='C:\DomainConsoleLab'
$dir=Join-Path $lab 'package'
$data=Join-Path $dir 'Data'
$utf8=New-Object Text.UTF8Encoding($false)
function Save-Json($name,$object){[IO.File]::WriteAllText((Join-Path $data $name),(ConvertTo-Json -Depth 10 -InputObject $object),$utf8)}
function Read-Data($name){$p=Join-Path $data $name;if(Test-Path $p){try{return Get-Content -Encoding UTF8 -Raw $p|ConvertFrom-Json}catch{}}}
function Wait-For($condition,$label,$seconds=180){
 $end=[DateTime]::UtcNow.AddSeconds($seconds)
 while([DateTime]::UtcNow -lt $end){$value=& $condition;if($value){return $value};Start-Sleep -Milliseconds 500}
 throw "Timed out waiting for $label"
}
$process=$null
try{
 Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
public static class DCLabNativeWindows {
    delegate bool Visitor(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(int thread, Visitor callback, IntPtr data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder title, int size);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int size);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr dialog,int id);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
    public static bool ConfirmExpectedDialog(int process,string title) {
        var dialog=Locate(process,title,false);
        var yes=dialog==IntPtr.Zero?IntPtr.Zero:GetDlgItem(dialog,6);
        return yes!=IntPtr.Zero && PostMessage(dialog,0x111,new IntPtr(6),yes);
    }
    public static IntPtr Locate(int process, string title, bool prefix) {
        var match=IntPtr.Zero;
        foreach(ProcessThread thread in Process.GetProcessById(process).Threads) {
            EnumThreadWindows(thread.Id,delegate(IntPtr h,IntPtr unused) {
                var text=new StringBuilder(600); GetWindowText(h,text,text.Capacity);
                bool same=prefix?text.ToString().StartsWith(title,StringComparison.OrdinalIgnoreCase):text.ToString().Equals(title,StringComparison.OrdinalIgnoreCase);
                if(same){match=h;return false;}
                return true;
            },IntPtr.Zero);
            if(match!=IntPtr.Zero)break;
        }
        return match;
    }
    public static string Inventory(int process) {
        var names=new List<string>();
        foreach(ProcessThread thread in Process.GetProcessById(process).Threads) {
            EnumThreadWindows(thread.Id,delegate(IntPtr h,IntPtr unused){
                var text=new StringBuilder(600);var name=new StringBuilder(120);
                GetWindowText(h,text,text.Capacity);GetClassName(h,name,name.Capacity);
                names.Add(name.ToString()+"["+text.ToString()+"]");return true;
            },IntPtr.Zero);
        }
        return string.Join(", ",names);
    }
}
'@
function Application-Window($title,[bool]$prefix=$false){
 $process.Refresh()
 if($process.HasExited){throw "Application process exited with code $($process.ExitCode)"}
 $handle=[DCLabNativeWindows]::Locate($process.Id,$title,$prefix)
 if($handle -ne [IntPtr]::Zero){return [Windows.Automation.AutomationElement]::FromHandle($handle)}
}

 $root=[Windows.Automation.AutomationElement]::RootElement
 $desc=[Windows.Automation.TreeScope]::Descendants
 function Element($parent,$property,$value){
  if(!$parent){return}
  $c=New-Object Windows.Automation.PropertyCondition($property,$value)
  return $parent.FindFirst($desc,$c)
 }
 function Button($parent,$text){
  $c=New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Button)
  foreach($button in $parent.FindAll($desc,$c)){if($button.Current.IsEnabled -and $button.Current.Name.StartsWith($text)){return $button}}
 }
 function Click($e){$e.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()}
 function Run-Repair($window,$prefix,$expected){
  $button=Wait-For { Button $window $prefix } $prefix
  $caption=$button.Current.Name
  if($caption -notmatch $expected){throw "Unexpected repair membership shown in button: $caption"}
  $async=[PowerShell]::Create()
  [void]$async.AddScript('param($b) $b.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()').AddArgument($button)
  $op=$async.BeginInvoke()
  try{
   $confirm=Wait-For {Application-Window 'Перерегистрация клиентов'} 'membership consent'
   $yes=Element $confirm ([Windows.Automation.AutomationElement]::AutomationIdProperty) '6'
   if($yes){Click $yes}
   elseif(![DCLabNativeWindows]::ConfirmExpectedDialog($process.Id,'Перерегистрация клиентов')){throw 'Expected Yes control not found'}
   if(!$op.AsyncWaitHandle.WaitOne(15000)){throw 'UI command did not return after consent'}
   [void]$async.EndInvoke($op)
  }finally{$async.Dispose()}
  return $caption
 }
 function Complete-Survey($minimum){
  Wait-For {$s=Read-Data 'identity-survey.json';$s -and $s.Results.PSObject.Properties.Count -and @($s.Results.PSObject.Properties).Count -ge $minimum -and $s.ScopeMessage -eq 'Проход завершён'} 'completed client observations' 400|Out-Null
 }
 function Browse-Domain($window){
  $before=Read-Data 'identity-survey.json'
  Click (Wait-For {Button $window 'Проверить весь список AD'} 'whole directory')
  Wait-For {$s=Read-Data 'identity-survey.json';$s.Domain -and $s.Started -ne $before.Started} 'new directory pass' |Out-Null
  Complete-Survey $computers.Count
 }
 New-Item $data -ItemType Directory -Force |Out-Null
 $computers=@(Get-ADComputer -Filter "Name -like 'CLONE*'" -Properties DNSHostName,OperatingSystem|ForEach-Object {
  @{Name=$_.Name;Host=$_.DNSHostName.ToLowerInvariant();OU=($_.DistinguishedName -split ',',2)[1];OS=$_.OperatingSystem;DirectoryId=$_.ObjectGUID.ToString();AdDisabled=$false}
 })
 $chosen='clone1.dc057.test'
 $observed=Get-Content "$lab\observations.json" -Raw|ConvertFrom-Json
 if($computers.Count -ne @($observed.PSObject.Properties).Count){throw 'The directory snapshot does not contain the exact fixture clients'}
 $pair=@($observed.PSObject.Properties).Count -eq 2
 $previous=@{}
 foreach($x in $observed.PSObject.Properties.Value){
  $hostName=$x.Name.ToLowerInvariant()
  if($hostName -ne $chosen){continue}
  $x|Add-Member -Force -NotePropertyName Host -NotePropertyValue $hostName
  $x.At=[DateTime]::UtcNow.AddHours(-2).ToString('o')
  $previous[$hostName]=$x
 }
 if($previous.Count -ne 1){throw 'A single historical client is required before discovery'}
 $source=$previous[$chosen].Server.TrimEnd('/').ToLowerInvariant()+'|'+$previous[$chosen].SusClientId.ToLowerInvariant()
 Save-Json 'computers.json' $computers
 Save-Json 'client-identities58.json' $previous
 Save-Json 'compromised-ids58.json' @($source)
 Save-Json 'wsus-settings.json' @{Host='dcwsus.dc057.test';Port=8530;Ssl=$false}
 Save-Json 'view-state.json' @{Host=$chosen;Tab=1}
 Save-Json 'poll-paused41.json' $true
 Save-Json 'auto-service31.json' $true
 $process=Start-Process (Join-Path $dir 'DomainConsole.exe') -WorkingDirectory $dir -ArgumentList '--software-rendering' -PassThru
 $main=Wait-For {Application-Window "DomainConsole" $true} 'packaged main window'
 $entry=Wait-For {Element $main ([Windows.Automation.AutomationElement]::AutomationIdProperty) 'HeaderIdentity58'} 'identity action'
 Click $entry
 $window=Wait-For {Application-Window 'Идентичность WSUS · CLONE1'} 'WSUS identity window'
 # A client with a different spelling of the WSUS URL first joins in the full AD pass.
 Complete-Survey 1
 $limited=Read-Data 'identity-survey.json'
 if($limited.Domain -or @($limited.Results.PSObject.Properties).Count -ne 1){throw 'The limited pass must start with the one historical client only'}
 Browse-Domain $window
 $discovered=Read-Data 'identity-survey.json'
 $onlineHosts=@($observed.PSObject.Properties | Where-Object Name -ne 'CLONE5' | ForEach-Object {$_.Value.Name.ToLowerInvariant()})
 # Repeat discovery once after a read timeout on an online guest, before any consent.
 $timedOut=@($onlineHosts | Where-Object {$discovered.Results.$_ -like 'Не удалось проверить · Превышено время операции*'})
 if($timedOut.Count -gt 0){
  Browse-Domain $window
  $discovered=Read-Data 'identity-survey.json'
 }
 if(@($discovered.Results.PSObject.Properties).Count -ne $computers.Count){throw 'The confirmed domain pass did not cover every AD computer'}
 Set-Content "$lab\read-only.done" 'checked'
 Wait-For {Test-Path "$lab\allow-consent.done"} 'read-only resource measurements' 120|Out-Null
 $expected=if($pair){2}else{5}
 $initial=if($pair){2}else{4}
 $firstCaption=Run-Repair $window 'Сбросить SusClientId' "$initial ПК$"
 Set-Content "$lab\first-consent.done" $initial
 if(!$pair){
  Wait-For {Test-Path "$lab\fifth-online.done"} 'new network observation' 1100|Out-Null
  Browse-Domain $window
  $extendedCaption=Run-Repair $window 'Подтвердить расширение группы' '1 ПК'
  Set-Content "$lab\expanded.done" '5'
 }
 Wait-For {Test-Path "$lab\server-reports.done"} 'fresh external WSUS records' 2200|Out-Null
 Click (Wait-For {Button $window 'Повторить проверку результата'} 'read-only verification')
 Wait-For {$attempts=Read-Data 'identity-repairs58.json';$attempts -and @($attempts.PSObject.Properties.Value|Where-Object Phase -eq 'Подтверждено').Count -eq $expected} 'confirmed registrations in the product' 400|Out-Null
 $evidence=@{FirstConsentButton=$firstCaption;ExtensionButton=$extendedCaption;InitialCachedClients=1;InitialSurvey=$limited;FirstDomainSurvey=$discovered;Version=$process.MainModule.FileVersionInfo.ProductVersion;Attempts=(Read-Data 'identity-repairs58.json');Survey=(Read-Data 'identity-survey.json')}
 [IO.File]::WriteAllText("$lab\gui-evidence.json",($evidence|ConvertTo-Json -Depth 10),$utf8)
 }catch{
 $message=($_|Out-String)
 if($process -and !$process.HasExited){
  $message+=" App windows: "+[DCLabNativeWindows]::Inventory($process.Id)
 }
 [IO.File]::WriteAllText("$lab\gui-error.txt",$message,$utf8);exit 1
}
finally{if($process -and !$process.HasExited){[void]$process.CloseMainWindow()}}
