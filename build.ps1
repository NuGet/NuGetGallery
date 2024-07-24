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
    [string]$BuildBranchCommit = '00a01b766623fb5b714238c4e814e906a242e88e', #DevSkim: ignore DS173237. Not a secret/token. It is a commit hash.
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

Invoke-BuildStep 'Getting private build tools' { Install-PrivateBuildTools } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning test results' { Clear-Tests } `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Clearing package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

Invoke-BuildStep 'Clearing artifacts' { Clear-Artifacts } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { `
    Install-SolutionPackages -path (Join-Path $PSScriptRoot "packages.config") -output (Join-Path $PSScriptRoot "packages") -excludeversion } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Set version metadata in AssemblyInfo.cs' {
        $GalleryAssemblyInfo = 
            "src\AccountDeleter\Properties\AssemblyInfo.g.cs",
            "src\DatabaseMigrationTools\Properties\AssemblyInfo.g.cs",
            "src\GalleryTools\Properties\AssemblyInfo.g.cs",
            "src\GitHubVulnerabilities2Db\Properties\AssemblyInfo.g.cs",
            "src\GitHubVulnerabilities2v3\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.DatabaseMigration\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Entities\Properties\AssemblyInfo.g.cs",
            "src\NuGetGallery.Core\Properties\AssemblyInfo.g.cs",
            "src\NuGetGallery.Services\Properties\AssemblyInfo.g.cs",
            "src\NuGetGallery\Properties\AssemblyInfo.g.cs",
            "src\VerifyMicrosoftPackage\Properties\AssemblyInfo.g.cs"
        $GalleryAssemblyInfo | ForEach-Object {
            Set-VersionInfo (Join-Path $PSScriptRoot $_) -AssemblyVersion $GalleryAssemblyVersion -PackageVersion $GalleryPackageVersion -Branch $Branch -Commit $CommitSHA 
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Removing .editorconfig file in NuGetGallery' { Remove-EditorconfigFile -Directory $PSScriptRoot } `
    -ev +BuildErrors

Invoke-BuildStep 'Building solution' { 
        $SolutionPath = Join-Path $PSScriptRoot "NuGetGallery.sln"
        $MvcBuildViews = $Configuration -eq "Release"
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $SolutionPath -SkipRestore:$SkipRestore -MSBuildProperties "/p:MvcBuildViews=$MvcBuildViews" `
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the binaries' {
        Sign-Binaries -Configuration $Configuration -BuildNumber $BuildNumber `
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
    $ErrorLines = $BuildErrors | ForEach-Object { ">>> $($_.Exception.Message)" }
    Error-Log "Builds completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)
