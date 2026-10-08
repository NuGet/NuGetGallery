# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$originalProfile = $env:APPHOST_PROFILE
$originalConfigurationFilePath = $env:ConfigurationFilePath
$originalSymbolPromotion = $env:Staging__EnableSymbolPromotion

try
{
    # Requires the full profile's Azure Service Bus and Search prerequisites.
    # Parent validation, malware scanning and symbol ingestion use development substitutes.
    $env:APPHOST_PROFILE = "full"
    $env:Staging__EnableSymbolPromotion = "true"
    Remove-Item Env:\ConfigurationFilePath -ErrorAction SilentlyContinue

    & "$PSScriptRoot\BuildGalleryFunctionalTests.ps1" -Configuration $Configuration
    if ($LASTEXITCODE -ne 0)
    {
        throw "Building staging worker tests failed with exit code $LASTEXITCODE."
    }

    $testDll = Join-Path $repoRoot "tests\NuGetGallery.FunctionalTests\bin\$Configuration\net10.0\NuGetGallery.FunctionalTests.dll"
    $resultsDirectory = Join-Path $repoRoot "tests\TestResults"
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

    dotnet test $testDll `
        --blame-hang-timeout 600s `
        --filter "Category=StagingFullTests" `
        --logger "trx;LogFileName=StagingFullTests.trx" `
        --results-directory $resultsDirectory

    if ($LASTEXITCODE -ne 0)
    {
        throw "Staging worker tests failed with exit code $LASTEXITCODE."
    }
}
finally
{
    $env:APPHOST_PROFILE = $originalProfile
    $env:ConfigurationFilePath = $originalConfigurationFilePath
    $env:Staging__EnableSymbolPromotion = $originalSymbolPromotion
}
