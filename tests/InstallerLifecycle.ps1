$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $root 'scripts\TaskbarFetch-Version.ps1')
$versionInfo = Get-TaskbarFetchVersion -RepositoryRoot $root
$qaRoot = Join-Path ([IO.Path]::GetTempPath()) ('TaskbarFetch-InstallerQA-' + [Guid]::NewGuid().ToString('N'))
$setupSource = Join-Path $root $versionInfo.SetupFileName
$sourceExe = Join-Path $root $versionInfo.PortableFileName
$appData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$startMenuLink = Join-Path $appData 'Microsoft\Windows\Start Menu\Programs\TaskbarFetch\TaskbarFetch.lnk'
$projectLink = Join-Path $root 'TaskbarFetch.lnk'
$runSubKey = 'Software\Microsoft\Windows\CurrentVersion\Run'
$runProviderPath = 'HKCU:\' + $runSubKey.Replace('\', '\')
$uninstallProviderPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
$runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runSubKey, $false)
$originalRunValue = $null
$originalRunKind = $null
if ($runKey) {
    if ($runKey.GetValueNames() -contains 'TaskbarFetch') {
        $originalRunValue = $runKey.GetValue('TaskbarFetch', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $originalRunKind = $runKey.GetValueKind('TaskbarFetch')
    }
    $runKey.Close()
}
$shell = New-Object -ComObject WScript.Shell
$logPath = Join-Path (Join-Path $localAppData 'TaskbarFetch') 'TaskbarFetch.log'
$originalLogHash = if (Test-Path -LiteralPath $logPath -PathType Leaf) { (Get-FileHash -LiteralPath $logPath -Algorithm SHA256).Hash } else { $null }
$settingsKey = 'HKCU:\Software\ThyannSeng\TaskbarFetch'
$originalMovementSetting = (Get-ItemProperty -LiteralPath $settingsKey -Name MoveAlreadyActiveWindowOnCrossMonitorClick -ErrorAction SilentlyContinue).MoveAlreadyActiveWindowOnCrossMonitorClick
$sourceHash = (Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash
$activeInstallDirectories = New-Object System.Collections.ArrayList
$createdProjectLinkTarget = $null
$unrelatedStartupTarget = Join-Path $qaRoot 'Unrelated.exe'
$unrelatedProjectLinkTarget = Join-Path $env:WINDIR 'System32\notepad.exe'

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "ASSERTION FAILED: $Message"
    }
    Write-Host "PASS: $Message" -ForegroundColor Green
}

function Get-StartupValue {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runSubKey, $false)
    if (-not $key) { return $null }
    try {
        if ($key.GetValueNames() -contains 'TaskbarFetch') {
            return [string]$key.GetValue('TaskbarFetch', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        }
        return $null
    }
    finally {
        $key.Close()
    }
}

function Get-LinkTarget([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return [string]$shell.CreateShortcut($Path).TargetPath
}

function Get-TaskbarFetchUninstallEntries {
    return @(Get-ChildItem -LiteralPath $uninstallProviderPath -ErrorAction SilentlyContinue |
        ForEach-Object { Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue } |
        Where-Object { $_.DisplayName -like 'TaskbarFetch*' })
}

function Start-Setup([string]$SetupPath, [string]$InstallDirectory, [AllowNull()][string]$Tasks = $null) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', ('/DIR="' + $InstallDirectory + '"'))
    if (-not [string]::IsNullOrWhiteSpace($Tasks)) {
        $arguments += '/TASKS=' + $Tasks
    }
    $process = Start-Process -FilePath $SetupPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    Assert ($process.ExitCode -eq 0) "Setup completed successfully for '$InstallDirectory'."
}

function Start-Uninstall([string]$InstallDirectory) {
    $uninstaller = Get-ChildItem -LiteralPath $InstallDirectory -Filter 'unins*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    Assert ($null -ne $uninstaller) "Native uninstaller exists in '$InstallDirectory'."
    $process = Start-Process -FilePath $uninstaller.FullName -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru -WindowStyle Hidden
    Assert ($process.ExitCode -eq 0) "Native uninstaller completed successfully for '$InstallDirectory'."
    [void]$activeInstallDirectories.Remove($InstallDirectory)
}

