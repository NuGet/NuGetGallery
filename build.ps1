[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [string]$SimpleVersion = '1.0.0',
    [string]$SemanticVersion = '1.0.0-zlocal',
    [string]$PackageSuffix,
    [string]$Branch,
    [string]$CommitSHA,
    [string]$BuildBranchCommit = '65e723253187442f5b8ea537f672bd9328ade5a7',
    [string]$VerifyMicrosoftPackageVersion = $null
)

Set-StrictMode -Version 1.0

# This script should fail the build if any issue occurs.
trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

if (-not (Test-Path "$PSScriptRoot/build")) {
    New-Item -Path "$PSScriptRoot/build" -ItemType "directory"
}

# Enable TLS 1.2 since GitHub requires it.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

wget -UseBasicParsing -Uri "https://raw.githubusercontent.com/NuGet/ServerCommon/$BuildBranchCommit/build/init.ps1" -OutFile "$PSScriptRoot/build/init.ps1"
. "$PSScriptRoot/build/init.ps1" -BuildBranchCommit $BuildBranchCommit

Function Clean-Tests {
    [CmdletBinding()]
    param()
    
    Trace-Log 'Cleaning test results'
    
    Remove-Item (Join-Path $PSScriptRoot "Results.*.xml")
}
    
Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()

Invoke-BuildStep 'Getting private build tools' { Install-PrivateBuildTools } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning test results' { Clean-Tests } `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Clearing package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { `
    Install-SolutionPackages -path (Join-Path $PSScriptRoot ".nuget\packages.config") -output (Join-Path $PSScriptRoot "packages") -excludeversion } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Set version metadata in AssemblyInfo.cs' {
    $Paths = `
        (Join-Path $PSScriptRoot "src\NuGetGallery\Properties\AssemblyInfo.g.cs"), `
        (Join-Path $PSScriptRoot "src\NuGetGallery.Core\Properties\AssemblyInfo.g.cs"), `
        (Join-Path $PSScriptRoot "src\NuGetGallery.Services\Properties\AssemblyInfo.g.cs"), `
        (Join-Path $PSScriptRoot "src\NuGet.Services.Entities\Properties\AssemblyInfo.g.cs"), `
        (Join-Path $PSScriptRoot "src\NuGet.Services.DatabaseMigration\Properties\AssemblyInfo.g.cs"), `
        (Join-Path $PSScriptRoot "src\DatabaseMigrationTools\Properties\AssemblyInfo.g.cs"), `
        (Join-Path $PSScriptRoot "src\AccountDeleter\Properties\AssemblyInfo.g.cs"), `
        (Join-Path $PSScriptRoot "src\GitHubVulnerabilities2Db\Properties\AssemblyInfo.g.cs"), `
        (Join-Path $PSScriptRoot "src\GalleryTools\Properties\AssemblyInfo.g.cs"), `
        (Join-Path $PSScriptRoot "src\VerifyMicrosoftPackage\Properties\AssemblyInfo.g.cs")

    Foreach ($Path in $Paths) {
        # Ensure the directory exists before generating the version info file.
        $directory = Split-Path $Path
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        Set-VersionInfo -Path $Path -Version $SimpleVersion -Branch $Branch -Commit $CommitSHA
    }
} `
-ev +BuildErrors

Invoke-BuildStep 'Removing .editorconfig file in NuGetGallery' { Remove-EditorconfigFile -Directory $PSScriptRoot } `
    -ev +BuildErrors

Invoke-BuildStep 'Building solution' { 
    $SolutionPath = Join-Path $PSScriptRoot "NuGetGallery.sln"
    Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $SolutionPath -SkipRestore:$SkipRestore -MSBuildProperties "/p:MvcBuildViews=true" `
} `
-ev +BuildErrors

Invoke-BuildStep 'Signing the binaries' {
    Sign-Binaries -Configuration $Configuration -BuildNumber $BuildNumber `
} `
-ev +BuildErrors

Invoke-BuildStep 'Creating artifacts' { `
    New-ProjectPackage (Join-Path $PSScriptRoot "src\NuGetGallery.Core\NuGetGallery.Core.csproj") -Configuration $Configuration -Symbols -BuildNumber $BuildNumber -Version $SemanticVersion -PackageId "NuGetGallery.Core$PackageSuffix"
    New-ProjectPackage (Join-Path $PSScriptRoot "src\NuGet.Services.Entities\NuGet.Services.Entities.csproj") -Configuration $Configuration -Symbols -BuildNumber $BuildNumber -Version $SemanticVersion
    New-ProjectPackage (Join-Path $PSScriptRoot "src\NuGet.Services.DatabaseMigration\NuGet.Services.DatabaseMigration.csproj") -Configuration $Configuration -Symbols -BuildNumber $BuildNumber -Version $SemanticVersion
    New-Package (Join-Path $PSScriptRoot "src\DatabaseMigrationTools\DatabaseMigration.Gallery.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch
    New-Package (Join-Path $PSScriptRoot "src\DatabaseMigrationTools\DatabaseMigration.SupportRequest.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch
    New-Package (Join-Path $PSScriptRoot "src\DatabaseMigrationTools\DatabaseMigration.Validation.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch
    New-Package (Join-Path $PSScriptRoot "src\AccountDeleter\Gallery.AccountDeleter.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch
    New-Package (Join-Path $PSScriptRoot "src\GitHubVulnerabilities2Db\GitHubVulnerabilities2Db.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch
    New-Package (Join-Path $PSScriptRoot "src\GalleryTools\Gallery.GalleryTools.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch
    New-Package (Join-Path $PSScriptRoot "src\VerifyGitHubVulnerabilities\VerifyGitHubVulnerabilities.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch

    if (!$VerifyMicrosoftPackageVersion) { $VerifyMicrosoftPackageVersion = $SemanticVersion }
    New-Package (Join-Path $PSScriptRoot "src\VerifyMicrosoftPackage\VerifyMicrosoftPackage.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $VerifyMicrosoftPackageVersion -Branch $Branch
} `
-ev +BuildErrors

Invoke-BuildStep 'Signing the packages' {
    Sign-Packages -Configuration $Configuration -BuildNumber $BuildNumber `
} `
-ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Error-Log "Builds completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)
