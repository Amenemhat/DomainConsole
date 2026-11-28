$ErrorActionPreference='Stop'
if($env:COMPUTERNAME -ne 'DCAD' -or (Get-ADDomain).DNSRoot -ne 'dc057.test'){
    throw 'Isolated lab domain required.'
}

$lab='C:\DomainConsoleLab'
$dir=Join-Path $lab 'package'
$data=Join-Path $dir 'Data'
$utf8=[Text.UTF8Encoding]::new($false)
$first='dcwsus.dc057.test'
$peer='clone1.dc057.test'
$command=@'
$ErrorActionPreference='Stop'
Stop-Service BITS -ErrorAction Stop
(Get-Service BITS).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(20))
Start-Service BITS -ErrorAction Stop
(Get-Service BITS).WaitForStatus('Running',[TimeSpan]::FromSeconds(20))
Add-Content 'C:\DomainConsoleLab\DCLabManualReceipt059.txt' 'BITS stopped and started'
'BITS stopped and started'
'@

function Save-Json($name,$value){
    [IO.File]::WriteAllText((Join-Path $data $name),(ConvertTo-Json -InputObject $value -Depth 12),$utf8)
}
function Wait-For($condition,$label,$seconds=150){
    $end=[DateTime]::UtcNow.AddSeconds($seconds)
    while([DateTime]::UtcNow -lt $end){
        $value=& $condition
        if($value){return $value}
        Start-Sleep -Milliseconds 350
    }
    throw "Timed out waiting for $label"
}
function JobFor($hostName){
    $path=Join-Path $data 'history.json'
    if(!(Test-Path $path)){return}
    try{$history=Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json}
    catch{return}
    $jobs=@($history | Where-Object {
        $_.Targets.Host -contains $hostName -and @($_.Steps | Where-Object Code -match 'DCLabManualReceipt059').Count -eq 1
    })
    if($jobs.Count -gt 1){throw "Duplicate command admitted for $hostName"}
    if($jobs.Count -eq 1){return $jobs[0]}
}