function Verify-InstalledApp([string]$InstallDirectory) {
    $installedExe = Join-Path $InstallDirectory 'TaskbarFetch.exe'
    Assert (Test-Path -LiteralPath $installedExe -PathType Leaf) 'TaskbarFetch.exe exists in the selected install directory.'
    Assert ((Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash -eq $sourceHash) 'Installed executable matches the built TaskbarFetch executable.'
    Assert ((Get-LinkTarget $startMenuLink) -eq $installedExe) 'Start menu shortcut targets the installed executable.'
    $entries = @(Get-TaskbarFetchUninstallEntries)
    Assert ($entries.Count -eq 1) 'A single per-user TaskbarFetch installed-app entry is registered.'
    Assert ($entries[0].Publisher -eq 'Thyann Seng') 'Installed-app publisher is Thyann Seng.'
    Assert ($entries[0].InstallLocation.TrimEnd('\') -eq $InstallDirectory.TrimEnd('\')) 'Installed-app entry points to the selected install directory.'
    Assert ($entries[0].UninstallString -match 'unins\d+\.exe') 'Installed-app entry points to the native Inno Setup uninstaller.'
}

if (-not (Test-Path -LiteralPath $setupSource -PathType Leaf)) { throw 'Compiled TaskbarFetch setup executable is missing.' }
if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) { throw 'Built TaskbarFetch executable is missing.' }
if (Get-Process -Name 'TaskbarFetch' -ErrorAction SilentlyContinue) { throw 'TaskbarFetch is already running; QA will not close it.' }
if ($originalRunValue -ne $null) { throw 'The TaskbarFetch startup registry value already exists; QA will not replace it.' }
if (Get-TaskbarFetchUninstallEntries) { throw 'A TaskbarFetch install is registered; QA will not overwrite it.' }
if (Test-Path -LiteralPath $startMenuLink) { throw 'A TaskbarFetch Start menu shortcut already exists; QA will not replace it.' }
if (Test-Path -LiteralPath $projectLink) { throw 'A project-folder shortcut already exists; QA will not replace it.' }

New-Item -ItemType Directory -Path $qaRoot -Force | Out-Null

try {
    $sourceInstall = Join-Path $qaRoot 'source-install'
    [void]$activeInstallDirectories.Add($sourceInstall)

    Write-Host 'Testing a fresh source-checkout install with startup left off...'
    Start-Setup $setupSource $sourceInstall
    Verify-InstalledApp $sourceInstall
    Assert (-not (Get-StartupValue)) 'Startup remains disabled by default when its checkbox is not selected.'
    Assert ((Get-LinkTarget $projectLink) -eq (Join-Path $sourceInstall 'TaskbarFetch.exe')) 'Source-checkout shortcut targets the installed executable.'
    Assert (-not (Get-Process -Name 'TaskbarFetch' -ErrorAction SilentlyContinue)) 'Silent setup does not launch TaskbarFetch.'

    $marker = Join-Path $sourceInstall 'user-data-to-preserve.txt'
    'Preserve files not owned by the installer.' | Set-Content -LiteralPath $marker -Encoding ascii
    Write-Host 'Testing update behavior after the app startup setting is off...'
    Start-Setup $setupSource $sourceInstall '!startup'
    Verify-InstalledApp $sourceInstall
    Assert (-not (Get-StartupValue)) 'Updating while startup is disabled leaves startup disabled.'

    Write-Host 'Testing explicit startup opt-in and safe removal...'
    Start-Setup $setupSource $sourceInstall 'startup'
    Assert ((Get-StartupValue) -eq ('"' + (Join-Path $sourceInstall 'TaskbarFetch.exe') + '"')) 'Selecting the startup task registers the current install path.'
    Remove-ItemProperty -LiteralPath $runProviderPath -Name TaskbarFetch
    Start-Setup $setupSource $sourceInstall '!startup'
    Assert (-not (Get-StartupValue)) 'Reinstalling after startup was disabled does not turn it back on.'
    Start-Setup $setupSource $sourceInstall 'startup'
    Assert ((Get-StartupValue) -eq ('"' + (Join-Path $sourceInstall 'TaskbarFetch.exe') + '"')) 'Startup can be opted into again on update.'
    Start-Uninstall $sourceInstall
    Assert (-not (Test-Path -LiteralPath (Join-Path $sourceInstall 'TaskbarFetch.exe'))) 'Uninstall removes the installed executable.'
    Assert (-not (Test-Path -LiteralPath $startMenuLink)) 'Uninstall removes the managed Start menu shortcut.'
    Assert (-not (Test-Path -LiteralPath $projectLink)) 'Uninstall removes the managed project-folder shortcut.'
    Assert (-not (Get-StartupValue)) 'Uninstall removes a startup value that still points to the managed install.'
    Assert (-not (Get-TaskbarFetchUninstallEntries)) 'Uninstall removes the Windows installed-app entry.'
    Assert (Test-Path -LiteralPath $marker) 'Uninstall preserves files in the install directory that the installer did not create.'

    Write-Host 'Testing preservation of unrelated project shortcuts and startup values...'
    $link = $shell.CreateShortcut($projectLink)
    $link.TargetPath = $unrelatedProjectLinkTarget
    $link.WorkingDirectory = Split-Path -Parent $unrelatedProjectLinkTarget
    $link.Save()
    $createdProjectLinkTarget = $unrelatedProjectLinkTarget

    $protectedInstall = Join-Path $qaRoot 'protected-install'
    [void]$activeInstallDirectories.Add($protectedInstall)
    Start-Setup $setupSource $protectedInstall 'startup'
    Verify-InstalledApp $protectedInstall
    Assert ((Get-LinkTarget $projectLink) -eq $unrelatedProjectLinkTarget) 'Setup leaves an unrelated project-folder shortcut unchanged.'
    Assert ((Get-StartupValue) -eq ('"' + (Join-Path $protectedInstall 'TaskbarFetch.exe') + '"')) 'Startup opt-in points at the protected install.'

    New-ItemProperty -LiteralPath $runProviderPath -Name TaskbarFetch -Value $unrelatedStartupTarget -PropertyType String -Force | Out-Null
    Start-Uninstall $protectedInstall
    Assert ((Get-StartupValue) -eq $unrelatedStartupTarget) 'Uninstall preserves a startup value changed to point elsewhere.'
    Assert ((Get-LinkTarget $projectLink) -eq $unrelatedProjectLinkTarget) 'Uninstall preserves the unrelated project-folder shortcut.'
    Assert (-not (Test-Path -LiteralPath $startMenuLink)) 'Uninstall removes the managed Start menu shortcut even when other entries are preserved.'
    Assert (-not (Get-TaskbarFetchUninstallEntries)) 'Uninstall removes the per-user installed-app entry for the protected install.'
    Remove-ItemProperty -LiteralPath $runProviderPath -Name TaskbarFetch
    Remove-Item -LiteralPath $projectLink -Force
    $createdProjectLinkTarget = $null

    Write-Host 'Testing setup launched from a release-download folder...'
    $releaseDirectory = Join-Path $qaRoot 'release-download'
    New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
    $releaseSetup = Join-Path $releaseDirectory 'TaskbarFetch-Setup.exe'
    Copy-Item -LiteralPath $setupSource -Destination $releaseSetup
    $releaseInstall = Join-Path $qaRoot 'release-install'
    [void]$activeInstallDirectories.Add($releaseInstall)
    Start-Setup $releaseSetup $releaseInstall '!startup'
    Verify-InstalledApp $releaseInstall
    Assert (-not (Test-Path -LiteralPath $projectLink)) 'A release download does not create a shortcut in the project checkout.'
    Start-Uninstall $releaseInstall
    Assert (-not (Test-Path -LiteralPath $startMenuLink)) 'Release-layout uninstall removes its Start menu shortcut.'

    Assert ((Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash -eq $sourceHash) 'Uninstall leaves the portable executable in the project root unchanged.'
    if ($originalLogHash) {
        Assert ((Get-FileHash -LiteralPath $logPath -Algorithm SHA256).Hash -eq $originalLogHash) 'Install and uninstall preserve the existing diagnostic log.'
    }
    else {
        Assert (-not (Test-Path -LiteralPath $logPath)) 'Install and uninstall do not create or remove diagnostic logs.'
    }
    $movementSetting = (Get-ItemProperty -LiteralPath $settingsKey -Name MoveAlreadyActiveWindowOnCrossMonitorClick -ErrorAction SilentlyContinue).MoveAlreadyActiveWindowOnCrossMonitorClick
    Assert ($movementSetting -eq $originalMovementSetting) 'Install and uninstall preserve the saved movement preference.'
    Assert (-not (Get-Process -Name 'TaskbarFetch' -ErrorAction SilentlyContinue)) 'All silent setup and uninstall runs left the app closed.'
    Write-Host "Installer QA passed. Isolated files: $qaRoot" -ForegroundColor Cyan
}
finally {
    if ($createdProjectLinkTarget -and (Get-LinkTarget $projectLink) -eq $createdProjectLinkTarget) {
        Remove-Item -LiteralPath $projectLink -Force
    }
    foreach ($directory in @($activeInstallDirectories)) {
        if (Test-Path -LiteralPath $directory -PathType Container) {
            $uninstaller = Get-ChildItem -LiteralPath $directory -Filter 'unins*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($uninstaller -and -not (Get-Process -Name 'TaskbarFetch' -ErrorAction SilentlyContinue)) {
                Start-Process -FilePath $uninstaller.FullName -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru -WindowStyle Hidden | Out-Null
            }
        }
    }
    $currentRunValue = Get-StartupValue
    if ($originalRunValue -ne $null) {
        $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($runSubKey)
        try { $key.SetValue('TaskbarFetch', $originalRunValue, $originalRunKind) } finally { $key.Close() }
    }
    elseif ($currentRunValue -eq $unrelatedStartupTarget -or $currentRunValue -like '*installer-qa-*\TaskbarFetch.exe*') {
        Remove-ItemProperty -LiteralPath $runProviderPath -Name TaskbarFetch -ErrorAction SilentlyContinue
    }
    if ((Get-LinkTarget $projectLink) -eq $unrelatedProjectLinkTarget -or (Get-LinkTarget $projectLink) -like '*installer-qa-*\TaskbarFetch.exe') {
        Remove-Item -LiteralPath $projectLink -Force -ErrorAction SilentlyContinue
    }

    $fullQaRoot = [IO.Path]::GetFullPath($qaRoot)
    $fullTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $qaPathPrefix = $fullQaRoot.TrimEnd('\') + '\'
    $isOwnedTempDirectory =
        $fullQaRoot.StartsWith($fullTempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $fullQaRoot) -like 'TaskbarFetch-InstallerQA-*'
    $startMenuTarget = Get-LinkTarget $startMenuLink
    if ($isOwnedTempDirectory -and $startMenuTarget -and
        [IO.Path]::GetFullPath($startMenuTarget).StartsWith($qaPathPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $startMenuLink -Force -ErrorAction SilentlyContinue
        $startMenuDirectory = Split-Path -Parent $startMenuLink
        if ((Test-Path -LiteralPath $startMenuDirectory -PathType Container) -and
            -not (Get-ChildItem -LiteralPath $startMenuDirectory -Force)) {
            Remove-Item -LiteralPath $startMenuDirectory -Force -ErrorAction SilentlyContinue
        }
    }
    if ($isOwnedTempDirectory -and $activeInstallDirectories.Count -eq 0 -and
        -not (Get-Process -Name 'TaskbarFetch' -ErrorAction SilentlyContinue)) {
        Remove-Item -LiteralPath $fullQaRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
