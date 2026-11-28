$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or (Get-CimInstance Win32_ComputerSystem).PartOfDomain){
    throw 'Only the disposable workgroup Windows runner is supported.'
}
Import-Module BitsTransfer -ErrorAction Stop
$root=Join-Path $env:RUNNER_TEMP 'dc-bits187-live'
New-Item -Path $root -ItemType Directory -Force | Out-Null
$utf8=[Text.UTF8Encoding]::new($false)
$task='DomainConsoleLab-BitsResume-'+[guid]::NewGuid().ToString('N')
$server=$null
$ids=@()
try {
    $portFile=Join-Path $root 'port.txt'
    $http=Join-Path $PSScriptRoot 'http_fixture.py'
    $server=Start-Process python -ArgumentList ('"'+$http+'" "'+$portFile+'"') -PassThru -NoNewWindow
    $deadline=[DateTime]::UtcNow.AddSeconds(12)
    while(!(Test-Path -LiteralPath $portFile) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 200}
    if($server.HasExited -or !(Test-Path -LiteralPath $portFile)){throw 'Private HTTP fixture did not start'}
    $policy='http://127.0.0.1:'+([int](Get-Content -LiteralPath $portFile -Raw))
    $seed=@'
param([string]$Root,[string]$Policy)
$ErrorActionPreference='Stop'
try {
    Import-Module BitsTransfer -ErrorAction Stop
    $ids=@()
    foreach($key in 'chosen','peer'){
        $label=if($key -eq 'chosen'){'WU Client Download'}else{'Isolated BITS peer'}
        $paused=($key -eq 'peer')
        $job=Start-BitsTransfer -Source ($Policy+'/Content/Lab/'+$key+'.cab') -Destination (Join-Path $Root ($key+'.cab')) -DisplayName $label -Asynchronous -Suspended:$paused -Priority Foreground -ProxyUsage NoProxy
        $ids+=$job.JobId.ToString()
        $ids|Set-Content -LiteralPath (Join-Path $Root 'jobs.txt') -Encoding ASCII
    }
    $deadline=[DateTime]::UtcNow.AddSeconds(32)
    do {
        $active=Get-BitsTransfer -JobId ([guid]$ids[0])
        if($active.BytesTransferred -gt 0 -and $active.BytesTotal -lt [long]::MaxValue){break}
        Start-Sleep -Milliseconds 500
    }while([DateTime]::UtcNow -lt $deadline)
    if($active.BytesTransferred -le 0 -or $active.BytesTransferred -ge $active.BytesTotal){
        throw ('Failed to start a partial native transfer: '+$active.JobState+' · '+$active.ErrorDescription)
    }
    Suspend-BitsTransfer -BitsJob $active -Confirm:$false -ErrorAction Stop
    'seeded'|Set-Content -LiteralPath (Join-Path $Root 'ready.txt')
}catch{
    $_|Out-String|Set-Content -LiteralPath (Join-Path $Root 'seed-error.txt')
    exit 1
}
'@
    $seedPath=Join-Path $root 'seed.ps1'
    [IO.File]::WriteAllText($seedPath,$seed,$utf8)
    $action=New-ScheduledTaskAction -Execute (Join-Path $PSHOME 'powershell.exe') -Argument ('-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$seedPath+'" -Root "'+$root+'" -Policy "'+$policy+'"')
    $limit=New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
    Register-ScheduledTask -TaskName $task -Action $action -Settings $limit -User SYSTEM -RunLevel Highest -Force | Out-Null
    Start-ScheduledTask -TaskName $task
    $deadline=[DateTime]::UtcNow.AddSeconds(50)
    $ready=Join-Path $root 'ready.txt'
    $failure=Join-Path $root 'seed-error.txt'
    while(!(Test-Path -LiteralPath $ready) -and [DateTime]::UtcNow -lt $deadline){
        if(Test-Path -LiteralPath $failure){throw (Get-Content -LiteralPath $failure -Raw)}
        Start-Sleep -Milliseconds 500
    }
    if(!(Test-Path -LiteralPath $ready)){throw 'SYSTEM did not create the two fixture jobs'}
    $ids=@(Get-Content -LiteralPath (Join-Path $root 'jobs.txt') | ForEach-Object { [guid]$_ })
    if($ids.Count -ne 2 -or $ids[0] -eq $ids[1]){throw 'Fixture needs distinct BITS jobs'}
    $chosen=Get-BitsTransfer -AllUsers | Where-Object JobId -eq $ids[0]
    $peer=Get-BitsTransfer -AllUsers | Where-Object JobId -eq $ids[1]
    if($chosen.JobState -ne 'Suspended' -or $peer.JobState -ne 'Suspended'){throw 'Initial jobs are not paused'}
    $file=@($chosen.FileList)[0]
    $policyBefore=@{UseWUServer=1;WUServer=$policy}
    $request=@{
        Id=$ids[0].ToString();State='Suspended';Received=[long]$chosen.BytesTransferred;Total=[long]$chosen.BytesTotal
        RemoteName=$file.RemoteName;LocalName=$file.LocalName;ModifiedUtc=$chosen.ModificationTime.ToUniversalTime().ToString('o')
        PolicySource=$policy;Automatic=$false
    }
    $serviceBefore=Get-CimInstance Win32_Service -Filter "Name='BITS'"
    $source=Join-Path $env:GITHUB_WORKSPACE 'DomainConsole-CSharp/Console/Polling42/bits-resume187.ps1'
    $literal="'"+($request|ConvertTo-Json -Compress).Replace("'","''")+"'"
    $body=[ScriptBlock]::Create([IO.File]::ReadAllText($source).Replace('@@REQUEST@@',$literal))
    $reply=& {
        # Supply the synthetic WSUS policy without changing the runner's Windows Update settings.
        function Get-ItemProperty {param($LiteralPath) $policyBefore}
        function Get-CimInstance {param($ClassName,$Filter)}
        function New-Object {param($ComObject) @{IsBusy=$false}}
        & $body
    } | ConvertFrom-Json
    $deadline=[DateTime]::UtcNow.AddSeconds(35)
    do {
        $after=Get-BitsTransfer -AllUsers | Where-Object JobId -eq $ids[0]
        if($after -and $after.BytesTransferred -gt $request.Received){break}
        Start-Sleep -Seconds 1
    }while([DateTime]::UtcNow -lt $deadline)
    $other=Get-BitsTransfer -AllUsers | Where-Object JobId -eq $ids[1]
    $serviceAfter=Get-CimInstance Win32_Service -Filter "Name='BITS'"
    $proof=@{Version=$env:APP_COMMIT;Initial=$request;Reply=$reply;SameJob=$after.JobId -eq $ids[0]
        State=[string]$after.JobState;Bytes=[long]$after.BytesTransferred;BitsError=[string]$after.ErrorDescription
        OtherState=[string]$other.JobState;OtherBytes=[long]$other.BytesTransferred;SameServicePid=$serviceBefore.ProcessId -eq $serviceAfter.ProcessId}
    [IO.File]::WriteAllText((Join-Path $root 'evidence.json'),($proof|ConvertTo-Json -Depth 6),$utf8)
    if(!$proof.SameJob -or $proof.Bytes -le $request.Received -or $proof.OtherState -ne 'Suspended' -or
       $proof.OtherBytes -ne $peer.BytesTransferred -or !$proof.SameServicePid){
        throw "Expected native byte growth on the original BITS GUID while keeping the other job and service intact. $($reply.Message)"
    }
    Write-Output 'PASS native BITS resumed the original SYSTEM job and left the adjacent job and service intact.'
}finally{
    $seeds=Join-Path $root 'jobs.txt'
    if(Test-Path -LiteralPath $seeds){
        $ids=@(Get-Content -LiteralPath $seeds | ForEach-Object { [guid]$_ })
        Get-BitsTransfer -AllUsers | Where-Object { $_.JobId -in $ids } | Remove-BitsTransfer -Confirm:$false -ErrorAction SilentlyContinue
    }
    Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
    if($server -and !$server.HasExited){Stop-Process -Id $server.Id}
}
