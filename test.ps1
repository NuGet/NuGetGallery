[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber
)

# For TeamCity - If any issue occurs, this script fails the build. - By default, TeamCity returns an exit code of 0 for all powershell scripts, even if they fail
trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\build\common.ps1"

Function Run-Tests {
    [CmdletBinding()]
    param()
    
    Trace-Log 'Running tests'
    
    $xUnitExe = (Join-Path $PSScriptRoot "packages\xunit.runner.console.2.1.0\tools\xunit.console.exe")
    
    $TestAssemblies = `
        "tests\CatalogMetadataTests\bin\$Configuration\CatalogMetadataTests.dll", `
        "tests\CatalogTests\bin\$Configuration\CatalogTests.dll", `
        "tests\Monitoring.PackageLag.Tests\bin\$Configuration\Monitoring.PackageLag.Tests.dll", `
        "tests\NgTests\bin\$Configuration\NgTests.dll", `
        "tests\NuGet.Jobs.Catalog2Registration.Tests\bin\$Configuration\NuGet.Jobs.Catalog2Registration.Tests.dll", `
        "tests\NuGet.Jobs.Common.Tests\bin\$Configuration\NuGet.Jobs.Common.Tests.dll", `
        "tests\NuGet.Jobs.GitHubIndexer.Tests\bin\$Configuration\NuGet.Jobs.GitHubIndexer.Tests.dll", `
        "tests\NuGet.Protocol.Catalog.Tests\bin\$Configuration\NuGet.Protocol.Catalog.Tests.dll", `
        "tests\NuGet.Services.AzureSearch.Tests\bin\$Configuration\NuGet.Services.AzureSearch.Tests.dll", `
        "tests\NuGet.Services.Revalidate.Tests\bin\$Configuration\NuGet.Services.Revalidate.Tests.dll", `
        "tests\NuGet.Services.Validation.Orchestrator.Tests\bin\$Configuration\NuGet.Services.Validation.Orchestrator.Tests.dll", `
        "tests\SplitLargeFiles.Tests\bin\$Configuration\NuGet.Tools.SplitLargeFiles.Tests.dll", `
        "tests\StatusAggregator.Tests\bin\$Configuration\StatusAggregator.Tests.dll", `
        "tests\Tests.CredentialExpiration\bin\$Configuration\Tests.CredentialExpiration.dll", `
        "tests\Tests.Gallery.Maintenance\bin\$Configuration\Tests.Gallery.Maintenance.dll", `
        "tests\Tests.Stats.AggregateCdnDownloadsInGallery\bin\$Configuration\Tests.Stats.AggregateCdnDownloadsInGallery.dll", `
        "tests\Tests.Stats.AzureCdnLogs.Common\bin\$Configuration\Tests.Stats.AzureCdnLogs.Common.dll", `
        "tests\Tests.Stats.CDNLogsSanitizer\bin\$Configuration\Tests.Stats.CDNLogsSanitizer.dll", `
        "tests\Tests.Stats.CollectAzureCdnLogs\bin\$Configuration\Tests.Stats.CollectAzureCdnLogs.dll", `
        "tests\Tests.Stats.CollectAzureChinaCDNLogs\bin\$Configuration\Tests.Stats.CollectAzureChinaCDNLogs.dll", `
        "tests\Tests.Stats.ImportAzureCdnStatistics\bin\$Configuration\Tests.Stats.ImportAzureCdnStatistics.dll", `
        "tests\Validation.Common.Job.Tests\bin\$Configuration\Validation.Common.Job.Tests.dll", `
        "tests\Validation.PackageSigning.Core.Tests\bin\$Configuration\Validation.PackageSigning.Core.Tests.dll", `
        "tests\Validation.PackageSigning.ProcessSignature.Tests\bin\$Configuration\Validation.PackageSigning.ProcessSignature.Tests.dll", `
        "tests\Validation.PackageSigning.RevalidateCertificate.Tests\bin\$Configuration\Validation.PackageSigning.RevalidateCertificate.Tests.dll", `
        "tests\Validation.PackageSigning.ScanAndSign.Tests\bin\$Configuration\Validation.PackageSigning.ScanAndSign.Tests.dll", `
        "tests\Validation.PackageSigning.ValidateCertificate.Tests\bin\$Configuration\Validation.PackageSigning.ValidateCertificate.Tests.dll", `
        "tests\Validation.Symbols.Core.Tests\bin\$Configuration\Validation.Symbols.Core.Tests.dll", `
        "tests\Validation.Symbols.Tests\bin\$Configuration\Validation.Symbols.Tests.dll"

    $DotnetTestProjects = `
        "tests\NuGet.Services.SearchService.Core.Tests\NuGet.Services.SearchService.Core.Tests.csproj"

    $TestCount = 0
    
    foreach ($Test in $TestAssemblies) {
        $TestResultFile = "Results.$TestCount.xml"
        & $xUnitExe (Join-Path $PSScriptRoot $Test) -xml $TestResultFile
        if (-not (Test-Path $TestResultFile))
        {
            Write-Error "The test run failed to produce a result file";
            exit 1;
        }
        $TestCount++
    }
    
    foreach ($Test in $DotnetTestProjects) {
        $TestResultFile = Join-Path $PSScriptRoot "Results.$TestCount.xml"
        dotnet test (Join-Path $PSScriptRoot $Test) --configuration $Configuration "-l:trx;LogFileName=$TestResultFile"
        if (-not (Test-Path $TestResultFile))
        {
            Write-Error "The test run failed to produce a result file";
            exit 1;
        }
        $TestCount++
    }
}

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()

Invoke-BuildStep 'Running tests' { Run-Tests } `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Error-Log "Tests completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)