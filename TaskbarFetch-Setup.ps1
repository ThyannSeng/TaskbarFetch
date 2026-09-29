[CmdletBinding()]
param(
    [string]$Version,
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$sourcePath = Join-Path $root 'src\TaskbarFetch.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw
$versionMatch = [regex]::Match($source, '\[assembly:\s*System\.Reflection\.AssemblyInformationalVersion\("([^"]+)"\)\]')
if (-not $versionMatch.Success) {
    throw 'Could not read TaskbarFetch assembly informational version from src\TaskbarFetch.cs.'
}

$sourceVersion = $versionMatch.Groups[1].Value
$fileVersionMatch = [regex]::Match($source, '\[assembly:\s*System\.Reflection\.AssemblyVersion\("([^"]+)"\)\]')
if (-not $fileVersionMatch.Success) {
    throw 'Could not read TaskbarFetch assembly file version from src\TaskbarFetch.cs.'
}
$fileVersion = $fileVersionMatch.Groups[1].Value
if ($fileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Application file version '$fileVersion' is not a four-part Windows version."
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $sourceVersion
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Installer version '$Version' is not a supported semantic version."
}
if ($Version -ne $sourceVersion) {
    throw "Installer version '$Version' does not match the application source version '$sourceVersion'. Update the app version before packaging."
}

$appExe = Join-Path $root 'dist\TaskbarFetch.exe'
if (-not (Test-Path -LiteralPath $appExe -PathType Leaf)) {
    $buildScript = Join-Path $root 'TaskbarFetch-Build.cmd'
    if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
        throw 'dist\TaskbarFetch.exe is missing and TaskbarFetch-Build.cmd was not found.'
    }

    Write-Host 'Building TaskbarFetch before compiling the setup program...'
    Push-Location $root
    try {
        & $buildScript
        $buildExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    if ($buildExitCode -ne 0 -or -not (Test-Path -LiteralPath $appExe -PathType Leaf)) {
        throw "TaskbarFetch build failed with exit code $buildExitCode."
    }
}

if ([string]::IsNullOrWhiteSpace($CompilerPath)) {
    $compilerCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($compilerCommand) {
        $CompilerPath = $compilerCommand.Source
    }
    else {
        $candidates = @()
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
    throw 'Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6.7.3, then run TaskbarFetch-Setup.ps1 again.'
}

$scriptPath = Join-Path $root 'installer\TaskbarFetch.iss'
$setupPath = Join-Path $root "dist\TaskbarFetch-Setup-v$Version.exe"
Write-Host "Compiling the TaskbarFetch $Version setup program..."
& $CompilerPath "/DAppVersion=$Version" "/DAppFileVersion=$fileVersion" $scriptPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf) -or (Get-Item -LiteralPath $setupPath).Length -le 0) {
    throw "Setup compilation completed but '$setupPath' was not created."
}

Write-Host ''
Write-Host 'Setup program created successfully.' -ForegroundColor Green
Write-Host "File: $setupPath"
Write-Host "Size: $([Math]::Round((Get-Item -LiteralPath $setupPath).Length / 1MB, 2)) MB"
