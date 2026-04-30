# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

<#
.SYNOPSIS
Seeds the local Gallery database with test data needed for functional tests.

.DESCRIPTION
Runs the GalleryTools seedfunctionaltests command which creates test users,
organizations, API keys, and a base test package. Writes settings.CI.json
for the functional test runner.

.PARAMETER Configuration
Build configuration (Release or Debug). Default: Release.
#>
[CmdletBinding()]
param (
	[string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$galleryToolsBin = Join-Path $repoRoot "src\GalleryTools\bin\$Configuration\net472"
$galleryToolsExe = Join-Path $galleryToolsBin "GalleryTools.exe"
$testDataDir = Join-Path $repoRoot "src\NuGetGallery.AppHost\testdata"
$settingsOutput = Join-Path $repoRoot "tests\NuGetGallery.FunctionalTests\settings.CI.json"

if (-not (Test-Path $galleryToolsExe))
{
	throw "GalleryTools.exe not found at $galleryToolsExe. Build GalleryTools first."
}

# Verify the GalleryTools config file exists
$configFile = Join-Path $galleryToolsBin "appsettings.Aspire.config"
if (Test-Path $configFile)
{
	Write-Host "GalleryTools config found: $configFile"
}
else
{
	Write-Host "WARNING: $configFile not found. GalleryTools will use defaults."
}

Write-Host "=== Seeding functional test data ==="

& $galleryToolsExe seedfunctionaltests `
	--output $settingsOutput `
	--package-dir $testDataDir `
	--base-url "https://localhost"

if ($LASTEXITCODE -ne 0)
{
	Write-Host "=== Seed failed. Dumping diagnostics ==="
	Write-Host "=== aspire-stderr.log (last 30 lines) ==="
	Get-Content (Join-Path $repoRoot "aspire-stderr.log") -Tail 30 -ErrorAction SilentlyContinue
	Write-Host "=== aspire-stdout.log (last 30 lines) ==="
	Get-Content (Join-Path $repoRoot "aspire-stdout.log") -Tail 30 -ErrorAction SilentlyContinue
	throw "GalleryTools seedfunctionaltests failed with exit code $LASTEXITCODE"
}

# Set the config file path for the functional tests
$env:ConfigurationFilePath = $settingsOutput
Write-Host "ConfigurationFilePath = $settingsOutput"

Write-Host "=== Functional test data seeding complete ==="
