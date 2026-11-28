# Startup profile for one disposable WinRM endpoint. A one-shot interruption
# trades the normal delivery receipt for a transport failure after Task Scheduler accepted the launch.
if($env:COMPUTERNAME -ne 'DCWSUS' -or !(Test-Path 'C:\DomainConsoleLab\node.json')) {
    throw 'The private fault fixture requires its disposable Windows guest.'
}
function global:Start-ScheduledTask([string]$TaskName) {
    ScheduledTasks\Start-ScheduledTask -TaskName $TaskName -ErrorAction Stop
    if($TaskName -notmatch '^DomainConsoleCSharp-([a-fA-F0-9-]{36})$'){ return }
    $id=$Matches[1]
    $job=Join-Path $env:ProgramData "DomainConsoleCSharp\$id\job.json"
    $audit='C:\DomainConsoleLab\receipt-disconnected.txt'
    if(!(Test-Path $audit) -and (Test-Path $job) -and
        ((Get-Content $job -Raw) -match 'DCLabManualReceipt059')) {
        $remoteHost=[Diagnostics.Process]::GetCurrentProcess()
        if($remoteHost.ProcessName -ne 'wsmprovhost'){throw 'Refusing to interrupt a process outside test WinRM'}
        New-NetFirewallRule -Name DCLabReceiptPaused -DisplayName 'Lab receipt loss recovery hold' `
            -Direction Inbound -Action Block -Protocol TCP -LocalPort 5985 -RemoteAddress 10.77.57.11 | Out-Null
        [IO.File]::WriteAllText($audit,$id)
        $remoteHost.Kill()
    }
}
