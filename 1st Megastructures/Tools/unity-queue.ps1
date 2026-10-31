# Serialised Unity batchmode runner for this project (Part 5.2). Every Unity batch call (lead, builders, testers) goes through here.
# - Takes a machine-wide named mutex for this project so two Unity processes never touch the project at once.
# - Handles a Unity Editor that already has THIS project open (closes it gracefully, then terminates only if needed).
# - Writes the Unity log to Logs\<Name>.log and its own summary to Logs\<Name>.queue.log.
# - Kills Unity only if its log has not changed for -StallMinutes (default 5) minutes (Part 2.3); a healthy long job is never killed.
#
# Examples:
#   powershell -File Tools\unity-queue.ps1 -Name compile                      (import + compile only)
#   powershell -File Tools\unity-queue.ps1 -Name rebuild -Method Megastructure.EditorTools.BatchTools.RebuildAll
#   powershell -File Tools\unity-queue.ps1 -Name build   -Method Megastructure.EditorTools.BatchTools.BuildWindows
#   powershell -File Tools\unity-queue.ps1 -Name tests   -Tests EditMode
param(
    [string]$Method = "",
    [string]$Name = "batch",
    [string]$Tests = "",            # EditMode or PlayMode
    [string]$TestFilter = "",
    [string[]]$Extra = @(),
    [int]$StallMinutes = 5,
    [switch]$NoGraphics
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
$logDir = Join-Path $root 'Logs'
New-Item -ItemType Directory -Force $logDir | Out-Null
$unityLog = Join-Path $logDir "$Name.log"
$queueLog = Join-Path $logDir "$Name.queue.log"

function QLog([string]$msg) {
    $line = "[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $msg
    Add-Content -Path $queueLog -Value $line
    Write-Output $line
}

Set-Content -Path $queueLog -Value "unity-queue $Name started $(Get-Date -Format o)"

# one mutex per project path
$sha = [System.Security.Cryptography.SHA1]::Create()
$hash = ([System.BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($root.ToLowerInvariant())))).Replace('-', '').Substring(0, 16)
$mutex = New-Object System.Threading.Mutex($false, "Global\AbyssUnityQueue_$hash")
QLog "waiting for project mutex"
try { [void]$mutex.WaitOne() } catch [System.Threading.AbandonedMutexException] { QLog "previous holder abandoned the mutex; continuing" }
$exitCode = 1
try {
    # Single-instance rule: close an Editor that has THIS project open (never touch other projects).
    $procs = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.ToLowerInvariant().Contains($root.ToLowerInvariant()) }
    foreach ($p in $procs) {
        QLog "closing Unity pid $($p.ProcessId) that has this project open"
        $gp = Get-Process -Id $p.ProcessId -ErrorAction SilentlyContinue
        if ($gp) {
            [void]$gp.CloseMainWindow()
            if (-not $gp.WaitForExit(30000)) { QLog "terminating pid $($p.ProcessId)"; Stop-Process -Id $p.ProcessId -Force }
        }
    }
    $lock = Join-Path $root 'Temp\UnityLockfile'
    if (Test-Path $lock) {
        try { Remove-Item $lock -Force -ErrorAction Stop; QLog "removed stale UnityLockfile" } catch { QLog "UnityLockfile in use: $($_.Exception.Message)" }
    }

    $uargs = @('-batchmode', '-projectPath', "`"$root`"", '-logFile', "`"$unityLog`"")
    if ($NoGraphics) { $uargs += '-nographics' }
    if ($Tests) {
        $results = Join-Path $logDir "$Name.results.xml"
        if (Test-Path $results) { Remove-Item $results -Force }
        $uargs += @('-runTests', '-testPlatform', $Tests, '-testResults', "`"$results`"")
        if ($TestFilter) { $uargs += @('-testFilter', "`"$TestFilter`"") }
    } else {
        if ($Method) { $uargs += @('-executeMethod', $Method) }
        $uargs += '-quit'
    }
    $uargs += $Extra
    if (Test-Path $unityLog) { Remove-Item $unityLog -Force }
    QLog ("launch: Unity.exe " + ($uargs -join ' '))
    $proc = Start-Process -FilePath $unity -ArgumentList $uargs -PassThru -WindowStyle Hidden
    $lastLen = -1; $lastChange = Get-Date
    while (-not $proc.HasExited) {
        Start-Sleep -Seconds 3
        $len = if (Test-Path $unityLog) { (Get-Item $unityLog).Length } else { 0 }
        if ($len -ne $lastLen) { $lastLen = $len; $lastChange = Get-Date }
        elseif (((Get-Date) - $lastChange).TotalMinutes -ge $StallMinutes) {
            QLog "log unchanged for $StallMinutes min: killing Unity pid $($proc.Id)"
            Stop-Process -Id $proc.Id -Force
            break
        }
    }
    $proc.WaitForExit()
    $exitCode = $proc.ExitCode
    QLog "Unity exited with code $exitCode"

    if (Test-Path $unityLog) {
        $errs = Select-String -Path $unityLog -Pattern 'error CS\d+|Compilation failed|Scripts have compiler errors|Exception:|\[ABYSS-ERROR\]|Shader error|failed to compile' -CaseSensitive:$false |
            ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique | Select-Object -First 60
        foreach ($e in $errs) { QLog "LOG: $e" }
        $abyss = Select-String -Path $unityLog -Pattern '\[ABYSS\]' | ForEach-Object { $_.Line.Trim() } | Select-Object -Last 40
        foreach ($a in $abyss) { QLog "OUT: $a" }
    }
    if ($Tests) {
        $results = Join-Path $logDir "$Name.results.xml"
        if (Test-Path $results) {
            [xml]$x = Get-Content $results
            $r = $x.'test-run'
            QLog "TESTS: total=$($r.total) passed=$($r.passed) failed=$($r.failed) skipped=$($r.skipped) result=$($r.result)"
            $x.SelectNodes("//test-case[@result='Failed']") | Select-Object -First 40 | ForEach-Object {
                QLog ("FAIL: " + $_.fullname + " :: " + (($_.failure.message.'#cdata-section' | Out-String).Trim() -replace "\s+", ' '))
            }
        } else { QLog "TESTS: no results file" }
    }
}
finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
exit $exitCode
