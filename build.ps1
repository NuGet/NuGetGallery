[CmdletBinding(DefaultParameterSetName = 'RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [string]$JobsAssemblyVersion = '4.3.0',
    [string]$JobsPackageVersion = '4.3.0-zlocal',
    [string]$Branch,
    [string]$CommitSHA,
    [string]$BuildBranchCommit = '8ea7f23faa289682fd02284a14959ab2c67ad546' #DevSkim: ignore DS173237. Not a secret/token. It is a commit hash.
)

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
$JobsSolution = Join-Path $PSScriptRoot "NuGet.Jobs.sln"
$JobsProjects = Get-SolutionProjects $JobsSolution
$JobsFunctionalTestsSolution = Join-Path $PSScriptRoot "NuGet.Jobs.FunctionalTests.sln"

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

Invoke-BuildStep 'Setting job version metadata in AssemblyInfo.cs' {
        $JobsProjects | Where-Object { !$_.IsTest } | ForEach-Object {
            $Path = Join-Path $_.Directory "Properties\AssemblyInfo.g.cs"
            Set-VersionInfo $Path -AssemblyVersion $JobsAssemblyVersion -PackageVersion $JobsPackageVersion -Branch $Branch -Commit $CommitSHA
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Building jobs solution' { 
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $JobsSolution -SkipRestore:$SkipRestore
    } `
    -ev +BuildErrors 

Invoke-BuildStep 'Building jobs functional test solution' { 
        Build-Solution -Configuration $Configuration -BuildNumber $BuildNumber -SolutionPath $JobsFunctionalTestsSolution -SkipRestore:$SkipRestore
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the binaries' {
        Sign-Binaries -Configuration $Configuration -BuildNumber $BuildNumber
    } `
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
