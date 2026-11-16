$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or (Get-CimInstance Win32_ComputerSystem).PartOfDomain){throw 'Disposable non-domain Windows runner required'}
$root=New-Item C:\DCLabAutomation -ItemType Directory -Force
$source=(Get-Content docs/testing/candidate-0.57.sha -Raw).Trim()
$api="repos/$env:GITHUB_REPOSITORY/actions"
$run=(gh api "$api/workflows/build-windows.yml/runs?branch=main&head_sha=$source"|ConvertFrom-Json).workflow_runs|Where-Object conclusion -eq success|Select-Object -First 1
$zip=(gh api "$api/runs/$($run.id)/artifacts"|ConvertFrom-Json).artifacts|Where-Object name -eq DomainConsole-Windows-x64
$archive="$root\zip"
& curl.exe --fail --silent --show-error --location -H "Authorization: Bearer $env:GH_TOKEN" "$env:GITHUB_API_URL/$api/artifacts/$($zip.id)/zip" -o $archive
if($LASTEXITCODE -or "sha256:$((Get-FileHash $archive).Hash.ToLowerInvariant())" -ne $zip.digest){throw 'Candidate ZIP digest mismatch'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($archive,"$root\package")

$data=New-Item "$root\package\Data" -ItemType Directory
$h='client1.isolated.invalid'
$sus=[guid]::NewGuid().ToString();$addr='http://wsus.isolated.invalid:8530'
$identity=@{Host=$h;Name=$h;Machine=[guid]::NewGuid().ToString();Server=$addr;UseWUServer=1;SusClientId=$sus;At=[DateTime]::UtcNow.AddHours(-2).ToString('o')}
function Save($file,$value){ConvertTo-Json -InputObject $value -Depth 6|Set-Content -Encoding UTF8 (Join-Path $data $file)}
Save 'computers.json' @(@{Host=$h;Name='CLONE1';OU='OU=Test';OS='Windows Server';DirectoryId=[guid]::NewGuid().ToString()})
$observations=@{};$observations[$h]=$identity
Save 'client-identities58.json' $observations
Save 'compromised-ids58.json' @("$addr|$sus")
Save 'poll-paused41.json' $true
Save 'view-state.json' @{Host=$h;Tab=1}
$live=Get-Content '.github/tests/domain-scenario/gui.ps1' -Raw -Encoding UTF8
$functions=$live.Substring($live.IndexOf(' Add-Type -AssemblyName UIAutomationClient'),$live.IndexOf(' New-Item $data')-$live.IndexOf(' Add-Type -AssemblyName UIAutomationClient'))
$wait=$live.Substring($live.IndexOf('function Wait-For('),$live.IndexOf('$process=$null')-$live.IndexOf('function Wait-For('))
$discovery=Get-Content '.github/tests/window-discovery.ps1' -Raw -Encoding UTF8
$start='$ErrorActionPreference="Stop";$lab="C:\DCLabAutomation";$process=$null;try{'+[Environment]::NewLine+$wait+$functions+[Environment]::NewLine+$discovery+[Environment]::NewLine
$body=@'
 $process=Start-Process "$lab\package\DomainConsole.exe" -ArgumentList '--software-rendering' -WorkingDirectory "$lab\package" -PassThru
 $main=Wait-For {Application-Window "DomainConsole" $true} 'main' 70
 $id=Wait-For {Element $main ([Windows.Automation.AutomationElement]::AutomationIdProperty) 'HeaderIdentity58'} 'identity action' 45
 Click $id
 $w=Wait-For {Application-Window 'Идентичность WSUS · CLONE1'} 'live identity dialog' 45
 $w.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).Close()
 $restart=Wait-For {Element $main ([Windows.Automation.AutomationElement]::AutomationIdProperty) 'HeaderReboot'} 'header reboot' 30
 $async=[PowerShell]::Create()
 [void]$async.AddScript('param($b) $b.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()').AddArgument($restart)
 $call=$async.BeginInvoke()
 $dialog=Wait-For {Application-Window 'Перезагрузка · client1.isolated.invalid'} 'real reboot confirmation' 45
 $delay=Element $dialog ([Windows.Automation.AutomationElement]::ControlTypeProperty) ([Windows.Automation.ControlType]::Edit)
 $delay.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('0')
 $dialog.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).Close()
 [void]$async.EndInvoke($call);$async.Dispose()
 'PASS: actual identity window, live zero-delay confirmation and cancel in the same scheduled Windows desktop'|Set-Content "$lab\done"
}catch{
 $problem=($_|Out-String)
 if($process){$process.Refresh();$problem+="
AppExited=$($process.HasExited) AppSession=$($process.SessionId) HostSession=$((Get-Process -Id $PID).SessionId)"
 if(!$process.HasExited){$problem+="
Native windows="+[DCLabNativeWindows]::Inventory($process.Id)}}
 $problem|Set-Content "$lab\error"
}finally{if($process -and !$process.HasExited){$process.Kill()}}
'@
[IO.File]::WriteAllText("$root\operator.ps1",$start+$body,(New-Object Text.UTF8Encoding($true)))
$user='DCLabAutomation'
$pw='Dc9!'+[guid]::NewGuid().ToString('N')
try{
 $null=New-LocalUser -Name $user -Password (ConvertTo-SecureString $pw -AsPlainText -Force)
 $null=Add-LocalGroupMember -SID 'S-1-5-32-544' -Member $user
 $account="$env:COMPUTERNAME\$user"
 & icacls.exe "$root" /grant "${account}:(OI)(CI)F"|Out-Null
 $action=New-ScheduledTaskAction -Execute powershell.exe -Argument "-NoProfile -STA -ExecutionPolicy Bypass -File $root\operator.ps1"
 Register-ScheduledTask -TaskName DCLabAutomation -Action $action -User $account -Password $pw -RunLevel Highest -Force|Out-Null
 Start-ScheduledTask DCLabAutomation
 $end=[DateTime]::UtcNow.AddMinutes(5)
 do{
  if(Test-Path "$root\error"){throw (Get-Content "$root\error" -Raw)}
  if(Test-Path "$root\done"){Get-Content "$root\done";return}
  Start-Sleep -Seconds 2
 }while([DateTime]::UtcNow -lt $end)
 throw 'Bounded scheduled UI test timeout'
}finally{
 Unregister-ScheduledTask -TaskName DCLabAutomation -Confirm:$false -ErrorAction SilentlyContinue
 Remove-LocalUser -Name $user -ErrorAction SilentlyContinue
}
