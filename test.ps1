[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [int]$BuildNumber
)

# For TeamCity - If any issue occurs, this script fail the build. - By default, TeamCity returns an exit code of 0 for all powershell scripts, even if they fail
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
    
    $TestAssemblies = `
        "tests\AccountDeleter.Facts\bin\$Configuration\AccountDeleter.Facts.dll", `
        "tests\GitHubVulnerabilities2Db.Facts\bin\$Configuration\GitHubVulnerabilities2Db.Facts.dll", `
        "tests\NuGet.Services.DatabaseMigration.Facts\bin\$Configuration\NuGet.Services.DatabaseMigration.Facts.dll", `
        "tests\NuGet.Services.Entities.Tests\bin\$Configuration\NuGet.Services.Entities.Tests.dll", `
        "tests\NuGetGallery.Core.Facts\bin\$Configuration\NuGetGallery.Core.Facts.dll", `
        "tests\NuGetGallery.Facts\bin\$Configuration\NuGetGallery.Facts.dll", `
        "tests\VerifyMicrosoftPackage.Facts\bin\$Configuration\NuGet.VerifyMicrosoftPackage.Facts.dll"
    
    $TestCount = 0
    
    foreach ($Test in $TestAssemblies) {
        & $xUnitExe (Join-Path $PSScriptRoot $Test) -xml "Results.$TestCount.xml"
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
