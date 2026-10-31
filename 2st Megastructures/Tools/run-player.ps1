<#
.SYNOPSIS
  Runs the built player (Builds\The Ring\The Ring.exe) under the same machine-wide mutex as Tools\unity-queue.ps1,
  so a player run never overlaps a Unity batch call (a build would fail to overwrite a running exe, and a capture
  would read a half-written build).

.EXAMPLE
  powershell -NoProfile -File Tools\run-player.ps1 -Name cap05 -PlayerArgs "-capture","QA\captures\run05","-shots","keys","-res","1920x1080"
  powershell -NoProfile -File Tools\run-player.ps1 -Name at02 -TimeoutSeconds 900 -PlayerArgs "-autotest"

.NOTES
  -logFile is added automatically (Logs\player-<Name>.log) unless PlayerArgs already contain one.
  The script waits for the player to exit, kills it only after -TimeoutSeconds (default 600), and exits with the
  player's exit code (95 on timeout, 90 when the lock could not be acquired).
#>
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [string[]]$PlayerArgs = @(),
    [int]$TimeoutSeconds = 600,
    [int]$LockWaitSeconds = 3600
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$exe = Join-Path $root "Builds\The Ring\The Ring.exe"
$logDir = Join-Path $root "Logs"
New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir "player-$Name.log"

if (-not (Test-Path $exe)) { Write-Output "PLAYER: no build at $exe"; exit 96 }

$mutex = New-Object System.Threading.Mutex($false, "Global\TheRing_UnityQueue_2ndMegastructures")
$acquired = $false
try {
    try { $acquired = $mutex.WaitOne([TimeSpan]::FromSeconds($LockWaitSeconds)) }
    catch [System.Threading.AbandonedMutexException] { $acquired = $true }
    if (-not $acquired) { Write-Output "PLAYER: could not acquire the Unity lock within $LockWaitSeconds s"; exit 90 }

    # powershell -File does not parse arrays: -PlayerArgs "-capture","DIR" arrives as ONE string "-capture,DIR". Split
    # comma lists back into arguments (a value that itself needs a comma, like a shot list, is re-joined below).
    if ($PlayerArgs.Count -eq 1 -and $PlayerArgs[0] -match ",") {
        $parts = $PlayerArgs[0].Split(",")
        $rebuilt = @()
        foreach ($t in $parts) {
            # a token that does not start with "-" and follows a value token belongs to a comma list value (e.g. -shots KS02,KS03)
            if ($rebuilt.Count -ge 2 -and -not $t.StartsWith("-") -and -not $rebuilt[$rebuilt.Count - 1].StartsWith("-")) {
                $rebuilt[$rebuilt.Count - 1] = $rebuilt[$rebuilt.Count - 1] + "," + $t
            } else { $rebuilt += $t }
        }
        $PlayerArgs = $rebuilt
    }
    $argList = @()
    $hasLog = $false
    foreach ($a in $PlayerArgs) { if ($a -eq "-logFile") { $hasLog = $true } }
    foreach ($a in $PlayerArgs) { if ($a -match "\s") { $argList += "`"$a`"" } else { $argList += $a } }
    if (-not $hasLog) { $argList += @("-logFile", "`"$log`"") }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Output ("PLAYER: start " + $Name + " " + ($argList -join " "))
    $p = Start-Process -FilePath $exe -ArgumentList $argList -WorkingDirectory $root -PassThru
    if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
        try { $p.Kill() } catch {}
        Write-Output ("PLAYER: timeout after " + $TimeoutSeconds + " s, killed")
        exit 95
    }
    $code = $p.ExitCode
    $sw.Stop()
    Write-Output ("PLAYER: done " + $Name + " exit=" + $code + " in " + [int]$sw.Elapsed.TotalSeconds + " s; log " + $log)
    if ((-not $hasLog) -and (Test-Path $log)) {
        $errs = Select-String -Path $log -Pattern "Exception|error|\[QA\]|Shader .* not supported|magenta" |
            Where-Object { $_.Line -notmatch "^\s*$" } | Select-Object -First 30 | ForEach-Object { $_.Line }
        if ($errs) { $errs | ForEach-Object { Write-Output ("  " + $_) } }
    }
    exit $code
}
finally {
    if ($acquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
