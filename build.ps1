[CmdletBinding(DefaultParameterSetName = 'RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$SkipArtifacts,
    [switch]$SkipCommon,
    [string]$CommonAssemblyVersion = '3.0.0',
    [string]$CommonPackageVersion = '3.0.0-zlocal',
    [switch]$SkipGallery,
    [string]$GalleryAssemblyVersion = '4.4.5',
    [string]$GalleryPackageVersion = '4.4.5-zlocal',
    [switch]$SkipJobs,
    [string]$JobsAssemblyVersion = '4.3.0',
    [string]$JobsPackageVersion = '4.3.0-zlocal',
    [string]$Branch,
    [string]$CommitSHA,
    [string]$BuildBranchCommit = '8ea7f23faa289682fd02284a14959ab2c67ad546', #DevSkim: ignore DS173237. Not a secret/token. It is a commit hash.
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
$CommonSolution = Join-Path $PSScriptRoot "NuGet.Server.Common.sln"
$CommonProjects = Get-SolutionProjects $CommonSolution
$SharedCommonProjects = $CommonProjects | Where-Object { $_.IsSrc } | ForEach-Object { $_.RelativePath }
$GallerySolution = Join-Path $PSScriptRoot "NuGetGallery.sln"
$GalleryProjects = Get-SolutionProjects $GallerySolution
$SharedGalleryProjects =
    "src\NuGet.Services.Entities\NuGet.Services.Entities.csproj",
    "src\NuGetGallery.Core\NuGetGallery.Core.csproj"
$JobsSolution = Join-Path $PSScriptRoot "NuGet.Jobs.sln"
$JobsProjects = Get-SolutionProjects $JobsSolution
$JobsFunctionalTestsSolution = Join-Path $PSScriptRoot "NuGet.Jobs.FunctionalTests.sln"
$SharedJobsProjects =
    "src\NuGet.Jobs.Common\NuGet.Jobs.Common.csproj",
    "src\Validation.Common.Job\Validation.Common.Job.csproj"

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

Invoke-BuildStep 'Setting common version metadata in AssemblyInfo.cs' {
        $CommonAssemblyInfo = $CommonProjects `
            | Where-Object { !$_.IsTest } `
            | Where-Object { !$SkipCommon -or $SharedCommonProjects -contains $_.RelativePath } `
            | Where-Object { $SharedGalleryProjects -notcontains $_.RelativePath } `
            | Where-Object { $SharedJobsProjects -notcontains $_.RelativePath };
        $CommonAssemblyInfo | ForEach-Object {
            $Path = Join-Path $_.Directory "Properties\AssemblyInfo.g.cs"
            Set-VersionInfo $Path -AssemblyVersion $CommonAssemblyVersion -PackageVersion $CommonPackageVersion -Branch $Branch -Commit $CommitSHA
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Setting gallery version metadata in AssemblyInfo.cs' {
        $GalleryAssemblyInfo = $GalleryProjects `
            | Where-Object { !$_.IsTest } `
            | Where-Object { $SharedCommonProjects -notcontains $_.RelativePath } `
            | Where-Object { !$SkipGallery -or $SharedGalleryProjects -contains $_.RelativePath } `
            | Where-Object { $SharedJobsProjects -notcontains $_.RelativePath };
        $GalleryAssemblyInfo | ForEach-Object {
            $Path = Join-Path $_.Directory "Properties\AssemblyInfo.g.cs"
            Set-VersionInfo $Path -AssemblyVersion $GalleryAssemblyVersion -PackageVersion $GalleryPackageVersion -Branch $Branch -Commit $CommitSHA
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Setting job version metadata in AssemblyInfo.cs' {
        $JobsAssemblyInfo = $JobsProjects `
            | Where-Object { !$_.IsTest } `
            | Where-Object { $SharedCommonProjects -notcontains $_.RelativePath } `
            | Where-Object { $SharedGalleryProjects -notcontains $_.RelativePath } `
            | Where-Object { !$SkipJobs -or $SharedJobsProjects -contains $_.RelativePath };
        $JobsAssemblyInfo | ForEach-Object {
            $Path = Join-Path $_.Directory "Properties\AssemblyInfo.g.cs"
            Set-VersionInfo $Path -AssemblyVersion $JobsAssemblyVersion -PackageVersion $JobsPackageVersion -Branch $Branch -Commit $CommitSHA
        }
    } `
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

Invoke-BuildStep 'Creating common artifacts' {
        $CommonPackages = $CommonProjects | Where-Object { $_.IsSrc }
        $CommonPackages | ForEach-Object {
            New-ProjectPackage $_.Path -Configuration $Configuration -BuildNumber $BuildNumber -Version $CommonPackageVersion
        }
    } `
    -skip:($SkipCommon -or $SkipArtifacts) `
    -ev +BuildErrors

Invoke-BuildStep 'Creating gallery artifacts' { `
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
    -skip:($SkipGallery -or $SkipArtifacts) `
    -ev +BuildErrors

Invoke-BuildStep 'Creating jobs artifacts' {
        $JobsProjects =
            "src\Catalog\NuGet.Services.Metadata.Catalog.csproj",
            "src\Microsoft.PackageManagement.Search.Web\Microsoft.PackageManagement.Search.Web.csproj",
            "src\NuGet.Jobs.Common\NuGet.Jobs.Common.csproj",
            "src\NuGet.Protocol.Catalog\NuGet.Protocol.Catalog.csproj",
            "src\NuGet.Services.AzureSearch\NuGet.Services.AzureSearch.csproj",
            "src\NuGet.Services.Metadata.Catalog.Monitoring\NuGet.Services.Metadata.Catalog.Monitoring.csproj",
            "src\NuGet.Services.V3\NuGet.Services.V3.csproj",
            "src\Stats.LogInterpretation\Stats.LogInterpretation.csproj",
            "src\Validation.Common.Job\Validation.Common.Job.csproj",
            "src\Validation.ContentScan.Core\Validation.ContentScan.Core.csproj",
            "src\Validation.ScanAndSign.Core\Validation.ScanAndSign.Core.csproj",
            "src\Validation.Symbols.Core\Validation.Symbols.Core.csproj"
        $JobsProjects | ForEach-Object {
            New-ProjectPackage (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $JobsPackageVersion -Branch $Branch -Symbols
        }

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
            "src\Stats.CollectAzureCdnLogs\Stats.CollectAzureCdnLogs.nuspec",
            "src\Stats.CollectAzureChinaCDNLogs\Stats.CollectAzureChinaCDNLogs.nuspec",
            "src\Stats.CreateAzureCdnWarehouseReports\Stats.CreateAzureCdnWarehouseReports.nuspec",
            "src\Stats.ImportAzureCdnStatistics\Stats.ImportAzureCdnStatistics.nuspec",
            "src\Stats.PostProcessReports\Stats.PostProcessReports.nuspec",
            "src\Stats.RollUpDownloadFacts\Stats.RollUpDownloadFacts.nuspec",
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
