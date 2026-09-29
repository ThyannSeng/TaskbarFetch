[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$versionInfoScript = Join-Path $PSScriptRoot 'TaskbarFetch-Version.ps1'
. $versionInfoScript
$versionInfo = Get-TaskbarFetchVersion -RepositoryRoot $root
$version = $versionInfo.InformationalVersion

$appExe = Join-Path $root $versionInfo.PortableFileName
$buildInputs = @(
    Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -File
    Get-Item -LiteralPath (Join-Path $root 'Directory.Build.props')
    Get-Item -LiteralPath (Join-Path $root 'app.manifest')
    Get-Item -LiteralPath (Join-Path $root 'TaskbarFetch.csproj')
    Get-Item -LiteralPath (Join-Path $root 'assets\TaskbarFetch.ico')
    Get-Item -LiteralPath (Join-Path $PSScriptRoot 'TaskbarFetch-Portable-Build.ps1')
    Get-Item -LiteralPath $versionInfoScript
)
$needsBuild = -not (Test-Path -LiteralPath $appExe -PathType Leaf)
if (-not $needsBuild) {
    $exeTimestamp = (Get-Item -LiteralPath $appExe).LastWriteTimeUtc
    $needsBuild = [bool]($buildInputs | Where-Object { $_.LastWriteTimeUtc -gt $exeTimestamp } | Select-Object -First 1)
}

if ($needsBuild) {
    $buildScript = Join-Path $root 'TaskbarFetch-Portable-Build.cmd'
    if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
        throw "TaskbarFetch-Portable-v$version.exe and TaskbarFetch-Portable-Build.cmd were not found."
    }

    Write-Host "Building the current TaskbarFetch portable v$version..."
    & $buildScript
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $appExe -PathType Leaf)) {
        throw "TaskbarFetch portable build failed with exit code $LASTEXITCODE."
    }
}

Start-Process -FilePath $appExe -WorkingDirectory $root
