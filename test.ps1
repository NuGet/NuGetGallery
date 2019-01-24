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

$CLIRoot=$PSScriptRoot
$env:DOTNET_INSTALL_DIR=$CLIRoot

. "$PSScriptRoot\build\common.ps1"

Function Run-Tests {
    [CmdletBinding()]
    param()

    Trace-Log 'Running tests'

    $xUnitExe = (Join-Path $PSScriptRoot "packages\xunit.runner.console\tools\xunit.console.exe")

    $TestAssemblies = "tests\NuGet.Services.KeyVault.Tests\bin\$Configuration\NuGet.Services.KeyVault.Tests.dll", `
        "tests\NuGet.Services.Configuration.Tests\bin\$Configuration\NuGet.Services.Configuration.Tests.dll", `
        "tests\NuGet.Services.Logging.Tests\bin\$Configuration\NuGet.Services.Logging.Tests.dll", `
        "tests\NuGet.Services.AzureManagement.Tests\bin\$Configuration\NuGet.Services.AzureManagement.Tests.dll", `
        "tests\NuGet.Services.Cursor.Tests\bin\$Configuration\NuGet.Services.Cursor.Tests.dll", `
        "tests\NuGet.Services.Owin.Tests\bin\$Configuration\NuGet.Services.Owin.Tests.dll", `
        "tests\NuGet.Services.Validation.Tests\bin\$Configuration\NuGet.Services.Validation.Tests.dll", `
        "tests\NuGet.Services.Contracts.Tests\bin\$Configuration\NuGet.Services.Contracts.Tests.dll", `
        "tests\NuGet.Services.Validation.Issues.Tests\bin\$Configuration\NuGet.Services.Validation.Issues.Tests.dll", `
        "tests\NuGet.Services.Sql.Tests\bin\$Configuration\NuGet.Services.Sql.Tests.dll", `
        "tests\NuGet.Services.Status.Tests\bin\$Configuration\NuGet.Services.Status.Tests.dll", `
        "tests\NuGet.Services.Messaging.Tests\bin\$Configuration\NuGet.Services.Messaging.Tests.dll", `
        "tests\NuGet.Services.Messaging.Email.Tests\bin\$Configuration\NuGet.Services.Messaging.Email.Tests.dll", `
        "tests\NuGet.Services.Licenses.Tests\bin\$Configuration\NuGet.Services.Licenses.Tests.dll"

    $TestCount = 0

    foreach ($Test in $TestAssemblies) {
        & $xUnitExe (Join-Path $PSScriptRoot $Test) -xml "Results.$TestCount.xml"
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

$TestErrors = @()
    
Invoke-BuildStep 'Running tests' { Run-Tests } `
    -ev +TestErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($TestErrors) {
    $ErrorLines = $TestErrors | %{ ">>> $($_.Exception.Message)" }
    Error-Log "Tests completed with $($TestErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -Fatal
}

Write-Host ("`r`n" * 3)