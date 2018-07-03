param(
    [parameter(Mandatory=$true)]
    [string[]] $MigrationTargets,
    [string] $NugetGallerySitePath)

function Initialize-MigrateExe() {
    [string] $migrateDirectory = [System.IO.Path]::Combine($PSScriptRoot, '__temp_migrate_directory_' + [guid]::NewGuid().ToString("N") )
    [string] $efDirectory = [System.IO.Path]::Combine($PSScriptRoot, "${env:userprofile}\.nuget\packages\EntityFramework\6.1.3")
    [string] $migrate = ([System.IO.Path]::Combine($migrateDirectory, 'migrate.exe'))

    if (-not (New-Item -ItemType Directory -Path $migrateDirectory -Force).Exists) {
        throw 'migrate directory could not be created.'
    }

    Copy-Item `
        -Path `
            ([System.IO.Path]::Combine($efDirectory, 'tools\migrate.exe')), `
            ([System.IO.Path]::Combine($efDirectory, 'lib\net45\*.dll')) `
        -Destination $migrateDirectory `
        -Force
    
    if (-not (Test-Path -Path $migrate)) {
        throw 'migrate.exe could not be provisioned.'
    }

    return $migrateDirectory
}

function Update-NugetDatabases([string] $MigrateExePath, [string] $NugetGallerySitePath, [string[]] $MigrationTargets) {
    [string] $binariesPath = [System.IO.Path]::Combine($NugetGallerySitePath, 'bin')
    [string] $webConfigPath = [System.IO.Path]::Combine($NugetGallerySitePath, 'web.config')
    if ($MigrationTargets.Contains('NugetGallery')) {
        Write-Host 'Updating Nuget Gallery database...'
        & $MigrateExePath "NuGetGallery.dll" MigrationsConfiguration "NuGetGallery.Core.dll" "/startUpDirectory:$binariesPath" "/startUpConfigurationFile:$webConfigPath"
    }
    
    if ($MigrationTargets.Contains('NugetGallerySupportRequest')) {
        Write-Host 'Updating Nuget Gallery Support request database...'
        & $MigrateExePath "NuGetGallery.dll" SupportRequestMigrationsConfiguration "NuGetGallery.dll" "/startUpDirectory:$binariesPath" "/startUpConfigurationFile:$webConfigPath"
    }

    Write-Host 'Update Complete!'
}

[string] $migrateExeDirectory = $null
try {
    if ([string]::IsNullOrWhiteSpace($NugetGallerySitePath)) {
        $NugetGallerySitePath = [System.IO.Path]::Combine($Script:PSScriptRoot, '..', 'src\NugetGallery')
        Write-Host 'NugetGallerySitePath was not provided.'
        Write-Host "We will attempt to use $NugetGallerySitePath"
    }

    $migrateExeDirectory = Initialize-MigrateExe

    Update-NugetDatabases `
        -MigrateExePath ([System.IO.Path]::Combine($migrateExeDirectory, 'migrate.exe')) `
        -NugetGallerySitePath $NugetGallerySitePath `
        -MigrationTargets $MigrationTargets
}
finally {
    if ($migrateExeDirectory -ne $null -and (Test-Path -Path $migrateExeDirectory -PathType Container)) {
        Remove-Item -Path $migrateExeDirectory -Recurse -Force
    }
}

