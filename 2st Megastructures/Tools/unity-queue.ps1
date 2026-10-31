<#
.SYNOPSIS
  Serialised Unity batch runner for The Ring. Every Unity batch call (lead, builders, testers) goes through this script.

.DESCRIPTION
  Takes a machine-wide named mutex so that only one Unity process ever works on this project, refuses to start
  while an interactive Editor has this project open, runs one batch call, writes its own log and an .exit file,
  prints a short error digest and releases the lock.

.EXAMPLE
  powershell -NoProfile -File Tools\unity-queue.ps1 -Name compile -Method Megastructure.EditorTools.BatchTools.CompileCheck
  powershell -NoProfile -File Tools\unity-queue.ps1 -Name build -Method Megastructure.EditorTools.BatchTools.BuildWindows
#>
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$Method = "",
    [string[]]$ExtraArgs = @(),
    [switch]$NoQuit,
    [int]$LockWaitSeconds = 3600
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
$logDir = Join-Path $root "Logs"
New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir "$Name.log"
$exitFile = Join-Path $logDir "$Name.exit"
if (Test-Path $exitFile) { Remove-Item $exitFile -Force }

$mutex = New-Object System.Threading.Mutex($false, "Global\TheRing_UnityQueue_2ndMegastructures")
$acquired = $false
try {
    try { $acquired = $mutex.WaitOne([TimeSpan]::FromSeconds($LockWaitSeconds)) }
    catch [System.Threading.AbandonedMutexException] { $acquired = $true }
    if (-not $acquired) { Write-Output "QUEUE: could not acquire Unity lock within $LockWaitSeconds s"; exit 90 }

    # Refuse to run while another Unity process has this project open (single-instance rule, Part 3).
    $others = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine -like "*2nd Megastructures\2nd Megastructures*" }
    if ($others) {
        Write-Output ("QUEUE: another Unity process has this project open: " + (($others | ForEach-Object { $_.ProcessId }) -join ","))
        exit 91
    }
    $lockFile = Join-Path $root "Temp\UnityLockfile"
    if (Test-Path $lockFile) {
        try { Remove-Item $lockFile -Force -ErrorAction Stop; Write-Output "QUEUE: removed stale Temp\UnityLockfile" }
        catch { Write-Output "QUEUE: Temp\UnityLockfile is held by a live process"; exit 92 }
    }

    $argList = @("-batchmode", "-projectPath", "`"$root`"", "-logFile", "`"$log`"")
    if (-not $NoQuit) { $argList += "-quit" }
    if ($Method -ne "") { $argList += @("-executeMethod", $Method) }
    $argList += $ExtraArgs

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Output ("QUEUE: start " + $Name + " " + ($argList -join " "))
    $p = Start-Process -FilePath $unity -ArgumentList $argList -PassThru -WindowStyle Hidden
    $p.WaitForExit()
    $code = $p.ExitCode
    $sw.Stop()
    Set-Content -Path $exitFile -Value $code -Encoding ascii
    Write-Output ("QUEUE: done " + $Name + " exit=" + $code + " in " + [int]$sw.Elapsed.TotalSeconds + " s; log " + $log)

    if (Test-Path $log) {
        $errs = Select-String -Path $log -Pattern "error CS\d+|Shader error|Compilation failed|Exception:|\[BATCH\]|\[TEST\]|\[QA\]|Build (succeeded|failed)|Scripts have compiler errors" -SimpleMatch:$false |
            Select-Object -First 80 | ForEach-Object { $_.Line }
        if ($errs) { $errs | ForEach-Object { Write-Output ("  " + $_) } }
    }
    exit $code
}
finally {
    if ($acquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
