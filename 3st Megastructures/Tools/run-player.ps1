<#
.SYNOPSIS
  Runs the built Windows player (Builds/Thread/Thread.exe) with QA flags, waits for it, and reports.
  Windowed 1920x1080 by default; captures render through RenderTextures at any size (Part 11.2).
.EXAMPLE
  powershell -NoProfile -File Tools\run-player.ps1 -Name autotest -PlayerArgs "-autotest"
  powershell -NoProfile -File Tools\run-player.ps1 -Name keyshots -PlayerArgs "-capture","QA\captures\run1","-capture-set","keyshots","-freeze-dither"
#>
param(
  [Parameter(Mandatory = $true)][string]$Name,
  [string[]]$PlayerArgs = @(),
  [int]$Width = 1920,
  [int]$Height = 1080,
  [int]$TimeoutMin = 60,
  [int]$StallMin = 6
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "Builds\Thread\Thread.exe"
if (-not (Test-Path $exe)) { Write-Output "player not found: $exe"; exit 98 }
$logDir = Join-Path $root "Logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Force $logDir | Out-Null }
$log = Join-Path $logDir "player-$Name.log"
if (Test-Path $log) { Remove-Item $log -Force }

# a list passed through 'powershell -File' arrives as one comma-joined string: split it
if ($PlayerArgs.Count -eq 1 -and $PlayerArgs[0] -match ',') { $PlayerArgs = $PlayerArgs[0].Split(',') }
# resolve relative paths in the argument list against the project root (the player's cwd differs)
$resolved = @()
for ($i = 0; $i -lt $PlayerArgs.Count; $i++) {
  $a = $PlayerArgs[$i]
  if ($i -gt 0 -and @("-capture", "-perf-log", "-audio-dump", "-report-dir") -contains $PlayerArgs[$i - 1] -and -not [System.IO.Path]::IsPathRooted($a)) {
    $a = Join-Path $root $a
  }
  if ($a -match '\s') { $resolved += "`"$a`"" } else { $resolved += $a }
}
$argList = @("-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height", "-logFile", "`"$log`"", "-report-dir", "`"$(Join-Path $root 'QA\reports')`"") + $resolved
Write-Output ("start: Thread.exe " + ($argList -join " "))
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $exe -ArgumentList $argList -PassThru -WorkingDirectory (Split-Path $exe)
$lastSize = -1; $lastChange = Get-Date
while (-not $p.HasExited) {
  Start-Sleep -Milliseconds 1000
  $size = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
  if ($size -ne $lastSize) { $lastSize = $size; $lastChange = Get-Date }
  if (((Get-Date) - $lastChange).TotalMinutes -gt $StallMin) { Write-Output "log stalled $StallMin min: killing"; Stop-Process -Id $p.Id -Force; break }
  if ($sw.Elapsed.TotalMinutes -gt $TimeoutMin) { Write-Output "timeout: killing"; Stop-Process -Id $p.Id -Force; break }
}
$p.WaitForExit()
$code = $p.ExitCode
Write-Output ("exit code {0} after {1:N1} s; log {2}" -f $code, $sw.Elapsed.TotalSeconds, $log)
if (Test-Path $log) {
  $errs = Select-String -Path $log -Pattern "Exception|\[THREAD-FAIL\]|^.*Error.*$" | Where-Object { $_.Line -notmatch "d3d11: failed to create staging|Burst" } | Select-Object -First 30
  Write-Output ("error-like lines: {0}" -f @($errs).Count)
  foreach ($e in $errs) { Write-Output ("  " + $e.Line.Trim()) }
  $tags = Select-String -Path $log -Pattern "\[THREAD\]" | Select-Object -Last 30
  foreach ($t in $tags) { Write-Output ("  " + $t.Line.Trim()) }
}
exit $code
