param([Parameter(Mandatory)][string]$Template,[Parameter(Mandatory)][string]$Fixture)
$ErrorActionPreference='Stop'
$raw=[IO.File]::ReadAllText($Template)
function Import-Module {}
function Get-CimInstance { param($ClassName,$Filter) }
function Get-ItemProperty {
    param($LiteralPath)
    @{UseWUServer=1;WUServer='http://wsus.fixture.invalid:8530'}
}
function New-Object {
    param($ComObject)
    if($ComObject -eq 'Microsoft.Update.Session') {
        $session=[pscustomobject]@{}
        $session | Add-Member ScriptMethod CreateUpdateInstaller { @{IsBusy=$false} }
        return $session
    }
    @{IsBusy=$script:busy}
}
function Get-BitsTransfer {
    param([switch]$AllUsers)
    $script:reads++
    if($script:case -eq 'disappear' -and $script:reads -gt 1){return}
    if($script:case -eq 'read-fails' -and $script:reads -gt 1){throw 'snapshot denied'}
    $script:job
    if($script:case -eq 'duplicate'){$script:duplicate}
}
function Suspend-BitsTransfer {
    param($BitsJob,[switch]$Confirm)
    $script:calls+='Suspend'
}
function Resume-BitsTransfer {
    param($BitsJob,[switch]$Asynchronous,[switch]$Confirm)
    if(!$Asynchronous){throw 'synchronous Resume'}
    $script:calls+='Resume'
    if($script:case -eq 'denied'){throw 'access denied'}
    if($script:case -eq 'growth'){$BitsJob.BytesTransferred+=5}
}
function Start-Sleep { param($Seconds) }

$id=[guid]::NewGuid()
$at=[datetime]::UtcNow
foreach($name in 'paused','queued','moving','foreign','duplicate','busy','bytes-changed','missing','denied','growth','disappear','read-fails'){
    $script:case=$name
    $request=Get-Content -LiteralPath $Fixture -Raw -Encoding UTF8 | ConvertFrom-Json
    $request.Id=$id.ToString()
    $request.ModifiedUtc=$at.ToString('o')
    if($name -eq 'paused'){$request.State='Suspended'}
    $state=if($name -eq 'moving'){'Transferring'}else{$request.State}
    $owner=if($name -eq 'foreign'){'NT AUTHORITY\LOCAL SERVICE'}else{'NT AUTHORITY\SYSTEM'}
    $script:job=[pscustomobject]@{
        DisplayName='WU Client Download'; OwnerAccount=$owner; JobId=if($name -eq 'missing'){[guid]::NewGuid()}else{$id}
        JobState=$state; BytesTransferred=if($name -eq 'bytes-changed'){[long]$request.Received+1}else{[long]$request.Received}
        BytesTotal=[long]$request.Total; ModificationTime=$at
        FileList=@(@{RemoteName=$request.RemoteName;LocalName=$request.LocalName})
    }
    $script:duplicate=[pscustomobject]@{JobId=[guid]::NewGuid();FileList=$script:job.FileList}
    $script:busy=($name -eq 'busy')
    $script:reads=0
    $script:calls=@()
    $literal="'"+($request|ConvertTo-Json -Compress).Replace("'","''")+"'"
    $result=& ([ScriptBlock]::Create($raw.Replace("'{}'",$literal))) | ConvertFrom-Json
    $expected=switch($name) {
        'paused' {'Resume'}
        {$_ -in 'queued','denied','growth','disappear','read-fails'} {'Suspend,Resume'}
        default {''}
    }
    if(($script:calls -join ',') -ne $expected){throw "Wrong BITS actions for ${name}: $($script:calls -join ',') · $($result.Message)"}
    if(($name -in 'queued','paused','read-fails') -ne [bool]$result.AwaitGrowth){throw "Wrong continuation state for ${name}"}
    if($name -eq 'disappear' -and $result.Message -notmatch 'больше не видно.+Windows Update'){throw 'Missing job was not diagnosed separately'}
    if($name -eq 'growth' -and $result.Message -notmatch '5 новых байт'){throw 'Byte growth was not confirmed'}
    if($name -eq 'denied' -and $result.Message -notmatch 'access denied'){throw 'Resume failure was hidden'}
    Write-Output "PASS BITS resume $name"
}
