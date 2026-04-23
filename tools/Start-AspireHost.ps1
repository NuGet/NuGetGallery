# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

<#
.SYNOPSIS
    Builds and starts the Aspire AppHost for CI, waits for the Gallery health check, then shuts down.

.PARAMETER Configuration
    Build configuration (Release or Debug). Default: Release.

.PARAMETER LaunchProfile
    Aspire launch profile to use. Default: https.

.PARAMETER AppHostProfile
    Value for the APPHOST_PROFILE environment variable, controlling which resources Aspire starts.
    Default: ci-gallery.

.PARAMETER HealthUrl
    URL to poll for Gallery readiness. Default: http://localhost/api/health-probe.

.PARAMETER Timeout
    Maximum seconds to wait for the health check. Default: 300.

.PARAMETER TrustDevCert
    When set, exports the .NET dev certificate and imports it into the local machine trusted root store.
    Requires elevation (admin). Use this in CI where dotnet dev-certs --trust is not available.
#>
param(
	[string]$Configuration = "Release",
	[string]$LaunchProfile = "https",
	[string]$AppHostProfile = "ci-gallery",
	[string]$HealthUrl = "http://localhost/api/health-probe",
	[int]$Timeout = 300,
	[switch]$TrustDevCert
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appHostProject = Join-Path $repoRoot "src\NuGetGallery.AppHost\NuGetGallery.AppHost.csproj"

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

# Step 4: Poll for health
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

	$httpCode = & curl.exe -s -o NUL -w "%{http_code}" $HealthUrl --max-time 5 2>$null
	if ($httpCode -eq "200")
	{
		Write-Host "Gallery is healthy! Status: 200 ($elapsed s)"
		$healthy = $true
		break
	}
	Write-Host "Waiting for Gallery... ($elapsed s) Status=$httpCode"
	Start-Sleep -Seconds 15
	$elapsed += 15
}

# Step 5: Shutdown
if (-not $proc.HasExited)
{
	Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
}

if (-not $healthy)
{
	Get-Content $stdoutLog -Tail 50 -ErrorAction SilentlyContinue
	Get-Content $stderrLog -Tail 50 -ErrorAction SilentlyContinue
	Write-Error "Gallery did not become healthy within $Timeout seconds."
	exit 1
}

Write-Host "=== Aspire Host CI check passed ==="
