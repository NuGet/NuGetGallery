[CmdletBinding(DefaultParameterSetName = 'RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [string]$GalleryAssemblyVersion = '4.4.5',
    [string]$GalleryPackageVersion = '4.4.5-zlocal',
    [string]$Branch,
    [string]$CommitSHA,
    [string]$BuildBranchCommit = 'caca1e96b175172a623e67a3bd53d2f7a78f6c7e', #DevSkim: ignore DS173237. Not a secret/token. It is a commit hash.
    [string]$VerifyMicrosoftPackageVersion = $null
)

Set-StrictMode -Version 1.0

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

Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/NuGet/ServerCommon/$BuildBranchCommit/build/init.ps1" -OutFile "$PSScriptRoot/build/init.ps1"
. "$PSScriptRoot/build/init.ps1" -BuildBranchCommit $BuildBranchCommit

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()
$GallerySolution = Join-Path $PSScriptRoot "NuGetGallery.sln"
$GalleryProjects = Get-SolutionProjects $GallerySolution

Invoke-BuildStep 'Getting private build tools' { Install-PrivateBuildTools } `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' {
        $SolutionPath = Join-Path $PSScriptRoot "packages.config"
        $PackagesDir = Join-Path $PSScriptRoot "packages"
        Install-SolutionPackages -path $SolutionPath -output $PackagesDir -ExcludeVersion
    } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Set gallery version metadata in AssemblyInfo.cs' {
        $GalleryProjects | Where-Object { !$_.IsTest } | ForEach-Object {
            $Path = Join-Path $_.Directory "Properties\AssemblyInfo.g.cs"
            Set-VersionInfo $Path -AssemblyVersion $GalleryAssemblyVersion -PackageVersion $GalleryPackageVersion -Branch $Branch -Commit $CommitSHA
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Building solution' { 
        $MvcBuildViews = $Configuration -eq "Release"
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $GallerySolution -SkipRestore:$SkipRestore -MSBuildProperties "/p:MvcBuildViews=$MvcBuildViews" `
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the binaries' {
        Sign-Binaries -Configuration $Configuration -BuildNumber $BuildNumber
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Creating artifacts' { `
        $GalleryProjects =
            "src\NuGet.Services.DatabaseMigration\NuGet.Services.DatabaseMigration.csproj",
            "src\NuGet.Services.Entities\NuGet.Services.Entities.csproj",
            "src\NuGetGallery.Core\NuGetGallery.Core.csproj",
            "src\NuGetGallery.Services\NuGetGallery.Services.csproj"
        $GalleryProjects | ForEach-Object {
            New-ProjectPackage (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $GalleryPackageVersion -Branch $Branch -Symbols
        }
        
        $GalleryNuspecProjects =
            "src\DatabaseMigrationTools\DatabaseMigration.Gallery.nuspec",
            "src\DatabaseMigrationTools\DatabaseMigration.SupportRequest.nuspec",
            "src\DatabaseMigrationTools\DatabaseMigration.Validation.nuspec",
            "src\AccountDeleter\Gallery.AccountDeleter.nuspec",
            "src\GitHubVulnerabilities2Db\GitHubVulnerabilities2Db.nuspec",
            "src\GitHubVulnerabilities2v3\GitHubVulnerabilities2v3.nuspec",
            "src\GalleryTools\Gallery.GalleryTools.nuspec",
            "src\VerifyGitHubVulnerabilities\VerifyGitHubVulnerabilities.nuspec"
        $GalleryNuspecProjects | ForEach-Object {
            New-Package (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $GalleryPackageVersion -Branch $Branch
        }

        if (!$VerifyMicrosoftPackageVersion) { $VerifyMicrosoftPackageVersion = $GalleryPackageVersion }
        New-Package (Join-Path $PSScriptRoot "src\VerifyMicrosoftPackage\VerifyMicrosoftPackage.nuspec") -Configuration $Configuration -BuildNumber $BuildNumber -Version $VerifyMicrosoftPackageVersion -Branch $Branch
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the packages' {
        Sign-Packages -Configuration $Configuration -BuildNumber $BuildNumber
    } `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | ForEach-Object { ">>> $($_.Exception.Message)" }
    Error-Log "Builds completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)
