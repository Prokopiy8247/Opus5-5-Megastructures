# Fast type-check of all game C# without Unity (seconds). Safe to run concurrently: every run uses its own obj/bin folder.
# Usage: powershell -NoProfile -File Tools\fastcompile.ps1 [-RuntimeOnly]
# Exit code 0 = both projects compile. Authoritative compile remains Unity batchmode via Tools\unity-queue.ps1.
param([switch]$RuntimeOnly)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$runDir = Join-Path $PSScriptRoot ("fastcompile\runs\" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $runDir | Out-Null
$env:FC_RUN = $runDir
$fail = 0
$projects = @("$PSScriptRoot\fastcompile\Runtime\FastRuntime.csproj")
if (-not $RuntimeOnly) { $projects += "$PSScriptRoot\fastcompile\Editor\FastEditor.csproj" }
try {
    foreach ($proj in $projects) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($proj)
        $out = & dotnet build $proj -nologo -v:q -clp:NoSummary -nodeReuse:false 2>&1
        $errs = $out | Select-String -Pattern ': error ' | ForEach-Object { $_.Line.Replace($root + '\', '') -replace '\s*\[[^\]]*\.csproj\]$', '' } | Sort-Object -Unique
        if ($LASTEXITCODE -ne 0 -or $errs) {
            $fail = 1
            Write-Output "[$name] FAILED ($(@($errs).Count) errors)"
            $errs | Select-Object -First 80 | ForEach-Object { Write-Output "  $_" }
        } else {
            Write-Output "[$name] OK"
        }
    }
}
finally {
    Remove-Item -Recurse -Force $runDir -ErrorAction SilentlyContinue
}
exit $fail


