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
        "tests\NgTests\bin\$Configuration\NgTests.dll", `
        "tests\NuGet.IndexingTests\bin\$Configuration\NuGet.IndexingTests.dll", `
        "tests\NuGet.Services.BasicSearchTests\bin\$Configuration\NuGet.Services.BasicSearchTests.dll", `
        "tests\CatalogTests\bin\$Configuration\CatalogTests.dll", `
        "tests\CatalogMetadataTests\bin\$Configuration\CatalogMetadataTests.dll", `
        "tests\NuGet.Protocol.Catalog.Tests\bin\$Configuration\NuGet.Protocol.Catalog.Tests.dll", `
        "tests\NuGet.Services.AzureSearch.Tests\bin\$Configuration\NuGet.Services.AzureSearch.Tests.dll", `
        "tests\NuGet.Services.SearchService.Tests\bin\$Configuration\NuGet.Services.SearchService.Tests.dll", `
        "tests\NuGet.Jobs.Catalog2Registration.Tests\bin\$Configuration\NuGet.Jobs.Catalog2Registration.Tests.dll", `
        "tests\NuGet.Jobs.RegistrationComparer.Tests\bin\$Configuration\NuGet.Jobs.RegistrationComparer.Tests.dll"
    
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