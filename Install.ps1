$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseExe = Join-Path $root 'TaskbarFetch.exe'
$distExe = Join-Path $root 'dist\TaskbarFetch.exe'
$build = Join-Path $root 'Build.cmd'
$installDir = Join-Path $env:LOCALAPPDATA 'TaskbarFetch'
$installExe = Join-Path $installDir 'TaskbarFetch.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

# Release packages contain TaskbarFetch.exe next to this script. Source checkouts
# normally build into dist\TaskbarFetch.exe. Support both layouts.
if (Test-Path $releaseExe) {
    $sourceExe = $releaseExe
}
elseif (Test-Path $distExe) {
    $sourceExe = $distExe
}
elseif (Test-Path $build) {
    Write-Host 'Building TaskbarFetch from source...'
    & $build
    if ($LASTEXITCODE -ne 0) {
        throw "TaskbarFetch build failed with exit code $LASTEXITCODE."
    }
    if (-not (Test-Path $distExe)) {
        throw 'Build completed but dist\TaskbarFetch.exe was not found.'
    }
    $sourceExe = $distExe
}
else {
    throw 'TaskbarFetch.exe was not found and this package does not contain Build.cmd.'
}

Get-Process -Name 'TaskbarFetch' -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 250

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -Path $sourceExe -Destination $installExe -Force

# Start automatically for this Windows user. No administrator rights are required.
New-Item -Path $runKey -Force | Out-Null
New-ItemProperty -Path $runKey -Name 'TaskbarFetch' -Value ('"' + $installExe + '"') -PropertyType String -Force | Out-Null

Start-Process -FilePath $installExe

Write-Host ''
Write-Host 'TaskbarFetch is installed and running.' -ForegroundColor Green
Write-Host "Installed to: $installExe"
Write-Host 'It will start automatically when you sign in.'
Write-Host 'Use the TaskbarFetch system-tray icon to Pause/Resume, change startup, open logs, or Exit.'
