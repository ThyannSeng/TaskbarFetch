[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'TaskbarFetch-Version.ps1')
$versionInfo = Get-TaskbarFetchVersion -RepositoryRoot $root
$version = $versionInfo.InformationalVersion

$dotnetCommand = Get-Command 'dotnet' -ErrorAction Stop
$sdkVersion = (& $dotnetCommand.Source --version).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Could not determine the installed .NET SDK version (exit code $LASTEXITCODE)."
}

$sdkMatch = [regex]::Match($sdkVersion, '^(\d+)\.(\d+)\.(\d+)(?:[-+].*)?$')
if (-not $sdkMatch.Success -or [int]$sdkMatch.Groups[1].Value -lt 10) {
    throw "TaskbarFetch requires the .NET 10 SDK or newer. Detected '$sdkVersion'. Install the .NET SDK, then run TaskbarFetch-Portable-Build.cmd again."
}

$projectPath = Join-Path $root 'TaskbarFetch.csproj'
$builtOutputPath = Join-Path $root "bin\Release\net48\$($versionInfo.PortableFileName)"
$outputPath = Join-Path $root $versionInfo.PortableFileName
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "TaskbarFetch project file was not found at '$projectPath'."
}

Write-Host "Building TaskbarFetch portable v$version with .NET SDK $sdkVersion..."
& $dotnetCommand.Source build $projectPath --configuration Release --nologo --verbosity minimal
$buildExitCode = $LASTEXITCODE
if ($buildExitCode -ne 0) {
    throw "TaskbarFetch portable build failed with exit code $buildExitCode."
}
if (-not (Test-Path -LiteralPath $builtOutputPath -PathType Leaf) -or
    (Get-Item -LiteralPath $builtOutputPath).Length -le 0) {
    throw "Build completed but the expected executable '$builtOutputPath' was not created."
}

$builtVersionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($builtOutputPath)
$builtProductVersion = ($builtVersionInfo.ProductVersion -split '\+', 2)[0]
if ($builtProductVersion -ne $version -or $builtVersionInfo.FileVersion -ne $versionInfo.FileVersion) {
    throw "The built executable metadata does not match project version '$version' (product '$($builtVersionInfo.ProductVersion)', file '$($builtVersionInfo.FileVersion)')."
}

Copy-Item -LiteralPath $builtOutputPath -Destination $outputPath -Force
if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf) -or
    (Get-Item -LiteralPath $outputPath).Length -le 0) {
    throw "Portable build completed but '$outputPath' was not created."
}

Write-Host ''
Write-Host 'Portable app created successfully.' -ForegroundColor Green
Write-Host "Version: $version"
Write-Host "File: $outputPath"
Write-Host "Size: $([Math]::Round((Get-Item -LiteralPath $outputPath).Length / 1KB, 1)) KB"
