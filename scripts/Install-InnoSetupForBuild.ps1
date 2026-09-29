$ErrorActionPreference = 'Stop'

$version = '6.7.3'
$releaseTag = 'is-6_7_3'
$downloadUrl = "https://github.com/jrsoftware/issrc/releases/download/$releaseTag/innosetup-$version.exe"
$installerPath = Join-Path $env:RUNNER_TEMP "innosetup-$version.exe"
$installDirectory = Join-Path $env:RUNNER_TEMP 'Inno Setup 6'

New-Item -ItemType Directory -Path $env:RUNNER_TEMP -Force | Out-Null
Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath

$signature = Get-AuthenticodeSignature -FilePath $installerPath
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Pyrsys B.V.*') {
    throw "The Inno Setup $version installer signature was invalid or unexpected."
}

$install = Start-Process -FilePath $installerPath -ArgumentList @(
    '/VERYSILENT',
    '/SUPPRESSMSGBOXES',
    '/NORESTART',
    '/CURRENTUSER',
    ('/DIR="' + $installDirectory + '"')
) -Wait -PassThru -WindowStyle Hidden
if ($install.ExitCode -ne 0) {
    throw "Inno Setup installation failed with exit code $($install.ExitCode)."
}

$compilerPath = Join-Path $installDirectory 'ISCC.exe'
if (-not (Test-Path -LiteralPath $compilerPath -PathType Leaf)) {
    throw 'Inno Setup installed, but ISCC.exe was not found at the expected location.'
}

if ($env:GITHUB_ENV) {
    "TASKBARFETCH_ISCC=$compilerPath" | Add-Content -LiteralPath $env:GITHUB_ENV -Encoding utf8
}
