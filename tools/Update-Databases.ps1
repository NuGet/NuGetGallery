param(
    [parameter(Mandatory=$true)]
    [string[]] $MigrationTargets,
    [string] $NuGetGallerySitePath)

function Initialize-EF6Exe() {
    [string] $migrateDirectory = [System.IO.Path]::Combine($PSScriptRoot, '__temp_migrate_directory_' + [guid]::NewGuid().ToString("N") )
    [string] $efDirectory = $null
    [string] $ef6 = ([System.IO.Path]::Combine($migrateDirectory, 'ef6.exe'))

    if (-not (New-Item -ItemType Directory -Path $migrateDirectory -Force).Exists) {
        throw 'migrate directory could not be created.'
    }

    if (!$efDirectory) {
        # Read the current version of EntityFramework from NuGetGallery.csproj so that we can find the tools.
        $csprojPath = Join-Path $PSScriptRoot "..\src\NuGetGallery\NuGetGallery.csproj"
        [xml]$csproj = Get-Content $csprojPath
        $efPackageReference = Select-Xml -Xml $csproj -XPath "//*[local-name()='PackageReference']" `
            | Where-Object { $_.Node.Attributes["Include"].Value -eq "EntityFramework" }
        $efVersion = $efPackageReference.Node.Version
        Write-Host "Using EntityFramework version $efVersion."
        $efDirectory = "$env:userprofile\.nuget\packages\EntityFramework\$efVersion"
    }

    Copy-Item `
        -Path `
            ([System.IO.Path]::Combine($efDirectory, 'tools\net45\win-x86\ef6.exe')), `
            ([System.IO.Path]::Combine($efDirectory, 'lib\net45\*.dll')) `
        -Destination $migrateDirectory `
        -Force
    
    if (-not (Test-Path -Path $ef6)) {
        throw 'ef6.exe could not be provisioned.'
    }

    return $migrateDirectory
}

function Update-NuGetDatabases([string] $EF6ExePath, [string] $NuGetGallerySitePath, [string[]] $MigrationTargets) {
    [string] $binariesPath = [System.IO.Path]::Combine($NuGetGallerySitePath, 'bin')
    [string] $webConfigPath = [System.IO.Path]::Combine($NuGetGallerySitePath, 'web.config')
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
        $NuGetGallerySitePath = [System.IO.Path]::Combine($Script:PSScriptRoot, '..', 'src\NuGetGallery')
        Write-Host 'NuGetGallerySitePath was not provided.'
        Write-Host "We will attempt to use $NuGetGallerySitePath"
    }

    $ef6ExeDirectory = Initialize-EF6Exe

    Update-NuGetDatabases `
        -EF6ExePath ([System.IO.Path]::Combine($ef6ExeDirectory, 'ef6.exe')) `
        -NuGetGallerySitePath $NuGetGallerySitePath `
        -MigrationTargets $MigrationTargets
}
finally {
    if ($ef6ExeDirectory -ne $null -and (Test-Path -Path $ef6ExeDirectory -PathType Container)) {
        Remove-Item -Path $ef6ExeDirectory -Recurse -Force
    }
}

