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

if ($TrustDevCert)
{
    Write-Host "=== Trusting development certificate ==="
    & "$repoRoot\tools\Trust-DevCertificate.ps1"
}

$buildArguments = @{
    Configuration = $Configuration
}
if ($UnsafeAdminApiAuthBypassForTesting)
{
    $buildArguments.UnsafeAdminApiAuthBypassForTesting = $true
}

Write-Host "=== Building functional tests ==="
& "$PSScriptRoot\BuildGalleryFunctionalTests.ps1" @buildArguments
if ($LASTEXITCODE -ne 0)
{
    throw "Building functional tests failed with exit code $LASTEXITCODE."
}

$categories = "Category=P0Tests|Category=P1Tests|Category=P2Tests|Category=PlaywrightTests"
if ($UnsafeAdminApiAuthBypassForTesting)
{
    $categories += "|Category=AdminApiTests"
}

Write-Host "=== Running Aspire-hosted functional tests: $categories ==="
$testDll = Join-Path $repoRoot "tests\NuGetGallery.FunctionalTests\bin\$Configuration\net10.0\NuGetGallery.FunctionalTests.dll"
$resultsDirectory = Join-Path $repoRoot "tests\TestResults"
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

dotnet test $testDll `
    --blame-hang-timeout 600s `
    --filter $categories `
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
