[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [string]$SimpleVersion = '1.0.0',
    [string]$SemanticVersion = '1.0.0-zlocal',
    [string]$Branch = 'zlocal',
    [string]$CommitSHA,
    [string]$BuildBranch = '6d1fcf147a7af8b6b4db842494bc7beed3b1d0e9'
)

$msBuildVersion = 15;

# For TeamCity - If any issue occurs, this script fails the build. - By default, TeamCity returns an exit code of 0 for all powershell scripts, even if they fail
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

wget -UseBasicParsing -Uri "https://raw.githubusercontent.com/NuGet/ServerCommon/$BuildBranch/build/init.ps1" -OutFile "$PSScriptRoot/build/init.ps1"
. "$PSScriptRoot/build/init.ps1" -BuildBranch "$BuildBranch"

Function Clean-Tests {
    [CmdletBinding()]
    param()
    
    Trace-Log 'Cleaning test results'
    
    Remove-Item (Join-Path $PSScriptRoot "Results.*.xml")
}

Function Prepare-NuGetCDNRedirect {
    [CmdletBinding()]
    param()
    
    Trace-Log 'Preparing NuGetCDNRedirect Package'
    
    $ZipPackagePath = "src\NuGetCDNRedirect\obj\NuGetCDNRedirect.zip"
    
    if (Test-Path $ZipPackagePath) {
        Remove-Item $ZipPackagePath
    }
    
    Build-Solution $Configuration $BuildNumber -MSBuildVersion "$msBuildVersion" "src\NuGetCDNRedirect\NuGetCDNRedirect.csproj" -Target "Package" -MSBuildProperties "/P:PackageLocation=obj\NuGetCDNRedirect.zip" -SkipRestore
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
    
Invoke-BuildStep 'Set version metadata in AssemblyInfo.cs' { `
        $versionMetadata =
            "src\CopyAzureContainer\Properties\AssemblyInfo.g.cs",
            "src\NuGetCDNRedirect\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Validation.Orchestrator\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Services.Revalidate\Properties\AssemblyInfo.g.cs",
            "src\Stats.CollectAzureChinaCDNLogs\Properties\AssemblyInfo.g.cs",
            "src\Validation.PackageSigning.ProcessSignature\Properties\AssemblyInfo.g.cs",
            "src\Validation.PackageSigning.ValidateCertificate\Properties\AssemblyInfo.g.cs",
            "src\Validation.PackageSigning.RevalidateCertificate\Properties\AssemblyInfo.g.cs",
            "src\Validation.Common.Job\Properties\AssemblyInfo.g.cs",
            "src\Validation.ScanAndSign.Core\Properties\AssemblyInfo.g.cs",
            "src\PackageLagMonitor\Properties\AssemblyInfo.g.cs",
            "src\StatusAggregator\Properties\AssemblyInfo.g.cs",
            "src\Validation.Symbols.Core\Properties\AssemblyInfo.g.cs",
            "src\Stats.CDNLogsSanitizer\Properties\AssemblyInfo.g.cs",
            "src\NuGet.Jobs.GitHubIndexer\Properties\AssemblyInfo.g.cs",
            "src\SplitLargeFiles\Properties\AssemblyInfo.g.cs",
            "src\Catalog\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.ApplicationInsights.Owin\Properties\AssemblyInfo.g.cs", `
            "src\Ng\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Services.Metadata.Catalog.Monitoring\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Protocol.Catalog\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Services.AzureSearch\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Jobs.Db2AzureSearch\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Jobs.Catalog2AzureSearch\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Services.SearchService\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Jobs.Auxiliary2AzureSearch\Properties\AssemblyInfo.g.cs", `
            "src\NuGet.Jobs.Catalog2Registration\Properties\AssemblyInfo.g.cs"
            
        $versionMetadata | ForEach-Object {
            Set-VersionInfo -Path (Join-Path $PSScriptRoot $_) -Version $SimpleVersion -Branch $Branch -Commit $CommitSHA
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Restoring solution packages' { `
    Install-SolutionPackages -path (Join-Path $PSScriptRoot ".nuget\packages.config") -output (Join-Path $PSScriptRoot "packages") -ExcludeVersion } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Building solution' { 
    param($Configuration, $BuildNumber, $SolutionPath, $SkipRestore)
    Build-Solution $Configuration $BuildNumber -MSBuildVersion "$msBuildVersion" $SolutionPath -SkipRestore:$SkipRestore `
    } `
    -args $Configuration, $BuildNumber, (Join-Path $PSScriptRoot "NuGet.Jobs.sln"), $SkipRestore `
    -ev +BuildErrors 

Invoke-BuildStep 'Building functional test solution' { 
        $SolutionPath = Join-Path $PSScriptRoot "tests\NuGetServicesMetadata.FunctionalTests.sln"
        Build-Solution $Configuration $BuildNumber -MSBuildVersion "$msBuildVersion" $SolutionPath -SkipRestore:$SkipRestore `
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the binaries' {
        Sign-Binaries -Configuration $Configuration -BuildNumber $BuildNumber -MSBuildVersion "15" `
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Prepare NuGetCDNRedirect Package' { Prepare-NuGetCDNRedirect } `
    -ev +BuildErrors

Invoke-BuildStep 'Creating artifacts' {
        # We need a few projects to be published for sharing the common bits with other repos.
        # We need symbols published for those, too. All other packages are deployment ones and
        # don't need to be shared, hence no need for symbols for them
        $CsprojProjects =
            "src\NuGet.Jobs.Common\NuGet.Jobs.Common.csproj",
            "src\Validation.Common.Job\Validation.Common.Job.csproj",
            "src\Validation.ScanAndSign.Core\Validation.ScanAndSign.Core.csproj",
            "src\Validation.Symbols.Core\Validation.Symbols.Core.csproj"
            "src\Catalog\NuGet.Services.Metadata.Catalog.csproj",
            "src\NuGet.ApplicationInsights.Owin\NuGet.ApplicationInsights.Owin.csproj",
            "src\NuGet.Services.Metadata.Catalog.Monitoring\NuGet.Services.Metadata.Catalog.Monitoring.csproj",
            "src\NuGet.Protocol.Catalog\NuGet.Protocol.Catalog.csproj",
            "src\NuGet.Services.AzureSearch\NuGet.Services.AzureSearch.csproj"

        $CsprojProjects | ForEach-Object {
            New-ProjectPackage (Join-Path $PSScriptRoot $_) -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch -Symbols
        }

        $NuspecProjects = `
            "src\Stats.CollectAzureCdnLogs\Stats.CollectAzureCdnLogs.csproj", `
            "src\Stats.AggregateCdnDownloadsInGallery\Stats.AggregateCdnDownloadsInGallery.csproj", `
            "src\Stats.ImportAzureCdnStatistics\Stats.ImportAzureCdnStatistics.csproj", `
            "src\Stats.CreateAzureCdnWarehouseReports\Stats.CreateAzureCdnWarehouseReports.csproj", `
            "src\Gallery.CredentialExpiration\Gallery.CredentialExpiration.csproj", `
            "src\Gallery.Maintenance\Gallery.Maintenance.nuspec", `
            "src\ArchivePackages\ArchivePackages.csproj", `
            "src\Stats.RollUpDownloadFacts\Stats.RollUpDownloadFacts.csproj", `
            "src\NuGet.SupportRequests.Notifications\NuGet.SupportRequests.Notifications.csproj", `
            "src\CopyAzureContainer\CopyAzureContainer.csproj", `
            "src\NuGet.Services.Validation.Orchestrator\Validation.Orchestrator.nuspec", `
            "src\NuGet.Services.Validation.Orchestrator\Validation.SymbolsOrchestrator.nuspec", `
            "src\NuGet.Services.Revalidate\NuGet.Services.Revalidate.csproj", `
            "src\Stats.CollectAzureChinaCDNLogs\Stats.CollectAzureChinaCDNLogs.csproj", `
            "src\Validation.PackageSigning.ProcessSignature\Validation.PackageSigning.ProcessSignature.csproj", `
            "src\Validation.PackageSigning.ValidateCertificate\Validation.PackageSigning.ValidateCertificate.csproj", `
            "src\Validation.PackageSigning.RevalidateCertificate\Validation.PackageSigning.RevalidateCertificate.csproj", `
            "src\PackageLagMonitor\Monitoring.PackageLag.csproj", `
            "src\StatusAggregator\StatusAggregator.csproj", `
            "src\Validation.Symbols.Core\Validation.Symbols.Core.csproj", `
            "src\Validation.Symbols\Validation.Symbols.Job.csproj", `
            "src\Stats.CDNLogsSanitizer\Stats.CDNLogsSanitizer.csproj", `
            "src\NuGet.Jobs.GitHubIndexer\NuGet.Jobs.GitHubIndexer.nuspec", `
            "src\SplitLargeFiles\SplitLargeFiles.nuspec", `
            "src\Ng\Catalog2Dnx.nuspec", `
            "src\Ng\Catalog2icon.nuspec", `
            "src\Ng\Catalog2Monitoring.nuspec", `
            "src\Ng\Db2Catalog.nuspec", `
            "src\Ng\Db2Monitoring.nuspec", `
            "src\Ng\Monitoring2Monitoring.nuspec", `
            "src\Ng\MonitoringProcessor.nuspec", `
            "src\Ng\Ng.Operations.nuspec", `
            "src\NuGet.Jobs.Db2AzureSearch\NuGet.Jobs.Db2AzureSearch.nuspec", `
            "src\NuGet.Jobs.Catalog2AzureSearch\NuGet.Jobs.Catalog2AzureSearch.nuspec", `
            "src\NuGet.Jobs.Auxiliary2AzureSearch\NuGet.Jobs.Auxiliary2AzureSearch.nuspec", `
            "src\NuGet.Jobs.Catalog2Registration\NuGet.Jobs.Catalog2Registration.nuspec"

        Foreach ($Project in $NuspecProjects) {
            New-Package (Join-Path $PSScriptRoot "$Project") -Configuration $Configuration -BuildNumber $BuildNumber -Version $SemanticVersion -Branch $Branch -MSBuildVersion "$msBuildVersion"
        }
    } `
    -ev +BuildErrors

Invoke-BuildStep 'Signing the packages' {
        Sign-Packages -Configuration $Configuration -BuildNumber $BuildNumber -MSBuildVersion $msBuildVersion `
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
