function Invoke-PackagedContinuation($Package, $Policy, [guid]$Id, [long]$Received) {
    Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
    $ae=[Windows.Automation.AutomationElement]
    $desc=[Windows.Automation.TreeScope]::Descendants
    $process=$null
    $policyKey='HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
    $updates=@(
        @{Path=$policyKey;Name='WUServer';Type='String';Value=$Policy},
        @{Path=$policyKey;Name='WUStatusServer';Type='String';Value=$Policy},
        @{Path="$policyKey\AU";Name='UseWUServer';Type='DWord';Value=1}
    )
    $originals=@()
    function Save-Json($Name,$Value){
        [IO.File]::WriteAllText((Join-Path $data $Name),(ConvertTo-Json -InputObject $Value -Depth 10),[Text.UTF8Encoding]::new($false))
    }
    function Find-Control($Parent,$Property,$Value){
        $Parent.FindFirst($desc,[Windows.Automation.PropertyCondition]::new($Property,$Value))
    }
    function Wait-Control($Read,[string]$Stage,[int]$Seconds){
        $end=[DateTime]::UtcNow.AddSeconds($Seconds)
        do {
            if($process.HasExited){throw "Console exited while waiting for $Stage"}
            try {$found=& $Read} catch [Windows.Automation.ElementNotAvailableException] {$found=$null}
            if($found){return $found}
            Start-Sleep -Milliseconds 400
        }while([DateTime]::UtcNow -lt $end)
        throw "Console did not show $Stage"
    }
    try {
        foreach($item in $updates){
            if(!(Test-Path -LiteralPath $item.Path)){New-Item -Path $item.Path -Force | Out-Null}
            $existing=Get-ItemProperty -LiteralPath $item.Path
            $property=$existing.PSObject.Properties[$item.Name]
            $originals+=@{Path=$item.Path;Name=$item.Name;Property=$property}
            New-ItemProperty -LiteralPath $item.Path -Name $item.Name -PropertyType $item.Type -Value $item.Value -Force | Out-Null
        }
        $data=Join-Path $Package 'Data'
        New-Item -Path $data -ItemType Directory -Force | Out-Null
        $computer=$env:COMPUTERNAME.ToLowerInvariant()
        $stamp=[DateTimeOffset]::UtcNow.ToString('o')
        $wsus=[uri]$Policy
        Save-Json 'computers.json' @(@{Host=$computer;Name=$computer;OS='Disposable Windows'})
        Save-Json 'view-state.json' @{Host=$computer;Tab=2}
        Save-Json 'background-expanded32.json' $true
        Save-Json 'link-automatic31.json' $false
        Save-Json 'auto-service31.json' $false
        Save-Json 'overview-settings.json' @{Automatic=$false;Hours=24}
        Save-Json 'wsus-settings.json' @{Host=$wsus.Host;Port=$wsus.Port;Ssl=$false}
        $reports=@{}
        $reports[$computer]=@{Server=$wsus.Authority;Found=$true;ReceivedUtc=$stamp
            Report=@{FullDomainName=$computer;LastReportedUtc=[DateTimeOffset]::UtcNow.AddDays(-3).ToString('o');LastSyncResult='Succeeded'}}
        Save-Json 'wsus-report-cache.json' $reports
        $process=Start-Process (Join-Path $Package 'DomainConsole.exe') -WorkingDirectory $Package -ArgumentList '--software-rendering' -PassThru
        $main=Wait-Control {
            $process.Refresh()
            if($process.MainWindowHandle -ne [IntPtr]::Zero){$ae::FromHandle($process.MainWindowHandle)}
        } 'the WPF window' 100
        $button=Wait-Control {
            $item=Find-Control $main $ae::AutomationIdProperty 'ContinueBits187'
            if($item -and $item.Current.IsEnabled){$item}
        } 'the Continue download button for the live paused transfer' 115
        $button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        $dialog=Wait-Control {
            $condition=[Windows.Automation.AndCondition]::new(
                [Windows.Automation.PropertyCondition]::new($ae::ProcessIdProperty,$process.Id),
                [Windows.Automation.PropertyCondition]::new($ae::NameProperty,("Продолжить скачивание · "+$computer)))
            $ae::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,$condition)
        } 'the selection dialog' 45
        $combo=$dialog.FindFirst($desc,[Windows.Automation.PropertyCondition]::new($ae::ControlTypeProperty,[Windows.Automation.ControlType]::ComboBox))
        $selection=$combo.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
        if($selection.Count -ne 1 -or $selection[0].Current.Name -notlike "*$Id*"){throw 'Dialog did not select the exact BITS job'}
        $confirm=Find-Control $dialog $ae::NameProperty 'Продолжить'
        $confirm.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        $message=Wait-Control {
            $details=Find-Control $main $ae::AutomationIdProperty 'BackgroundDetails'
            if($details){
                $value=$details.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value
                if($value -match 'Передача продолжилась .+ новых байт'){($value -split '\r?\n')[0]}
                elseif($value -match 'Исход запроса не подтверждён|BITS не подтвердил|Пропущено'){throw ($value -split '\r?\n')[0]}
            }
        } 'byte growth confirmed in the background activity panel' 85
        return @{Message=$message;AwaitGrowth=$false;Action='Packaged WPF';OriginalGuid=$Id;OriginalBytes=$Received}
    }catch{
        $failure=@{PackageSha256=$env:APP_SHA256;Failure=$_.Exception.Message;Guid=$Id;OriginalBytes=$Received}
        $collector=Join-Path $env:ProgramData 'DomainConsoleCSharp\Snapshot41'
        foreach($name in 'state.json','Activity.json','Activity.error','error.txt','run.json'){
            $file=Join-Path $collector $name
            if(Test-Path -LiteralPath $file){$failure[$name]=Get-Content -LiteralPath $file -Raw -Encoding UTF8}
        }
        if($main){
            $controls=@{}
            foreach($name in 'CurrentStage','BackgroundSummary','BackgroundDetails','ContinueBits187'){
                $element=Find-Control $main $ae::AutomationIdProperty $name
                if($element){$controls[$name]=@{Title=$element.Current.Name;Enabled=$element.Current.IsEnabled;Offscreen=$element.Current.IsOffscreen}}
            }
            $failure.Controls=$controls
        }
        $failure | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'evidence.json') -Encoding UTF8
        throw
    }finally{
        if($process -and !$process.HasExited){
            [void]$process.CloseMainWindow()
            if(!$process.WaitForExit(10000)){Stop-Process -Id $process.Id}
        }
        foreach($item in $originals){
            if($null -eq $item.Property){Remove-ItemProperty -LiteralPath $item.Path -Name $item.Name -ErrorAction SilentlyContinue}
            else{Set-ItemProperty -LiteralPath $item.Path -Name $item.Name -Value $item.Property.Value}
        }
    }
}
