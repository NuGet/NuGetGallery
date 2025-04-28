param(
    [parameter(Mandatory=$true)]
    [string[]] $MigrationTargets,
    [string] $NuGetGallerySitePath)

function Initialize-EF6Exe() {
    [string] $migrateDirectory = Join-Path $PSScriptRoot "__temp_migrate_directory_$(New-Guid)"
    [string] $efDirectory = $null
    [string] $ef6 = Join-Path $migrateDirectory 'ef6.exe'

    if (-not (New-Item -ItemType Directory -Path $migrateDirectory -Force).Exists) {
        throw 'migrate directory could not be created.'
    }

    if (!$efDirectory) {
        # Read the current version of EntityFramework so that we can find the tools.
        $cpmPath = Resolve-Path (Join-Path $PSScriptRoot "..\Directory.Packages.props")
        [xml]$cpm = Get-Content $cpmPath
        $efPackageReference = Select-Xml -Xml $cpm -XPath "//*[local-name()='PackageVersion']" `
            | Where-Object { $_.Node.Attributes["Include"].Value -eq "EntityFramework" }
        $efVersion = $efPackageReference.Node.Version
        if (!$efVersion) {
            throw "EntityFramework version could not be found. Make sure there is an EntityFramework entry in $cpmPath"
        }
        Write-Host "Using EntityFramework version $efVersion."

        if ($env:NUGET_PACKAGES) {
            $efDirectory = Join-Path $env:NUGET_PACKAGES "EntityFramework\$efVersion"
        }
        else {
            $efDirectory = Join-Path $env:USERPROFILE ".nuget\packages\EntityFramework\$efVersion"
        }
    }

    Copy-Item `
        -Path `
            (Join-Path $efDirectory 'tools\net45\win-x86\ef6.exe'), `
            (Join-Path $efDirectory 'lib\net45\*.dll') `
        -Destination $migrateDirectory `
        -Force
    
    if (-not (Test-Path -Path $ef6)) {
        throw 'ef6.exe could not be provisioned.'
    }

    return $migrateDirectory
}

function Update-NuGetDatabases([string] $EF6ExePath, [string] $NuGetGallerySitePath, [string[]] $MigrationTargets) {
    [string] $binariesPath = Join-Path $NuGetGallerySitePath 'bin'
    [string] $webConfigPath = Join-Path $NuGetGallerySitePath 'web.config'
    if ($MigrationTargets.Contains('NuGetGallery')) {
        Write-Host 'Updating NuGet Gallery database...'
        & $EF6ExePath database update --assembly (Join-Path $binariesPath "NuGetGallery.dll") --migrations-config MigrationsConfiguration --config $webConfigPath
    }
    
    if ($MigrationTargets.Contains('NuGetGallerySupportRequest')) {
        Write-Host 'Updating NuGet Gallery Support request database...'
        & $EF6ExePath database update --assembly (Join-Path $binariesPath "NuGetGallery.dll") --migrations-config SupportRequestMigrationsConfiguration --config $webConfigPath
    }

    Write-Host 'Update Complete!'
}

[string] $ef6ExeDirectory = $null
try {
    if ([string]::IsNullOrWhiteSpace($NuGetGallerySitePath)) {
        $NuGetGallerySitePath = Resolve-Path(Join-Path $PSScriptRoot '..\src\NuGetGallery')
        Write-Host 'NuGetGallerySitePath was not provided.'
        Write-Host "We will attempt to use $NuGetGallerySitePath"
    }

    $ef6ExeDirectory = Initialize-EF6Exe

    Update-NuGetDatabases `
        -EF6ExePath (Join-Path $ef6ExeDirectory 'ef6.exe') `
        -NuGetGallerySitePath $NuGetGallerySitePath `
        -MigrationTargets $MigrationTargets
}
finally {
    try {
        if ($ef6ExeDirectory -and (Test-Path -Path $ef6ExeDirectory -PathType Container)) {
            Remove-Item -Path $ef6ExeDirectory -Recurse -Force
        }
    }
    catch {
        Write-Host "Failed to remove temporary directory: $ef6ExeDirectory"
    }
}