$process=$null
try {
    Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
    . (Join-Path $lab 'window-discovery.ps1')
    $children=[Windows.Automation.TreeScope]::Descendants
    function Find($parent,$property,$value){
        $condition=[Windows.Automation.PropertyCondition]::new($property,$value)
        return $parent.FindFirst($children,$condition)
    }
    function ById($parent,$value){
        Find $parent ([Windows.Automation.AutomationElement]::AutomationIdProperty) $value
    }
    function ByName($parent,$value){
        Find $parent ([Windows.Automation.AutomationElement]::NameProperty) $value
    }
    function Click($element){
        $element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    }
    function Mark-Target($tree,$name){
        $element=Wait-For {ByName $tree $name} "tree row $name"
        $walker=[Windows.Automation.TreeWalker]::ControlViewWalker
        while($element -and $element.Current.ControlType -ne [Windows.Automation.ControlType]::TreeItem){
            $element=$walker.GetParent($element)
        }
        if(!$element){throw "Missing tree item for $name"}
        $check=Find $element ([Windows.Automation.AutomationElement]::ControlTypeProperty) ([Windows.Automation.ControlType]::CheckBox)
        $pattern=$check.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)
        if($pattern.Current.ToggleState -eq [Windows.Automation.ToggleState]::Off){$pattern.Toggle()}
        if($pattern.Current.ToggleState -ne [Windows.Automation.ToggleState]::On){throw "$name was not marked"}
    }
    function Confirm-Command($editorButton,$batch){
        $submit=[PowerShell]::Create()
        [void]$submit.AddScript('param($b) $b.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()').AddArgument($editorButton)
        $op=$submit.BeginInvoke()
        try {
            $dialog=Wait-For {Application-Window 'Подтверждение действия и целей'} 'target consent' 30
            if($batch){
                $scope=Wait-For {ByName $dialog 'Все отмеченные: 2'} 'exactly two marked targets' 15
                $scope.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
            }else{
                $scope=Wait-For {ByName $dialog 'Только текущий: DCWSUS'} 'only the uncertain client' 15
                if(!$scope.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected){
                    throw 'The repeat would target another client.'
                }
            }
            Click (Wait-For {ByName $dialog 'Выполнить'} 'confirm action' 15)
            if(!$op.AsyncWaitHandle.WaitOne(15000)){throw 'UI command did not return after consent'}
            [void]$submit.EndInvoke($op)
        }finally{$submit.Dispose()}
    }

    $computers=@(Get-ADComputer -Filter * -Properties DNSHostName,OperatingSystem | Where-Object DNSHostName -in @($first,$peer) | ForEach-Object {
        @{Name=$_.Name;Host=$_.DNSHostName.ToLowerInvariant();OU=($_.DistinguishedName -split ',',2)[1];OS=$_.OperatingSystem;DirectoryId=$_.ObjectGUID.ToString()}
    })
    if($computers.Count -ne 2){throw 'Expected two distinct lab targets.'}
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    Save-Json 'computers.json' $computers
    Save-Json 'view-state.json' @{Host=$first;Tab=0;Expanded=@($computers.OU)}
    Save-Json 'poll-paused41.json' $true
    Save-Json 'auto-service31.json' $false
    Save-Json 'shell-mode.json' 1
    $process=Start-Process (Join-Path $dir 'DomainConsole.exe') -WorkingDirectory $dir -ArgumentList '--software-rendering' -PassThru
    $main=Wait-For {Application-Window 'DomainConsole' $true} 'packaged main window'
    $tree=Wait-For {ById $main 'OuTree'} 'computer tree'
    Mark-Target $tree 'DCWSUS'
    Mark-Target $tree 'CLONE1'
    $editor=Wait-For {ById $main 'Editor'} 'command editor'
    $editor.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($command)
    $run=Wait-For {ById $main 'RunEditorButton'} 'run from editor'
    Confirm-Command $run $true

    $partial=Wait-For {
        $a=JobFor $first
        $b=JobFor $peer
        if($a -and $b -and $a.Targets[0].Status -eq 'SubmissionUnknown' -and $b.Targets[0].Status -in @('Submitted','Running','Completed')){
            @{ReceiptLost=$a.Targets[0].Status;Peer=$b.Targets[0].Status;At=[DateTime]::UtcNow.ToString('o')}
        }
    } 'uncertain delivery and a confirmed peer' 180
    $receiptJob=(JobFor $first).Id
    $peerJob=(JobFor $peer).Id
    Confirm-Command $run $false
    Wait-For {
        $toast=ById $main 'ToastText'
        if($toast -and $toast.Current.Name -like '*действие уже принято или его исход уточняется*'){$true}
    } 'duplicate submission notice' 10 | Out-Null
    if((JobFor $first).Id -ne $receiptJob -or (JobFor $peer).Id -ne $peerJob){throw 'The repeat created a different job.'}
    Set-Content (Join-Path $lab 'receipt-repeated.done') 'checked'
    $final=Wait-For {
        $a=JobFor $first
        $b=JobFor $peer
        if($a -and $b -and $a.Targets[0].Status -eq 'Completed' -and $b.Targets[0].Status -eq 'Completed'){
            @{$first=$a.Id;$peer=$b.Id}
        }
    } 'both original jobs completed after restoring WinRM' 265
    if($final[$first] -ne $receiptJob -or $final[$peer] -ne $peerJob){throw 'The final result does not match the original jobs.'}
    $evidence=@{Partial=$partial;Jobs=$final;RepeatRejected=$true;OriginalJobsReconciled=$true;Version=$process.MainModule.FileVersionInfo.ProductVersion}
    [IO.File]::WriteAllText((Join-Path $lab 'gui-evidence.json'),($evidence|ConvertTo-Json -Depth 10),$utf8)
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
