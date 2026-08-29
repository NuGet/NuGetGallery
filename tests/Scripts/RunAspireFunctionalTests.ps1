# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$TrustDevCert,
    # WARNING: This flag compiles in an auth bypass for Admin API functional testing.
    # It must NEVER be used in release or deployment builds.
    [switch]$UnsafeAdminApiAuthBypassForTesting
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$hostPid = $null
$originalExternalHost = $env:NUGET_PLAYWRIGHT_EXTERNAL_HOST

try
{
    Write-Host "=== Building functional tests ==="
    & "$PSScriptRoot\BuildGalleryFunctionalTests.ps1" -Configuration $Configuration
    if ($LASTEXITCODE -ne 0)
    {
        throw "Building functional tests failed with exit code $LASTEXITCODE."
    }

    Write-Host "=== Starting Aspire Host ==="
    $startArguments = @{
        Configuration = $Configuration
    }
    if ($TrustDevCert)
    {
        $startArguments.TrustDevCert = $true
    }
    if ($UnsafeAdminApiAuthBypassForTesting)
    {
        $startArguments.UnsafeAdminApiAuthBypassForTesting = $true
    }

    $hostPid = & "$repoRoot\tools\Start-AspireHost.ps1" @startArguments
    Write-Host "Aspire Host PID: $hostPid"

    Write-Host "=== Seeding functional test data ==="
    & "$repoRoot\tools\Seed-FunctionalTestData.ps1" -Configuration $Configuration
    if ($LASTEXITCODE -ne 0)
    {
        throw "Seeding functional test data failed with exit code $LASTEXITCODE."
    }

    $categories = "Category=P0Tests|Category=P1Tests|Category=P2Tests"
    if ($UnsafeAdminApiAuthBypassForTesting)
    {
        $categories += "|Category=AdminApiTests"
    }

    Write-Host "=== Running non-Playwright functional tests: $categories ==="
    $testDll = Join-Path $repoRoot "tests\NuGetGallery.FunctionalTests\bin\$Configuration\net10.0\NuGetGallery.FunctionalTests.dll"
    $resultsDirectory = Join-Path $repoRoot "tests\TestResults"
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
    $env:NUGET_PLAYWRIGHT_EXTERNAL_HOST = [bool]::TrueString

    dotnet test $testDll `
        --blame-hang-timeout 600s `
        --filter "($categories)&Category!=PlaywrightTests" `
        --logger "trx;LogFileName=FunctionalTests.trx" `
        --results-directory $resultsDirectory

    $testExitCode = $LASTEXITCODE
    Write-Host "dotnet test exited with code $testExitCode"

    Get-ChildItem -File -Recurse $resultsDirectory | ForEach-Object {
        Write-Host "  Result file: $($_.FullName) ($($_.Length) bytes)"
    }

    if ($testExitCode -ne 0)
    {
        throw "Functional tests failed with exit code $testExitCode."
    }
}
finally
{
    try
    {
        if ($null -ne $hostPid)
        {
            Write-Host "=== Stopping Aspire Host ==="
            & "$repoRoot\tools\Stop-AspireHost.ps1" -HostPid $hostPid
        }
    }
    finally
    {
        $env:NUGET_PLAYWRIGHT_EXTERNAL_HOST = $originalExternalHost
    }
}
