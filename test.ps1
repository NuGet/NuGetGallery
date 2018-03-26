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
    
    $xUnitExe = (Join-Path $PSScriptRoot "packages\xunit.runner.console\tools\xunit.console.exe")
    
    $TestAssemblies = "tests\Tests.Gallery.Maintenance\bin\$Configuration\Tests.Gallery.Maintenance.dll", `
        "tests\Tests.Search.GenerateAuxiliaryData\bin\$Configuration\Tests.Search.GenerateAuxiliaryData.dll", `
        "tests\Tests.Stats.CollectAzureCdnLogs\bin\$Configuration\Tests.Stats.CollectAzureCdnLogs.dll", `
        "tests\Tests.Stats.ImportAzureCdnStatistics\bin\$Configuration\Tests.Stats.ImportAzureCdnStatistics.dll", `
        "tests\Validation.Helper.Tests\bin\$Configuration\Validation.Helper.Tests.dll",`
        "tests\Tests.Stats.CollectAzureChinaCDNLogs\bin\$Configuration\Tests.Stats.CollectAzureChinaCDNLogs.dll", `
        "tests\NuGet.Services.Validation.Orchestrator.Tests\bin\$Configuration\NuGet.Services.Validation.Orchestrator.Tests.dll", `
        "tests\Validation.Common.Tests\bin\$Configuration\Validation.Common.Tests.dll", `
        "tests\Validation.PackageSigning.ProcessSignature.Tests\bin\$Configuration\Validation.PackageSigning.ProcessSignature.Tests.dll", `
        "tests\Validation.PackageSigning.ValidateCertificate.Tests\bin\$Configuration\Validation.PackageSigning.ValidateCertificate.Tests.dll", `
        "tests\Validation.PackageSigning.Core.Tests\bin\$Configuration\Validation.PackageSigning.Core.Tests.dll", `
        "tests\Validation.Common.Job.Tests\bin\$Configuration\Validation.Common.Job.Tests.dll"
    
    $TestCount = 0
    
    foreach ($Test in $TestAssemblies) {
        & $xUnitExe (Join-Path $PSScriptRoot $Test) -xml "Results.$TestCount.xml"
        $TestCount++
    }

    Trace-Log "Running configuration validations";

    $exe = "src\NuGet.Services.Validation.Orchestrator\bin\$Configuration\NuGet.Services.Validation.Orchestrator.exe";
    $cfg = "src\NuGet.Services.Validation.Orchestrator\settings.json";

    & $exe -Configuration $cfg -Validate
    if ( $LASTEXITCODE -gt 0 )
    {
        Write-Error "Validation of the $cfg failed"
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