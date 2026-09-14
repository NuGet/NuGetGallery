# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("ci-gallery", "full")]
    [string]$AppHostProfile = "ci-gallery"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$originalAppHostProfile = $env:APPHOST_PROFILE
$originalNuGetAudit = $env:NuGetAudit

try
{
    $env:APPHOST_PROFILE = $AppHostProfile
    $env:NuGetAudit = "false"

    & "$PSScriptRoot\BuildGalleryFunctionalTests.ps1" -Configuration $Configuration
    if ($LASTEXITCODE -ne 0)
    {
        throw "Building the Gallery functional tests failed with exit code $LASTEXITCODE."
    }

    $testDll = Join-Path $repoRoot "tests\NuGetGallery.FunctionalTests\bin\$Configuration\net10.0\NuGetGallery.FunctionalTests.dll"
    $resultsDirectory = Join-Path $repoRoot "tests\TestResults"
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

    dotnet test $testDll `
        --blame-hang-timeout 600s `
        --filter "Category=PlaywrightTests" `
        --logger "trx;LogFileName=PlaywrightTests.trx" `
        --results-directory $resultsDirectory

    if ($LASTEXITCODE -ne 0)
    {
        throw "Gallery Playwright tests failed with exit code $LASTEXITCODE."
    }
}
finally
{
    $env:APPHOST_PROFILE = $originalAppHostProfile
    $env:NuGetAudit = $originalNuGetAudit
}
