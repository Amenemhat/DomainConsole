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
        $_.Targets.Host -contains $hostName -and @($_.Steps | Where-Object Code -match 'function Save-Dependents').Count -eq 1
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
    function Open-Action($element){
        $runspace=[PowerShell]::Create()
        [void]$runspace.AddScript('param($b) $b.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()').AddArgument($element)
        $invocation=$runspace.BeginInvoke()
        return @{Runspace=$runspace;Invocation=$invocation}
    }
    function Confirm-Command($serviceTools,$batch){
        $menuCall=Open-Action $serviceTools
        $restartCall=$null
        $selectionCall=$null
        try {
            $menu=Wait-For {Application-Window 'Диагностика и восстановление'} 'service menu' 30
            $label=Wait-For {ByName $menu 'Перезапуск служб…'} 'restart services action' 20
            $walker=[Windows.Automation.TreeWalker]::ControlViewWalker
            while($label -and $label.Current.ControlType -ne [Windows.Automation.ControlType]::Button){
                $label=$walker.GetParent($label)
            }
            if(!$label){throw 'The restart action has no button.'}
            $restartCall=Open-Action $label
            $choice=Wait-For {Application-Window 'Перезапуск служб Windows Update'} 'Windows Update service selection' 30
            foreach($service in 'UsoSvc','bits'){
                $box=Wait-For {ByName $choice $service} $service 15
                $toggle=$box.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)
                $state=if($service -eq 'bits'){[Windows.Automation.ToggleState]::On}else{[Windows.Automation.ToggleState]::Off}
                if($toggle.Current.ToggleState -ne $state){$toggle.Toggle()}
                if($toggle.Current.ToggleState -ne $state){throw "Wrong fixture state for $service"}
            }
            $selectionCall=Open-Action (Wait-For {
                ByName $choice 'Применить к целям из следующего подтверждения'
            } 'approve the selected service' 15)
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
            foreach($call in @($selectionCall,$restartCall,$menuCall)){
                if(!$call.Invocation.AsyncWaitHandle.WaitOne(15000)){throw 'UI command did not return after consent'}
                [void]$call.Runspace.EndInvoke($call.Invocation)
            }
        }finally{
            foreach($call in @($selectionCall,$restartCall,$menuCall)){
                if($call){$call.Runspace.Dispose()}
            }
        }
    }

    $computers=@(Get-ADComputer -Filter * -Properties DNSHostName,OperatingSystem | Where-Object DNSHostName -in @($first,$peer) | ForEach-Object {
        @{Name=$_.Name;Host=$_.DNSHostName.ToLowerInvariant();OU=($_.DistinguishedName -split ',',2)[1];OS=$_.OperatingSystem;DirectoryId=$_.ObjectGUID.ToString()}
    })
    if($computers.Count -ne 2){throw 'Expected two distinct lab targets.'}
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    Save-Json 'computers.json' $computers
    Save-Json 'view-state.json' @{Host=$first;Tab=2;Expanded=@($computers.OU)}
    Save-Json 'poll-paused41.json' $true
    Save-Json 'auto-service31.json' $false
    $process=Start-Process (Join-Path $dir 'DomainConsole.exe') -WorkingDirectory $dir -ArgumentList '--software-rendering' -PassThru
    $main=Wait-For {Application-Window 'DomainConsole' $true} 'packaged main window'
    $tree=Wait-For {ById $main 'OuTree'} 'computer tree'
    Mark-Target $tree 'DCWSUS'
    Mark-Target $tree 'CLONE1'
    $serviceTools=Wait-For {ById $main 'ServiceTools36'} 'service tools'
    Confirm-Command $serviceTools $true

    $partial=Wait-For {
        $a=JobFor $first
        $b=JobFor $peer
        if($a -and $b -and $a.Targets[0].Status -eq 'SubmissionUnknown' -and $b.Targets[0].Status -in @('Submitted','Running','Completed')){
            @{ReceiptLost=$a.Targets[0].Status;Peer=$b.Targets[0].Status;At=[DateTime]::UtcNow.ToString('o')}
        }
    } 'uncertain delivery and a confirmed peer' 180
    $receiptJob=(JobFor $first).Id
    $peerJob=(JobFor $peer).Id
    Confirm-Command $serviceTools $false
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
