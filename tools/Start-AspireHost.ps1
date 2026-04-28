# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

<#
.SYNOPSIS
    Builds, starts the Aspire AppHost, and waits for the Gallery health check.
    The host remains running after this script exits so that tests can run against it.
    Use Stop-AspireHost.ps1 to shut it down.

.PARAMETER Configuration
    Build configuration (Release or Debug). Default: Release.

.PARAMETER LaunchProfile
    Aspire launch profile to use. Default: https.

.PARAMETER AppHostProfile
    Value for the APPHOST_PROFILE environment variable, controlling which resources Aspire starts.
    Default: ci-gallery.

.PARAMETER HealthUrls
    URLs to verify after the host starts. The first URL is polled until it responds HTTP 200
    (this is the main readiness gate). All remaining URLs are then checked once.
    Default: Gallery HTTP, Aspire dashboard HTTP, Gallery HTTPS, Aspire dashboard HTTPS.

.PARAMETER Timeout
    Maximum seconds to wait for the first health URL to respond. Default: 300.

.PARAMETER TrustDevCert
    When set, exports the .NET dev certificate and imports it into the local machine trusted root store.
    Requires elevation (admin). Use this in CI where dotnet dev-certs --trust is not available.
#>
param(
	[string]$Configuration = "Release",
	[string]$LaunchProfile = "https",
	[string]$AppHostProfile = "ci-gallery",
	[string[]]$HealthUrls = @(
		"http://localhost/api/health-probe",
		"http://localhost:15170",
		"https://localhost/api/health-probe",
		"https://localhost:17170"
	),
	[int]$Timeout = 300,
	[switch]$TrustDevCert
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appHostProject = Join-Path $repoRoot "src\NuGetGallery.AppHost\NuGetGallery.AppHost.csproj"
$pidFile = Join-Path $repoRoot "aspire-host.pid"

# Step 1: Build AppHost
Write-Host "=== Building NuGetGallery.AppHost ==="
dotnet build $appHostProject -c $Configuration
if ($LASTEXITCODE -ne 0)
{
	Write-Error "AppHost build failed."
	exit 1
}
Write-Host "AppHost build succeeded."

# Step 2: Trust dev certificate (optional)
if ($TrustDevCert)
{
	Write-Host "=== Trusting dev certificate ==="
	$crt = Join-Path $env:TEMP "aspire-dev-cert.crt"
	dotnet dev-certs https -ep $crt --format Pem --no-password
	if ($LASTEXITCODE -ne 0)
	{
		Write-Error "Failed to export dev cert."
		exit 1
	}

	Import-Certificate -FilePath $crt -CertStoreLocation Cert:\LocalMachine\Root
	if (-not $?)
	{
		Write-Error "Failed to import dev cert."
		exit 1
	}

	Write-Host "Dev certificate trusted successfully."
	Remove-Item $crt, ($crt -replace '\.crt$', '.key') -ErrorAction SilentlyContinue
}

# Step 3: Start Aspire host
Write-Host "=== Starting Aspire Host (profile=$AppHostProfile, launch=$LaunchProfile) ==="
$env:APPHOST_PROFILE = $AppHostProfile

$stdoutLog = Join-Path $repoRoot "aspire-stdout.log"
$stderrLog = Join-Path $repoRoot "aspire-stderr.log"

$proc = Start-Process -FilePath "dotnet" `
	-ArgumentList "run", "--project", $appHostProject, "-c", $Configuration, "--no-build", "--launch-profile", $LaunchProfile `
	-PassThru -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog

Write-Host "AppHost started with PID $($proc.Id)"
$proc.Id | Out-File -FilePath $pidFile -Encoding ascii

# Step 4: Poll first URL for readiness
$primaryUrl = $HealthUrls[0]
Write-Host "=== Waiting for $primaryUrl ==="
Start-Sleep -Seconds 20
$elapsed = 20
$healthy = $false
while ($elapsed -lt $Timeout)
{
	if ($proc.HasExited)
	{
		Get-Content $stdoutLog -ErrorAction SilentlyContinue
		Get-Content $stderrLog -ErrorAction SilentlyContinue
		Write-Error "AppHost exited prematurely with code $($proc.ExitCode)."
		exit 1
	}

	try
	{
		$response = Invoke-WebRequest -Uri $primaryUrl -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
		$httpCode = $response.StatusCode
	}
	catch
	{
		$httpCode = 0
	}
	if ($httpCode -eq 200)
	{
		Write-Host "  $primaryUrl -> 200 OK ($elapsed s)"
		$healthy = $true
		break
	}
	Write-Host "  Waiting... ($elapsed s) Status=$httpCode"
	Start-Sleep -Seconds 15
	$elapsed += 15
}

if (-not $healthy)
{
	if (-not $proc.HasExited)
	{
		Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
	}
	Get-Content $stdoutLog -Tail 50 -ErrorAction SilentlyContinue
	Get-Content $stderrLog -Tail 50 -ErrorAction SilentlyContinue
	Write-Error "Primary health URL did not respond within $Timeout seconds."
	exit 1
}

# Step 5: Verify all health URLs
Write-Host "=== Verifying all health URLs ==="
$allPassed = $true
foreach ($url in $HealthUrls)
{
	try
	{
		$response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10 -MaximumRedirection 5 -ErrorAction Stop
		$httpCode = $response.StatusCode
	}
	catch
	{
		$httpCode = 0
		$errorMsg = $_.Exception.Message
	}
	if ($httpCode -eq 200)
	{
		Write-Host "  $url -> 200 OK"
	}
	else
	{
		Write-Host "  $url -> $httpCode FAILED $(if ($errorMsg) { "($errorMsg)" })"
		$allPassed = $false
	}
}

if (-not $allPassed)
{
	if (-not $proc.HasExited)
	{
		Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
	}
	Get-Content $stdoutLog -Tail 50 -ErrorAction SilentlyContinue
	Get-Content $stderrLog -Tail 50 -ErrorAction SilentlyContinue
	Write-Error "One or more health URLs failed verification."
	exit 1
}

Write-Host "=== Aspire Host is running (PID $($proc.Id)) ==="
