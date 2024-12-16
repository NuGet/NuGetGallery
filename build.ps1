[CmdletBinding(DefaultParameterSetName = 'RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$SkipArtifacts,
    [switch]$SkipCommon,
    [string]$CommonAssemblyVersion = '5.0.0',
    [string]$CommonPackageVersion = '5.0.0-zlocal',
    [switch]$SkipGallery,
    [string]$GalleryAssemblyVersion = '5.0.0',
    [string]$GalleryPackageVersion = '5.0.0-zlocal',
    [switch]$SkipJobs,
    [string]$JobsAssemblyVersion = '5.0.0',
    [string]$JobsPackageVersion = '5.0.0-zlocal',
    [string]$Branch,
    [string]$CommitSHA,
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

. "$PSScriptRoot\build\common.ps1"

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()

. (Join-Path $PSScriptRoot "build.shared.ps1") `
    -SkipCommon:$SkipCommon `
    -CommonAssemblyVersion $CommonAssemblyVersion `
    -CommonPackageVersion $CommonPackageVersion `
    -SkipGallery:$SkipGallery `
    -GalleryAssemblyVersion $GalleryAssemblyVersion `
    -GalleryPackageVersion $GalleryPackageVersion `
    -SkipJobs:$SkipJobs `
    -JobsAssemblyVersion $JobsAssemblyVersion `
    -JobsPackageVersion $JobsPackageVersion

Invoke-BuildStep 'Restoring solution packages' {
        $SolutionPath = Join-Path $PSScriptRoot "packages.config"
        $PackagesDir = Join-Path $PSScriptRoot "packages"
        Install-SolutionPackages -path $SolutionPath -output $PackagesDir -ExcludeVersion
    } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Building common solution' {
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $CommonSolution -SkipRestore:$SkipRestore
    } `
    -skip:$SkipCommon `
    -ev +BuildErrors

Invoke-BuildStep 'Building gallery solution' { 
        $MvcBuildViews = $Configuration -eq "Release"
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $GallerySolution -SkipRestore:$SkipRestore -MSBuildProperties "/p:MvcBuildViews=$MvcBuildViews" `
    } `
    -skip:$SkipGallery `
    -ev +BuildErrors

Invoke-BuildStep 'Building jobs solution' { 
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $JobsSolution -SkipRestore:$SkipRestore
    } `
    -skip:$SkipJobs `
    -ev +BuildErrors 

Invoke-BuildStep 'Building jobs functional test solution' { 
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $JobsFunctionalTestsSolution -SkipRestore:$SkipRestore
    } `
    -skip:$SkipJobs `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the binaries' {
        Sign-Binaries -Configuration $Configuration -BuildNumber $BuildNumber
    } `
    -skip:$SkipArtifacts `
    -ev +BuildErrors

Invoke-BuildStep 'Creating dependency packages from all solutions' {
        $packageVersions = "/p:CommonPackageVersion=$CommonPackageVersion;GalleryPackageVersion=$GalleryPackageVersion;JobsPackageVersion=$JobsPackageVersion"
    
        $CommonPackages = $CommonProjects | Where-Object { $_.IsSrc } | ForEach-Object { $_.RelativePath }
        $CommonPackages | ForEach-Object {
            New-ProjectPackage (Join-Path $PSScriptRoot $_) -Configuration $Configuration -Symbols -Options $packageVersions
        }
    } `
    -skip:($SkipCommon -or $SkipArtifacts) `
    -ev +BuildErrors

Invoke-BuildStep 'Creating job packages from gallery solution' { `
        $GalleryNuspecProjects =
            "src\DatabaseMigrationTools\DatabaseMigration.Gallery.nuspec",
            "src\DatabaseMigrationTools\DatabaseMigration.SupportRequest.nuspec",
            "src\DatabaseMigrationTools\DatabaseMigration.Validation.nuspec",
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
    -skip:($SkipGallery -or $SkipArtifacts) `
    -ev +BuildErrors

Invoke-BuildStep 'Creating job packages from jobs solution' {
        $JobsNuspecProjects =
            "src\ArchivePackages\ArchivePackages.nuspec",
            "src\CopyAzureContainer\CopyAzureContainer.nuspec",
            "src\Gallery.CredentialExpiration\Gallery.CredentialExpiration.nuspec",
            "src\Gallery.Maintenance\Gallery.Maintenance.nuspec",
            "src\Ng\Catalog2Dnx.nuspec",
            "src\Ng\Catalog2icon.nuspec",
            "src\Ng\Catalog2Monitoring.nuspec",
            "src\Ng\Db2Catalog.nuspec",
            "src\Ng\Db2Monitoring.nuspec",
            "src\Ng\Monitoring2Monitoring.nuspec",
            "src\Ng\MonitoringProcessor.nuspec",
            "src\Ng\Ng.Operations.nuspec",
            "src\NuGet.Jobs.Auxiliary2AzureSearch\NuGet.Jobs.Auxiliary2AzureSearch.nuspec",
            "src\NuGet.Jobs.Catalog2AzureSearch\NuGet.Jobs.Catalog2AzureSearch.nuspec",
            "src\NuGet.Jobs.Catalog2Registration\NuGet.Jobs.Catalog2Registration.nuspec",
            "src\NuGet.Jobs.Db2AzureSearch\NuGet.Jobs.Db2AzureSearch.nuspec",
            "src\NuGet.Jobs.GitHubIndexer\NuGet.Jobs.GitHubIndexer.nuspec",
            "src\NuGet.Services.Revalidate\NuGet.Services.Revalidate.nuspec",
            "src\NuGet.Services.Validation.Orchestrator\Validation.Orchestrator.nuspec",
            "src\NuGet.Services.Validation.Orchestrator\Validation.SymbolsOrchestrator.nuspec",
            "src\NuGet.SupportRequests.Notifications\NuGet.SupportRequests.Notifications.nuspec",
            "src\PackageLagMonitor\Monitoring.PackageLag.nuspec",
            "src\SplitLargeFiles\SplitLargeFiles.nuspec",
            "src\Stats.AggregateCdnDownloadsInGallery\Stats.AggregateCdnDownloadsInGallery.nuspec",
            "src\Stats.CDNLogsSanitizer\Stats.CDNLogsSanitizer.nuspec",
            "src\Stats.CollectAzureChinaCDNLogs\Stats.CollectAzureChinaCDNLogs.nuspec",
            "src\Stats.PostProcessReports\Stats.PostProcessReports.nuspec",
            "src\StatusAggregator\StatusAggregator.nuspec",
            "src\Validation.PackageSigning.ProcessSignature\Validation.PackageSigning.ProcessSignature.nuspec",
            "src\Validation.PackageSigning.RevalidateCertificate\Validation.PackageSigning.RevalidateCertificate.nuspec",
            "src\Validation.PackageSigning.ValidateCertificate\Validation.PackageSigning.ValidateCertificate.nuspec",
            "src\Validation.Symbols\Validation.Symbols.Job.nuspec"
        $JobsNuspecProjects | ForEach-Object {
            New-Package (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $JobsPackageVersion -Branch $Branch
        }
    } `
    -skip:($SkipJobs -or $SkipArtifacts) `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the packages' {
        Sign-Packages -Configuration $Configuration -BuildNumber $BuildNumber
    } `
    -skip:$SkipArtifacts `
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
