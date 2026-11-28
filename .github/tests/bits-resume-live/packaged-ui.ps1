. (Join-Path $PSScriptRoot 'window-discovery.ps1')

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
        $main=Wait-Control {Application-Window 'DomainConsole ·' $true} 'the WPF window' 100
        $wsusView=Wait-Control {Find-Control $main $ae::AutomationIdProperty 'WsusNavigation33'} 'the WSUS view selector' 30
        $select=$wsusView.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)
        if(!$select.Current.IsSelected){
            Add-Type -AssemblyName System.Windows.Forms
            $wsusView.SetFocus()
            [Windows.Forms.SendKeys]::SendWait(' ')
        }
        $collectorResult=Join-Path $env:ProgramData 'DomainConsoleCSharp\Snapshot41\result.json'
        $null=Wait-Control {
            if(Test-Path -LiteralPath $collectorResult){
                $snapshot=[IO.File]::ReadAllText($collectorResult)|ConvertFrom-Json
                if($snapshot.Healthy -and @($snapshot.Activity.Downloads|Where-Object {
                    $_.Id -eq $Id -and $_.State -eq 'Suspended' -and $_.Received -eq $Received
                }).Count -eq 1){$snapshot}
            }
        } 'the collector result for the exact suspended BITS job' 95
        Start-Sleep -Seconds 12
        if(!$select.Current.IsSelected){throw 'WSUS is not the selected view'}
        $bounds=$main.Current.BoundingRectangle
        if([Math]::Abs($bounds.Width-1044) -gt 25 -or [Math]::Abs($bounds.Height-780) -gt 25){
            throw "Unexpected window size for the verified screenshot layout: $bounds"
        }
        # The WPF tab template renders its content without UI Automation descendants.
        # Click the button inside the exact window using the recorded 1044x780 layout.
        if(![DCLabNativeWindows]::ClickVisible([IntPtr]$main.Current.NativeWindowHandle,[int]($bounds.X+370),[int]($bounds.Y+450))){
            throw 'The verified rendered button is not on the visible console window'
        }
        $dialog=Wait-Control {Application-Window ("Продолжить скачивание · "+$computer)} 'the selection dialog' 45
        $combo=$dialog.FindFirst($desc,[Windows.Automation.PropertyCondition]::new($ae::ControlTypeProperty,[Windows.Automation.ControlType]::ComboBox))
        $selection=$combo.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
        $labels=@($selection|ForEach-Object {$_.Current.Name})
        if($selection.Count -ne 1 -or ($labels -join ' ') -notlike "*$Id*"){throw "Dialog did not select the exact BITS job: $labels"}
        $confirm=Find-Control $dialog $ae::NameProperty 'Продолжить'
        $confirm.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        $message=Wait-Control {
            foreach($file in Get-ChildItem (Join-Path $data 'Events') -Recurse -Filter '*.jsonl' -File -ErrorAction SilentlyContinue){
                foreach($line in [IO.File]::ReadAllLines($file.FullName)){
                    $event=$line|ConvertFrom-Json
                    if($event.Operation36 -eq "bits-resume|$Id" -and $event.Text -eq 'Продолжение скачивания BITS'){
                        if($event.Details -match 'Передача продолжилась .+ новых байт'){return [string]$event.Details}
                        if($event.Details -match 'Исход запроса не подтверждён|BITS не подтвердил|Пропущено'){throw $event.Details}
                    }
                }
            }
        } 'byte growth confirmed in the client journal' 85
        return @{Message=$message;AwaitGrowth=$false;Action='Packaged WPF';OriginalGuid=$Id;OriginalBytes=$Received}
    }catch{
        $failure=@{PackageSha256=$env:APP_SHA256;Failure=$_.Exception.Message;Guid=$Id;OriginalBytes=$Received}
        $collector=Join-Path $env:ProgramData 'DomainConsoleCSharp\Snapshot41'
        foreach($name in 'state.json','Activity.json','Activity.error','error.txt','run.json'){
            $file=Join-Path $collector $name
            if(Test-Path -LiteralPath $file){
                $content=[IO.File]::ReadAllText($file)
                $failure[$name]=$content.Substring(0,[Math]::Min(8192,$content.Length))
            }
        }
        if($process -and !$process.HasExited){$failure.WindowInventory=[DCLabNativeWindows]::Inventory($process.Id)}
        if($main){
            $controls=@{}
            foreach($name in 'WsusNavigation33','CurrentStage','BackgroundGroup','BackgroundSummary','BackgroundDetails','ContinueBits187'){
                $element=Find-Control $main $ae::AutomationIdProperty $name
                if($element){$controls[$name]=@{Title=$element.Current.Name;Enabled=$element.Current.IsEnabled;Offscreen=$element.Current.IsOffscreen}}
            }
            $failure.Controls=$controls
            $condition=[Windows.Automation.Condition]::TrueCondition
            $failure.AutomationTree=@($main.FindAll($desc,$condition)|Select-Object -First 200|ForEach-Object {
                [string]$_.Current.AutomationId+'|'+[string]$_.Current.ControlType.ProgrammaticName+'|'+[string]$_.Current.Name
            })
            try{
                Add-Type -AssemblyName System.Drawing
                $bounds=$main.Current.BoundingRectangle
                if($bounds.Width -gt 0 -and $bounds.Height -gt 0 -and $bounds.Width -le 4096 -and $bounds.Height -le 2160){
                    $image=[Drawing.Bitmap]::new([int]$bounds.Width,[int]$bounds.Height)
                    $graphics=[Drawing.Graphics]::FromImage($image)
                    $hdc=$graphics.GetHdc()
                    try {$captured=[DCLabNativeWindows]::PrintWindow([IntPtr]$main.Current.NativeWindowHandle,$hdc,2)}
                    finally {$graphics.ReleaseHdc($hdc);$graphics.Dispose()}
                    if($captured){$image.Save((Join-Path $root 'window.png'),[Drawing.Imaging.ImageFormat]::Png)}
                    $image.Dispose()
                }
            }catch{$failure.ScreenshotError=$_.Exception.Message}
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
