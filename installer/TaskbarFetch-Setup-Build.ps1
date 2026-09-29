[CmdletBinding()]
param(
    [string]$Version,
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts\TaskbarFetch-Version.ps1')
$versionInfo = Get-TaskbarFetchVersion -RepositoryRoot $root
$sourceVersion = $versionInfo.InformationalVersion
$fileVersion = $versionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $sourceVersion
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Installer version '$Version' is not a supported semantic version."
}
if ($Version -ne $sourceVersion) {
    throw "Installer version '$Version' does not match the application source version '$sourceVersion'. Update the app version before packaging."
}

$appExe = Join-Path $root $versionInfo.PortableFileName
$buildScript = Join-Path $root 'TaskbarFetch-Portable-Build.cmd'
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
    throw 'TaskbarFetch-Portable-Build.cmd was not found.'
}

Write-Host 'Building the current TaskbarFetch portable executable before compiling setup...'
Push-Location $root
try {
    & $buildScript
    $buildExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
if ($buildExitCode -ne 0 -or -not (Test-Path -LiteralPath $appExe -PathType Leaf)) {
    throw "TaskbarFetch portable build failed with exit code $buildExitCode."
}

if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
    $compilerCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($compilerCommand) {
        $CompilerPath = $compilerCommand.Source
    }
    else {
        $candidates = @()
        $candidates += Join-Path $root 'bin\installer-tool\InnoSetup6\ISCC.exe'
        if (${env:ProgramFiles(x86)}) {
            $candidates += Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
        }
        if ($env:ProgramFiles) {
            $candidates += Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'
        }
        if ($env:LOCALAPPDATA) {
            $candidates += Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
        }
        $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}

if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw 'Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6.7.3, then run TaskbarFetch-Setup-Build.cmd again.'
}

$scriptPath = Join-Path $PSScriptRoot 'TaskbarFetch.iss'
$setupPath = Join-Path $root $versionInfo.SetupFileName
Write-Host "Compiling the TaskbarFetch $Version setup program..."
& $CompilerPath "/DAppVersion=$Version" "/DAppFileVersion=$fileVersion" "/DAppPortableFileName=$($versionInfo.PortableFileName)" $scriptPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf) -or (Get-Item -LiteralPath $setupPath).Length -le 0) {
    throw "Setup compilation completed but '$setupPath' was not created."
}

$setupVersionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($setupPath)
$setupProductVersion = $setupVersionInfo.ProductVersion.Trim()
$setupNumericFileVersion = '{0}.{1}.{2}.{3}' -f
    $setupVersionInfo.FileMajorPart,
    $setupVersionInfo.FileMinorPart,
    $setupVersionInfo.FileBuildPart,
    $setupVersionInfo.FilePrivatePart
if ($setupProductVersion -ne $sourceVersion -or $setupNumericFileVersion -ne $fileVersion) {
    throw "The setup executable metadata does not match project version '$sourceVersion' (product '$setupProductVersion', file '$setupNumericFileVersion')."
}

Write-Host ''
Write-Host 'Setup program created successfully.' -ForegroundColor Green
Write-Host "Version: $setupProductVersion"
Write-Host "File: $setupPath"
Write-Host "Size: $([Math]::Round((Get-Item -LiteralPath $setupPath).Length / 1MB, 2)) MB"
