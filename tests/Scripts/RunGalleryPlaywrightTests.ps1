# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("ci-gallery", "full")]
    [string]$AppHostProfile = "ci-gallery",
    [switch]$PwDebug,
    [switch]$Headed,
    [switch]$Dashboard
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$originalAppHostProfile = $env:APPHOST_PROFILE
$originalDashboard = $env:NUGET_PLAYWRIGHT_ASPIRE_DASHBOARD
$originalHeaded = $env:HEADED
$originalNuGetAudit = $env:NuGetAudit
$originalPwDebug = $env:PWDEBUG

try
{
    $env:APPHOST_PROFILE = $AppHostProfile
    $env:NuGetAudit = "false"
    if ($PwDebug)
    {
        $env:PWDEBUG = "1"
    }
    if ($Headed)
    {
        $env:HEADED = "1"
    }
    if ($Dashboard)
    {
        $env:NUGET_PLAYWRIGHT_ASPIRE_DASHBOARD = "true"
    }

    & "$PSScriptRoot\BuildGalleryFunctionalTests.ps1" -Configuration $Configuration
    if ($LASTEXITCODE -ne 0)
    {
        throw "Building the Gallery functional tests failed with exit code $LASTEXITCODE."
    }

    $testDll = Join-Path $repoRoot "tests\NuGetGallery.FunctionalTests\bin\$Configuration\net10.0\NuGetGallery.FunctionalTests.dll"
    $resultsDirectory = Join-Path $repoRoot "tests\TestResults"
    $testResultsPath = Join-Path $resultsDirectory "PlaywrightTests.trx"
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
    Remove-Item $testResultsPath -ErrorAction SilentlyContinue

    dotnet test $testDll `
        --blame-hang-timeout 600s `
        --filter "Category=PlaywrightTests" `
        --logger "trx;LogFileName=PlaywrightTests.trx" `
        --results-directory $resultsDirectory

    $testExitCode = $LASTEXITCODE
    if (Test-Path $testResultsPath)
    {
        [xml]$testResults = Get-Content $testResultsPath -Raw
        $counters = $testResults.TestRun.ResultSummary.Counters
        $duration = [DateTimeOffset]$testResults.TestRun.Times.finish - [DateTimeOffset]$testResults.TestRun.Times.start
        $skipped = [int]$counters.total - [int]$counters.executed

        Write-Host ""
        Write-Host "Playwright test results:"
        Write-Host "  Failed:  $($counters.failed)"
        Write-Host "  Passed:  $($counters.passed)"
        Write-Host "  Skipped: $skipped"
        Write-Host "  Total:   $($counters.total)"
        Write-Host "  Run duration: $($duration.ToString('hh\:mm\:ss'))"
    }
    elseif ($testExitCode -eq 0)
    {
        throw "Gallery Playwright tests completed without producing the expected result file '$testResultsPath'."
    }

    if ($testExitCode -ne 0)
    {
        throw "Gallery Playwright tests failed with exit code $testExitCode."
    }
}
finally
{
    $env:APPHOST_PROFILE = $originalAppHostProfile
    $env:NUGET_PLAYWRIGHT_ASPIRE_DASHBOARD = $originalDashboard
    $env:HEADED = $originalHeaded
    $env:NuGetAudit = $originalNuGetAudit
    $env:PWDEBUG = $originalPwDebug
}
