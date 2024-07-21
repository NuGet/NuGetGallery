[CmdletBinding(DefaultParameterSetName = 'RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber
)

trap {
    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\build\common.ps1"

Function Invoke-Tests {
    [CmdletBinding()]
    param()
    
    Trace-Log 'Running tests'
    
    $xUnitExe = (Join-Path $PSScriptRoot "packages\xunit.runner.console\tools\net472\xunit.console.exe")
    
    $GalleryTestAssemblies = `
        "tests\AccountDeleter.Facts\bin\$Configuration\net472\AccountDeleter.Facts.dll", `
        "tests\GitHubVulnerabilities2Db.Facts\bin\$Configuration\net472\GitHubVulnerabilities2Db.Facts.dll", `
        "tests\GitHubVulnerabilities2v3.Facts\bin\$Configuration\GitHubVulnerabilities2v3.Facts.dll", `
        "tests\NuGet.Services.DatabaseMigration.Facts\bin\$Configuration\net472\NuGet.Services.DatabaseMigration.Facts.dll", `
        "tests\NuGet.Services.Entities.Tests\bin\$Configuration\net472\NuGet.Services.Entities.Tests.dll", `
        "tests\NuGetGallery.Core.Facts\bin\$Configuration\NuGetGallery.Core.Facts.dll", `
        "tests\NuGetGallery.Facts\bin\$Configuration\NuGetGallery.Facts.dll", `
        "tests\VerifyMicrosoftPackage.Facts\bin\$Configuration\NuGet.VerifyMicrosoftPackage.Facts.dll"
    
    $TestCount = 0

    $GalleryTestAssemblies | ForEach-Object {
        $TestResultFile = Join-Path $PSScriptRoot "Results.$TestCount.xml"
        & $xUnitExe (Join-Path $PSScriptRoot $_) -xml $TestResultFile
        if (-not (Test-Path $TestResultFile)) {
            Write-Error "The test run failed to produce a result file";
            exit 1;
        }
        $TestCount++
    }

    Write-Host "Ensuring the EntityFramework version can be discovered."
    . (Join-Path $PSScriptRoot "tools\Update-Databases.ps1") -MigrationTargets @("FakeMigrationTarget")
}
    
Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()
    
Invoke-BuildStep 'Running tests' { Invoke-Tests } `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | ForEach-Object { ">>> $($_.Exception.Message)" }
    Error-Log "Tests completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)
