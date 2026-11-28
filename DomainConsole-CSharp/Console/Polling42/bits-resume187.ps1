$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$request=@@REQUEST@@ | ConvertFrom-Json -ErrorAction Stop

function Reply([string]$message,[bool]$awaitGrowth=$false) {
    @{Message=$message;AwaitGrowth=$awaitGrowth;CollectedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Compress
}

function Owns-BitsJob($job) {
    if ($job.DisplayName -ne 'WU Client Download') { return $false }
    try {
        return ([Security.Principal.NTAccount]::new([string]$job.OwnerAccount).Translate(
            [Security.Principal.SecurityIdentifier]).Value -eq 'S-1-5-18')
    } catch { return $false }
}

function Same-Transfer($job) {
    $files=@($job.FileList)
    return ($files.Count -eq 1 -and [uri]$files[0].RemoteName -eq $url -and
        $files[0].LocalName -eq [string]$request.LocalName)
}

function Matching-Files($job) {
    foreach ($file in @($job.FileList)) {
        $other=$null
        if (([uri]::TryCreate([string]$file.RemoteName,[UriKind]::Absolute,[ref]$other) -and $other -eq $url) -or
            $file.LocalName -eq [string]$request.LocalName) { return $true }
    }
    return $false
}

function Confirm-ActiveUpdate($folder) {
    $operation=[guid]::Empty
    if (![guid]::TryParse([string]$request.Operation,[ref]$operation) -or $operation -eq [guid]::Empty) { return $false }
    $jobFolder=Join-Path $folder $operation.ToString('D')
    $statusPath=Join-Path $jobFolder 'status.json'
    foreach ($path in $folder,$jobFolder,$statusPath) {
        if (!(Test-Path -LiteralPath $path) -or ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            return $false
        }
    }
    $state=Get-Content -LiteralPath $statusPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $proof=$state.ManagedBits187
    if ($state.Status -ne 'Running' -or $state.ManagedSource54 -ne $true -or !$proof -or
        [guid]$proof.Id -ne $id -or [guid]$proof.UpdateId -ne [guid]$request.Proof.UpdateId -or
        $proof.Revision -lt 1 -or $proof.Revision -ne $request.Proof.Revision -or [uri]$proof.Url -ne $url) { return $false }
    $seen=[DateTimeOffset]::Parse([string]$proof.ObservedUtc)
    if ($seen -gt [DateTimeOffset]::UtcNow -or [DateTimeOffset]::UtcNow-$seen -gt [TimeSpan]::FromSeconds(90)) { return $false }
    $executor=Get-Process -Id ([int]$state.ExecutorPid37) -ErrorAction SilentlyContinue
    if (!$executor -or $executor.ProcessName -ne 'DomainConsole.Agent') { return $false }
    return ([Math]::Abs(($executor.StartTime.ToUniversalTime()-[datetime]::Parse($state.ExecutorStarted37).ToUniversalTime()).TotalSeconds) -lt 2)
}

function Report-LocalWindowsUpdate([datetime]$after) {
    try {
        $session=New-Object -ComObject Microsoft.Update.Session
        if ($request.Proof -and $request.Proof.UpdateId -and $request.Proof.Revision -gt 0) {
            $searcher=$session.CreateUpdateSearcher()
            $limit=[Math]::Min(20,$searcher.GetTotalHistoryCount())
            if ($limit -gt 0) {
                $result=@($searcher.QueryHistory(0,$limit) | Where-Object {
                    $_.Operation -eq 1 -and $_.Date.ToUniversalTime() -ge $after -and
                    $_.UpdateIdentity.UpdateID -eq [string]$request.Proof.UpdateId -and
                    $_.UpdateIdentity.RevisionNumber -eq [int]$request.Proof.Revision
                } | Select-Object -First 1)
                if ($result.Count) {
                    return ('Локальная история Windows Update · код результата '+$result[0].ResultCode+
                        ', HRESULT 0x'+([int]$result[0].HResult).ToString('X8')+'.')
                }
            }
        }
        if ($session.CreateUpdateInstaller().IsBusy) { return 'Установщик Windows Update занят. Итог этого пакета уточняется.' }
        return 'Локальный результат Windows Update пока не подтверждён.'
    } catch {
        $errorCode=([int]$_.Exception.GetBaseException().HResult).ToString('X8')
        if ($errorCode -eq '80240009') { return 'Windows Update занят другой операцией (0x80240009). Дождитесь её завершения.' }
        return ('Ошибка отдельной проверки Windows Update · HRESULT 0x'+$errorCode+' · '+$_.Exception.Message)
    }
}

$id=[guid]::Empty
$url=$null
$policy=$null
if (![guid]::TryParse([string]$request.Id,[ref]$id) -or $id -eq [guid]::Empty -or
    ![uri]::TryCreate([string]$request.RemoteName,[UriKind]::Absolute,[ref]$url) -or
    ![uri]::TryCreate([string]$request.PolicySource,[UriKind]::Absolute,[ref]$policy) -or
    $url.Scheme -notin 'http','https' -or $url.Scheme -ne $policy.Scheme -or $url.Host -ne $policy.Host -or
    $url.Port -ne $policy.Port -or !$url.AbsolutePath.StartsWith('/Content/',[StringComparison]::OrdinalIgnoreCase)) {
    Reply 'Пропущено · источник выбранного задания WSUS не подтверждён.'
    return
}
$mutex=[Threading.Mutex]::new($false,('Global\DomainConsole-BitsResume-'+$id.ToString('N')))
$locked=$false
try {
    try { $locked=$mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked=$true }
    if (!$locked) { Reply 'Запрос для этого задания BITS уже выполняется.'; return }
    $policyKey='HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
    if ((Get-ItemProperty -LiteralPath (Join-Path $policyKey 'AU')).UseWUServer -ne 1 -or
        [uri](Get-ItemProperty -LiteralPath $policyKey).WUServer -ne $policy) {
        Reply 'Пропущено · политика WSUS изменилась.'
        return
    }
    Import-Module BitsTransfer -ErrorAction Stop
    $transfers=@(Get-BitsTransfer -AllUsers -ErrorAction Stop)
    $jobs=@($transfers | Where-Object JobId -eq $id)
    if ($jobs.Count -ne 1) { Reply 'Пропущено · выбранное задание исчезло или не определяется однозначно.'; return }
    $job=$jobs[0]
    if (!(Owns-BitsJob $job) -or !(Same-Transfer $job) -or
        @($transfers | Where-Object { Matching-Files $_ }).Count -ne 1) {
        Reply 'Пропущено · изменился состав передачи или обнаружен дубль.'
        return
    }
    if ($job.JobState -ne $request.State -or $job.JobState -notin 'Suspended','Queued' -or
        [decimal]$job.BytesTransferred -ne [decimal]$request.Received -or [decimal]$job.BytesTotal -ne [decimal]$request.Total -or
        [decimal]$job.BytesTransferred -ge [decimal]$job.BytesTotal -or
        $job.ModificationTime.ToUniversalTime() -ne ([datetime]$request.ModifiedUtc).ToUniversalTime()) {
        Reply 'Пропущено · состояние или байты изменились. Обновите снимок.'
        return
    }
    if ((New-Object -ComObject Microsoft.Update.Installer).IsBusy -or
        @(Get-CimInstance Win32_Process -Filter "Name='TiWorker.exe' OR Name='TrustedInstaller.exe' OR Name='MRT.exe'").Count) {
        Reply 'Пропущено · обнаружена установка или обслуживание Windows.'
        return
    }

    if ($request.Automatic) {
        $root=Join-Path $env:ProgramData 'DomainConsoleCSharp'
        if ($job.JobState -ne 'Queued' -or !(Confirm-ActiveUpdate $root)) {
            Reply 'Пропущено · текущее скачивание выбранного пакета не подтверждено.'
            return
        }
        $controls=Join-Path $root 'BitsResume'
        if (!(Test-Path -LiteralPath $controls)) { New-Item -Path $controls -ItemType Directory -Force | Out-Null }
        if ((Get-Item -LiteralPath $controls -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Каталог попыток BITS является ссылкой.'
        }
        $marker=Join-Path $controls ($id.ToString('N')+'.attempt')
        if (Test-Path -LiteralPath $marker) { Reply 'Автоматическая попытка для этого GUID уже была отмечена.'; return }
        $handle=[IO.File]::Open($marker,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        $handle.Dispose()
        $active=@($transfers | ForEach-Object { $_.JobId.ToString('N') })
        Get-ChildItem -LiteralPath $controls -File -Filter '*.attempt' | Where-Object {
            $_.BaseName -match '^[0-9a-f]{32}$' -and $_.BaseName -notin $active -and
            $_.LastWriteTimeUtc -lt [DateTime]::UtcNow.AddDays(-30) -and !($_.Attributes -band [IO.FileAttributes]::ReparsePoint)
        } | Select-Object -First 16 | Remove-Item -ErrorAction SilentlyContinue
    }

    $changedAt=[DateTime]::UtcNow
    try {
        if ($job.JobState -eq 'Queued') { Suspend-BitsTransfer -BitsJob $job -Confirm:$false -ErrorAction Stop | Out-Null }
        Resume-BitsTransfer -BitsJob $job -Asynchronous -Confirm:$false -ErrorAction Stop | Out-Null
    } catch {
        Reply ('BITS не подтвердил запрос продолжения · '+$_.Exception.Message)
        return
    }
    Start-Sleep -Seconds 3
    try { $after=@(Get-BitsTransfer -AllUsers -ErrorAction Stop | Where-Object JobId -eq $id) }
    catch { Reply ('Запрос принят. Повторное чтение BITS не удалось · '+$_.Exception.Message) $true; return }
    if ($after.Count -eq 0) {
        Reply ('Запрос принят. Задание больше не видно в BITS. '+(Report-LocalWindowsUpdate $changedAt))
    } elseif ($after.Count -eq 1 -and (Same-Transfer $after[0]) -and [decimal]$after[0].BytesTransferred -gt [decimal]$request.Received) {
        Reply ('Передача продолжилась · получено '+([decimal]$after[0].BytesTransferred-[decimal]$request.Received)+' новых байт.')
    } elseif ($after.Count -eq 1 -and (Same-Transfer $after[0])) {
        Reply ('Запрос принят. Рост байтов пока не подтверждён · BITS '+$after[0].JobState+'.') $true
    } else { Reply 'Запрос принят. Текущее состояние выбранной передачи не подтверждено.' }
} finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
