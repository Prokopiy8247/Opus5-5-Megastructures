# Look-development loop: build the player into its own folder, capture shots in it, compute metrics and a contact sheet.
# Safe for parallel agents: Unity builds are serialised by unity-queue.ps1's mutex; each agent uses its own -Slot.
#
#   powershell -NoProfile -File Tools\lookdev.ps1 -Slot hall -Run r01 -Shots KS4            (build + capture + metrics)
#   powershell -NoProfile -File Tools\lookdev.ps1 -Slot hall -Run r02 -Shots keys -NoBuild   (reuse the last build)
#   extra player args: -Extra "-nopost","-dither","off"
#
# Outputs: Builds/lookdev_<Slot>/Abyss.exe, QA/captures/<Slot>_<Run>/*.png + report.json, QA/metrics/<Slot>_<Run>/,
#          QA/contact/<Slot>_<Run>_p01.jpg. Prints the metric verdict per shot. Exit code 0 when the capture ran.
param(
    [Parameter(Mandatory)][string]$Slot,
    [Parameter(Mandatory)][string]$Run,
    [string]$Shots = "keys",
    [string[]]$Extra = @(),
    [switch]$NoBuild,
    [int]$TimeoutSec = 900
)
$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildDir = "Builds/lookdev_$Slot"
$exe = Join-Path $root "$buildDir\Abyss.exe"
if (-not $NoBuild) {
    $env:ABYSS_BUILD_DIR = $buildDir
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'unity-queue.ps1') -Name "lookdev_$Slot" -Method Megastructure.EditorTools.BatchTools.BuildWindows | Select-Object -Last 3
    Remove-Item Env:\ABYSS_BUILD_DIR
    if ($LASTEXITCODE -ne 0) { Write-Output "BUILD FAILED (see Logs\lookdev_$Slot.log)"; exit 2 }
}
if (-not (Test-Path $exe)) { Write-Output "no exe at $exe"; exit 2 }
$name = "${Slot}_$Run"
$out = Join-Path $root "QA\captures\$name"
$log = Join-Path $root "Logs\player_$name.log"
$args2 = @('-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080', '-capture', "`"$out`"", '-shots', $Shots, '-logFile', "`"$log`"") + $Extra
$p = Start-Process -FilePath $exe -ArgumentList $args2 -PassThru
if (-not $p.WaitForExit($TimeoutSec * 1000)) { Stop-Process -Id $p.Id -Force; Write-Output "capture killed after $TimeoutSec s" }
Write-Output "capture exit $($p.ExitCode) -> $out"
Select-String -Path $log -Pattern '\[ABYSS-ERROR\]|Exception' | Select-Object -First 10 | ForEach-Object { "LOG: " + $_.Line.Trim() }
& python -I (Join-Path $root 'Tools\qa\image_metrics.py') $out | Select-String -Pattern 'PASS|FAIL|image_metrics:' | ForEach-Object { $_.Line }
& python -I (Join-Path $root 'Tools\qa\contact_sheet.py') $out | Select-Object -Last 3
exit 0

