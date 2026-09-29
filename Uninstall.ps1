$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'TaskbarFetch'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

Get-Process -Name 'TaskbarFetch' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 250

if (Test-Path $runKey) {
    Remove-ItemProperty -Path $runKey -Name 'TaskbarFetch' -ErrorAction SilentlyContinue
}

if (Test-Path $installDir) {
    Remove-Item -Path $installDir -Recurse -Force
}

Write-Host 'TaskbarFetch has been removed.' -ForegroundColor Green
