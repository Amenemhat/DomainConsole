$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'DCWSUS' -or (Get-CimInstance Win32_ComputerSystem).Domain -ne 'dc057.test'){
    throw 'Isolated console member required.'
}
$lab='C:\DomainConsoleLab'
$dir=Join-Path $lab 'package'
$data=Join-Path $dir 'Data'
$fixture=Get-Content -LiteralPath (Join-Path $lab 'observations.json') -Raw | ConvertFrom-Json
$target='clone1.dc057.test'
if($fixture.Host -ne $target -or $fixture.Account -ne 'DCLAB\RecoveryFixture59'){throw 'Unexpected test target or account.'}
$utf8=[Text.UTF8Encoding]::new($false)

function Save-Json($name,$value){
    [IO.File]::WriteAllText((Join-Path $data $name),(ConvertTo-Json -InputObject $value -Depth 12),$utf8)
}
function Read-Entry($name,$key){
    $path=Join-Path $data $name
    if(!(Test-Path $path)){return}
    try{
        $json=Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
        if($json){return $json.PSObject.Properties[$key].Value}
    }catch{}
}
function Wait-For($condition,$label,$seconds=120){
    $deadline=[DateTime]::UtcNow.AddSeconds($seconds)
    while([DateTime]::UtcNow -lt $deadline){
        $result=& $condition
        if($result){return $result}
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $label"
}
function Mark($name){Set-Content (Join-Path $lab $name) 'checked'}
function Wait-Mark($name,$seconds=80){Wait-For {Test-Path (Join-Path $lab $name)} $name $seconds | Out-Null}

$process=$null
try{
    Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
    . (Join-Path $lab 'window-discovery.ps1')
    $desc=[Windows.Automation.TreeScope]::Descendants
    function Find($parent,$property,$value){
        $condition=[Windows.Automation.PropertyCondition]::new($property,$value)
        $parent.FindFirst($desc,$condition)
    }
    function Header-User(){
        $label=Find $main ([Windows.Automation.AutomationElement]::AutomationIdProperty) 'HeaderUser37'
        if($label){$label.Current.Name}
    }
    function Inspect-Client(){
        $button=Wait-For {Find $main ([Windows.Automation.AutomationElement]::AutomationIdProperty) 'HeaderLink'} 'WinRM check action'
        $button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    }
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    Save-Json 'computers.json' @(@{Name='CLONE1';Host=$target;OU='OU=LabA,DC=dc057,DC=test';OS='Windows Server'})
    Save-Json 'view-state.json' @{Host=$target;Tab=0}
    Save-Json 'poll-paused41.json' $true
    Save-Json 'auto-service31.json' $false
    $process=Start-Process (Join-Path $dir 'DomainConsole.exe') -WorkingDirectory $dir -ArgumentList '--software-rendering' -PassThru
    $main=Wait-For {Application-Window 'DomainConsole' $true} 'packaged main window'

    Inspect-Client
    $initial=Wait-For {
        $connection=Read-Entry 'client-connections.json' $target
        $label=Header-User
        if($connection.Error -and $label -eq 'Сведения о пользователе не получены'){$label}
    } 'WinRM unavailable without a cached identity' 100
    Mark 'winrm-without-history.done'

    Wait-Mark 'test-ad-loss.done'
    Inspect-Client
    $failed=Wait-For {
        $lookup=Read-Entry 'user-names37.json' $fixture.Account
        $session=Read-Entry 'client-users37.json' $target
        if($lookup.DirectoryUnavailable -and $session.Account -eq $fixture.Account -and
           $session.Current -and !$session.Name -and (Header-User) -eq 'Имя пользователя не получено'){
            $lookup
        }
    } 'valid WTS session with directory lookup denied' 105
    Mark 'ad-unavailable.done'

    Wait-Mark 'restore-directory.done'
    $retry=[DateTimeOffset]::Parse($failed.CheckedUtc).AddMinutes(2)
    Wait-For {[DateTimeOffset]::UtcNow -ge $retry} 'existing directory retry delay' 150 | Out-Null
    Inspect-Client
    $confirmed=Wait-For {
        $session=Read-Entry 'client-users37.json' $target
        $lookup=Read-Entry 'user-names37.json' $fixture.Account
        if($session.Name -eq 'Directory Fixture' -and $lookup -and !$lookup.DirectoryUnavailable -and (Header-User) -like 'Directory Fixture · *'){
            $session
        }
    } 'the same user resolved when AD returned' 105
    $savedLabel=Header-User
    Mark 'directory-recovered.done'

    Wait-Mark 'test-winrm-with-history.done'
    Inspect-Client
    $outage=Wait-For {
        $connection=Read-Entry 'client-connections.json' $target
        if($connection.Error -and (Header-User) -eq $savedLabel){$connection}
    } 'WinRM outage with a confirmed name' 100
    if((Read-Entry 'client-users37.json' $target).SeenUtc -ne $confirmed.SeenUtc){throw 'WinRM failure replaced the confirmed timestamp.'}
    [void]$process.CloseMainWindow()
    if(!$process.WaitForExit(25000)){throw 'Console did not close after the recorded WinRM failure.'}
    $process=Start-Process (Join-Path $dir 'DomainConsole.exe') -WorkingDirectory $dir -ArgumentList '--software-rendering' -PassThru
    $main=Wait-For {Application-Window 'DomainConsole' $true} 'restarted console window' 30
    Wait-For {(Header-User) -eq $savedLabel} 'the same confirmed name and time after restart' 30 | Out-Null
    Mark 'winrm-with-history.done'

    Wait-Mark 'restore-winrm.done'
    Inspect-Client
    $final=Wait-For {
        $session=Read-Entry 'client-users37.json' $target
        $connection=Read-Entry 'client-connections.json' $target
        if($session.Name -eq 'Directory Fixture' -and !$connection.Error -and
           [DateTimeOffset]::Parse($session.SeenUtc) -gt [DateTimeOffset]::Parse($confirmed.SeenUtc) -and
           (Header-User) -like 'Directory Fixture · *'){$session}
    } 'new user observation after restoring WinRM' 110
    $result=@{
        InitialHeader=$initial; DirectoryFailureAt=$failed.CheckedUtc; NameAfterDirectoryRecovery=$confirmed.Name
        SeenDuringWinRMLoss=$confirmed.SeenUtc; WinRMLossAt=$outage.LastAttempt; SeenAfterWinRMRecovery=$final.SeenUtc
        ConfirmedNameRestoredAfterRestart=$true; TechnicalLoginVisible=$false; Version=$process.MainModule.FileVersionInfo.ProductVersion
    }
    [IO.File]::WriteAllText((Join-Path $lab 'gui-evidence.json'),($result|ConvertTo-Json -Depth 6),$utf8)
}catch{
    $problem=$_|Out-String
    if($process){
        $process.Refresh()
        $problem+="`nAppExited=$($process.HasExited) AppSession=$($process.SessionId) HostSession=$((Get-Process -Id $PID).SessionId)"
        if(!$process.HasExited){$problem+="`nApp windows: "+[DCLabNativeWindows]::Inventory($process.Id)}
    }
    [IO.File]::WriteAllText((Join-Path $lab 'gui-error.txt'),$problem,$utf8)
    exit 1
}finally{
    if($process -and !$process.HasExited){[void]$process.CloseMainWindow()}
}
