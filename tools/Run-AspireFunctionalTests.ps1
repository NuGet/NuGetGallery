# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

<#
.SYNOPSIS
Starts the Aspire host, seeds test data, builds the Gallery functional tests, runs them, and stops the host.

.PARAMETER Configuration
Build configuration (Release or Debug). Default: Release.

.PARAMETER AppHostProfile
Aspire AppHost profile to use. Default: ci-gallery.

.PARAMETER TrustDevCert
Imports the .NET development certificate into the local machine trusted root store.
Requires elevation and is intended for CI environments.

.PARAMETER IncludeAdminApiTests
Includes Admin API tests and compiles the AppHost with the unsafe test-only authentication bypass.
This switch must never be used for release or deployment builds.
#>
[CmdletBinding()]
param(
	[ValidateSet("", "Debug", "Release")]
	[string]$Configuration = "Release",
	[ValidateSet("ci-gallery", "full")]
	[string]$AppHostProfile = "ci-gallery",
	[switch]$TrustDevCert,
	[switch]$IncludeAdminApiTests
)

if ([string]::IsNullOrWhiteSpace($Configuration))
{
	$Configuration = "Release"
}

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$originalConfigurationFilePath = $env:ConfigurationFilePath
$originalNuGetAudit = $env:NuGetAudit
$originalAppHostProfile = $env:APPHOST_PROFILE
$originalNodeNoWarnings = $env:NODE_NO_WARNINGS
$hostPid = $null

try
{
	$env:NuGetAudit = "false"

	Write-Host "=== Building functional tests ==="
	& "$repoRoot\tests\Scripts\BuildGalleryFunctionalTests.ps1" -Configuration $Configuration
	if ($LASTEXITCODE -ne 0)
	{
		throw "Building functional tests failed with exit code $LASTEXITCODE."
	}

	Write-Host "=== Starting Aspire Host ==="
	$startArguments = @{
		Configuration = $Configuration
		AppHostProfile = $AppHostProfile
	}
	if ($TrustDevCert)
	{
		$startArguments.TrustDevCert = $true
	}
	if ($IncludeAdminApiTests)
	{
		$startArguments.UnsafeAdminApiAuthBypassForTesting = $true
	}

	$hostPid = & "$PSScriptRoot\Start-AspireHost.ps1" @startArguments
	Write-Host "Aspire Host PID: $hostPid"

	Write-Host "=== Seeding functional test data ==="
	& "$PSScriptRoot\Seed-FunctionalTestData.ps1" -Configuration $Configuration
	if ($LASTEXITCODE -ne 0)
	{
		throw "Seeding functional test data failed with exit code $LASTEXITCODE."
	}

	$categories = "Category=P0Tests|Category=P1Tests|Category=P2Tests"
	if ($IncludeAdminApiTests)
	{
		$categories += "|Category=AdminApiTests"
	}

	Write-Host "=== Running non-Playwright functional tests: $categories ==="
	$testDll = Join-Path $repoRoot "tests\NuGetGallery.FunctionalTests\bin\$Configuration\net10.0\NuGetGallery.FunctionalTests.dll"
	$resultsDirectory = Join-Path $repoRoot "tests\TestResults"
	New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

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
			& "$PSScriptRoot\Stop-AspireHost.ps1" -HostPid $hostPid
		}
	}
	finally
	{
		$env:ConfigurationFilePath = $originalConfigurationFilePath
		$env:NuGetAudit = $originalNuGetAudit
		$env:APPHOST_PROFILE = $originalAppHostProfile
		$env:NODE_NO_WARNINGS = $originalNodeNoWarnings
	}
}
