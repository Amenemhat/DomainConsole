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
 function Run-Repair($window,$prefix){
  $button=Wait-For { Button $window $prefix } $prefix
  $async=[PowerShell]::Create()
  [void]$async.AddScript('param($b) $b.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()').AddArgument($button)
  $op=$async.BeginInvoke()
  try{
   $confirm=Wait-For {Element $root ([Windows.Automation.AutomationElement]::NameProperty) 'Перерегистрация клиентов'} 'membership consent'
   $yes=Wait-For {Element $confirm ([Windows.Automation.AutomationElement]::AutomationIdProperty) '6'} 'Yes'
   Click $yes
   if(!$op.AsyncWaitHandle.WaitOne(15000)){throw 'UI command did not return after consent'}
   [void]$async.EndInvoke($op)
  }finally{$async.Dispose()}
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
 $computers=@(Get-ADComputer -Filter * -Properties DNSHostName,OperatingSystem|ForEach-Object {
  @{Name=$_.Name;Host=$_.DNSHostName.ToLowerInvariant();OU=($_.DistinguishedName -split ',',2)[1];OS=$_.OperatingSystem;DirectoryId=$_.ObjectGUID.ToString();AdDisabled=$false}
 })
 $chosen='clone1.dc057.test'
 $observed=Get-Content "$lab\observations.json" -Raw|ConvertFrom-Json
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
 $main=Wait-For {Element $root ([Windows.Automation.AutomationElement]::ProcessIdProperty) $process.Id} 'packaged main window'
 $entry=Wait-For {Element $main ([Windows.Automation.AutomationElement]::AutomationIdProperty) 'HeaderIdentity58'} 'identity action'
 Click $entry
 $window=Wait-For {Element $root ([Windows.Automation.AutomationElement]::NameProperty) 'Идентичность WSUS · CLONE1'} 'WSUS identity window'
 # A client with a different spelling of the WSUS URL first joins in the full AD pass.
 Complete-Survey 1
 $limited=Read-Data 'identity-survey.json'
 if($limited.Domain -or @($limited.Results.PSObject.Properties).Count -ne 1){throw 'The limited pass must start with the one historical client only'}
 Browse-Domain $window
 $discovered=Read-Data 'identity-survey.json'
 if(@($discovered.Results.PSObject.Properties).Count -ne $computers.Count){throw 'The confirmed domain pass did not cover every AD computer'}
 Set-Content "$lab\read-only.done" 'checked'
 Wait-For {Test-Path "$lab\allow-consent.done"} 'read-only resource measurements' 120|Out-Null
 Run-Repair $window 'Исправить общий ID'
 Set-Content "$lab\first-consent.done" '4'
 Wait-For {Test-Path "$lab\fifth-online.done"} 'new network observation' 1100|Out-Null
 Browse-Domain $window
 Run-Repair $window 'Подтвердить расширение группы'
 Set-Content "$lab\expanded.done" '5'
 Wait-For {Test-Path "$lab\server-reports.done"} 'five external WSUS records' 2200|Out-Null
 Click (Wait-For {Button $window 'Повторить проверку результата'} 'read-only verification')
 Wait-For {$attempts=Read-Data 'identity-repairs58.json';$attempts -and @($attempts.PSObject.Properties.Value|Where-Object Phase -eq 'Подтверждено').Count -eq 5} 'five confirmed registrations in the product' 400|Out-Null
 $evidence=@{InitialCachedClients=1;InitialSurvey=$limited;FirstDomainSurvey=$discovered;Version=$process.MainModule.FileVersionInfo.ProductVersion;Attempts=(Read-Data 'identity-repairs58.json');Survey=(Read-Data 'identity-survey.json')}
 # One confirmed request must change the boot time on this disposable guest.
 if($chosen -ne 'clone1.dc057.test'){throw 'Unexpected reboot target'}
 $boot={ (Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().ToString('o') }
 $beforeBoot=Invoke-Command -ComputerName $chosen -ScriptBlock $boot
 $window.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).Close()
 $restart=Wait-For {Element $main ([Windows.Automation.AutomationElement]::AutomationIdProperty) 'HeaderReboot'} 'header reboot action'
 $async=[PowerShell]::Create()
 [void]$async.AddScript('param($b) $b.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()').AddArgument($restart)
 $opening=$async.BeginInvoke()
 try{
  $dialog=Wait-For {Element $root ([Windows.Automation.AutomationElement]::NameProperty) ("Перезагрузка · "+$chosen)} 'reboot configuration'
  $delay=Element $dialog ([Windows.Automation.AutomationElement]::ControlTypeProperty) ([Windows.Automation.ControlType]::Edit)
  $delay.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('0')
  $scan=Element $dialog ([Windows.Automation.AutomationElement]::NameProperty) 'Проверить обновления после перезагрузки'
  $toggle=$scan.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)
  if($toggle.Current.ToggleState -eq [Windows.Automation.ToggleState]::On){$toggle.Toggle()}
  Click (Button $dialog 'Перезагрузить')
  if(!$opening.AsyncWaitHandle.WaitOne(30000)){throw 'Reboot action did not return'}
  [void]$async.EndInvoke($opening)
 }finally{$async.Dispose()}
 $afterBoot=Wait-For {
  try{$time=Invoke-Command -ComputerName $chosen -ScriptBlock $boot -ErrorAction Stop;if([datetime]$time -gt [datetime]$beforeBoot){$time}}catch{}
 } 'actual new Windows boot' 450
 $evidence.Reboot=@{Target=$chosen;Before=$beforeBoot;After=$afterBoot;Confirmations=1}
 [IO.File]::WriteAllText("$lab\gui-evidence.json",($evidence|ConvertTo-Json -Depth 10),$utf8)
}catch{[IO.File]::WriteAllText("$lab\gui-error.txt",($_|Out-String),$utf8);exit 1}
finally{if($process -and !$process.HasExited){[void]$process.CloseMainWindow()}}
