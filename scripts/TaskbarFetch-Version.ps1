function Get-TaskbarFetchVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$RepositoryRoot
    )

    $propertiesPath = Join-Path $RepositoryRoot 'Directory.Build.props'
    if (-not (Test-Path -LiteralPath $propertiesPath -PathType Leaf)) {
        throw "TaskbarFetch version properties were not found at '$propertiesPath'."
    }

    try {
        [xml]$properties = Get-Content -LiteralPath $propertiesPath -Raw
    }
    catch {
        throw "Could not read TaskbarFetch version properties: $($_.Exception.Message)"
    }

    $propertyGroup = $properties.Project.PropertyGroup
    $version = [string]$propertyGroup.TaskbarFetchVersion
    $assemblyVersion = [string]$propertyGroup.TaskbarFetchAssemblyVersion
    $fileVersion = [string]$propertyGroup.TaskbarFetchFileVersion
    $portableFilePrefix = [string]$propertyGroup.TaskbarFetchPortableFilePrefix
    $setupFilePrefix = [string]$propertyGroup.TaskbarFetchSetupFilePrefix

    if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
        throw "TaskbarFetch version '$version' is not a supported semantic version."
    }
    if ($assemblyVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "TaskbarFetch assembly version '$assemblyVersion' must have four numeric components."
    }
    if ($fileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "TaskbarFetch file version '$fileVersion' must have four numeric components."
    }

    $numericFileVersion = (($version -split '-', 2)[0]) + '.0'
    if ($assemblyVersion -ne $numericFileVersion -or $fileVersion -ne $numericFileVersion) {
        throw "TaskbarFetch assembly and file versions must match the numeric release version '$numericFileVersion'."
    }
    if ([string]::IsNullOrWhiteSpace($portableFilePrefix) -or
        [string]::IsNullOrWhiteSpace($setupFilePrefix) -or
        [IO.Path]::GetFileName($portableFilePrefix + 'x.exe') -ne ($portableFilePrefix + 'x.exe') -or
        [IO.Path]::GetFileName($setupFilePrefix + 'x.exe') -ne ($setupFilePrefix + 'x.exe')) {
        throw 'TaskbarFetch portable and setup filename prefixes must not contain directory paths.'
    }

    $portableFileName = $portableFilePrefix + $version + '.exe'
    $setupFileName = $setupFilePrefix + $version + '.exe'

    [pscustomobject]@{
        InformationalVersion = $version
        AssemblyVersion     = $assemblyVersion
        FileVersion         = $fileVersion
        PortableFileName    = $portableFileName
        SetupFileName       = $setupFileName
        PortableFilePrefix  = $portableFilePrefix
    }
}
