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
    Maximum seconds to wait for the first health URL to respond. Default: 600.

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
	[int]$Timeout = 600,
	[switch]$TrustDevCert,
	# WARNING: This flag compiles in an auth bypass for Admin API functional testing.
	# It must NEVER be used in release or deployment builds.
	[switch]$UnsafeAdminApiAuthBypass
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appHostProject = Join-Path $repoRoot "src\NuGetGallery.AppHost\NuGetGallery.AppHost.csproj"

# Step 1: Build AppHost
Write-Host "##[group]Building NuGetGallery.AppHost"
$buildArgs = @($appHostProject, "-c", $Configuration)
if ($UnsafeAdminApiAuthBypass) {
	$buildArgs += "/p:UnsafeAdminApiAuthBypass=true"
}
dotnet build @buildArgs | Out-Host
if ($LASTEXITCODE -ne 0)
{
	Write-Error "AppHost build failed."
	exit 1
}
Write-Host "AppHost build succeeded."
Write-Host "##[endgroup]"

# Step 2: Trust dev certificate (optional)
if ($TrustDevCert)
{
	Write-Host "##[group]Trusting dev certificate"
	$crt = Join-Path $env:TEMP "aspire-dev-cert.crt"
	dotnet dev-certs https -ep $crt --format Pem --no-password | Out-Host
	if ($LASTEXITCODE -ne 0)
	{
		Write-Error "Failed to export dev cert."
		exit 1
	}

	Import-Certificate -FilePath $crt -CertStoreLocation Cert:\LocalMachine\Root | Out-Host
	if (-not $?)
	{
		Write-Error "Failed to import dev cert."
		exit 1
	}

	Write-Host "Dev certificate trusted successfully."
	Remove-Item $crt, ($crt -replace '\.crt$', '.key') -ErrorAction SilentlyContinue
	Write-Host "##[endgroup]"
}

# Step 3: Start Aspire host (with retry for silent IIS Express launch failures)
$env:APPHOST_PROFILE = $AppHostProfile

$maxAttempts = 3
# After this many seconds without IIS Express, abort and retry
$iisExpressGracePeriod = 180
$healthy = $false
$proc = $null

function Find-GalleryIISExpress
{
	# Find the specific IIS Express instance for the NuGet Gallery site
	Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
		Where-Object { $_.Name -eq "iisexpress.exe" -and $_.CommandLine -match "NuGet Gallery" }
}

function Stop-AppHostProcessTree([System.Diagnostics.Process]$hostProc)
{
	if ($hostProc -and -not $hostProc.HasExited)
	{
		Write-Host "  Stopping AppHost process tree (PID $($hostProc.Id))..."
		# Kill IIS Express first
		Find-GalleryIISExpress | ForEach-Object {
			Write-Host "  Stopping IIS Express PID=$($_.ProcessId)"
			Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
		}
		# Kill all child processes
		Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $hostProc.Id } | ForEach-Object {
			Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
		}
		Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue
		Start-Sleep -Seconds 2
	}
}

for ($attempt = 1; $attempt -le $maxAttempts; $attempt++)
{
	Write-Host "=== Starting Aspire Host (attempt $attempt/$maxAttempts, profile=$AppHostProfile, launch=$LaunchProfile) ==="

	$stdoutLog = Join-Path $repoRoot "aspire-stdout-attempt$attempt.log"
	$stderrLog = Join-Path $repoRoot "aspire-stderr-attempt$attempt.log"

	$argLine = "run --project `"$appHostProject`" -c $Configuration --no-build --launch-profile $LaunchProfile"
	$proc = Start-Process dotnet -ArgumentList $argLine `
		-RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog `
		-PassThru

	Write-Host "AppHost started with PID $($proc.Id)"

	$primaryUrl = $HealthUrls[0]
	Write-Host "=== Waiting for $primaryUrl ==="
	$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
	$lastDiagnosticDump = 0
	$abortAttempt = $false

	while ($true)
	{
		$elapsed = [int]$stopwatch.Elapsed.TotalSeconds
		if ($elapsed -ge $Timeout) { break }
		if ($proc.HasExited)
		{
			Write-Host "##[group]AppHost crashed - stdout/stderr"
			Get-Content $stdoutLog -ErrorAction SilentlyContinue | Out-Host
			Get-Content $stderrLog -ErrorAction SilentlyContinue | Out-Host
			Write-Host "##[endgroup]"
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
			$healthError = $_.Exception.Message
		}

		if ($httpCode -eq 200)
		{
			Write-Host "  $primaryUrl -> 200 OK ($elapsed s)"
			$healthy = $true
			break
		}
		Write-Host "  Waiting... ($elapsed s) Status=$httpCode $(if ($healthError) { "($healthError)" })"

		# Check if IIS Express has started; if not after grace period, abort this attempt
		if ($elapsed -ge $iisExpressGracePeriod -and $attempt -lt $maxAttempts)
		{
			$iisProc = Find-GalleryIISExpress
			if (-not $iisProc)
			{
				Write-Host "  IIS Express has not started after $elapsed s. Aborting attempt $attempt and retrying..."
				Stop-AppHostProcessTree $proc
				$abortAttempt = $true
				break
			}
		}

		# Log IIS Express status once per 60 seconds
		if ($elapsed - $lastDiagnosticDump -ge 60)
		{
			$lastDiagnosticDump = $elapsed
			$iisProcs = Find-GalleryIISExpress
			if ($iisProcs)
			{
				foreach ($iis in $iisProcs) { Write-Host "  IIS Express PID=$($iis.ProcessId)" }
			}
			else
			{
				Write-Host "  IIS Express: not running"
			}
		}

		Start-Sleep -Seconds 15
	}

	if ($healthy -or (-not $abortAttempt))
	{
		break
	}

	Write-Host "=== Attempt $attempt failed. Cleaning up before retry... ==="
	Start-Sleep -Seconds 5
}

if (-not $healthy)
{
	Write-Host "##[group]Timeout - AppHost stdout/stderr"
	Get-Content $stdoutLog -ErrorAction SilentlyContinue | Out-Host
	Get-Content $stderrLog -ErrorAction SilentlyContinue | Out-Host
	Write-Host "##[endgroup]"

	if (-not $proc.HasExited)
	{
		Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
	}
	Write-Error "Primary health URL did not respond within $Timeout seconds (after $maxAttempts attempts)."
	exit 1
}

# Step 5: Verify all health URLs
Write-Host "##[group]Verifying all health URLs"
$allPassed = $true
foreach ($url in $HealthUrls)
{
	try
	{
		$response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30 -MaximumRedirection 5 -ErrorAction Stop
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
Write-Host "##[endgroup]"

if (-not $allPassed)
{
	if (-not $proc.HasExited)
	{
		Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
	}
	Get-Content $stdoutLog -Tail 50 -ErrorAction SilentlyContinue | Out-Host
	Get-Content $stderrLog -Tail 50 -ErrorAction SilentlyContinue | Out-Host
	Write-Error "One or more health URLs failed verification."
	exit 1
}

Write-Host "=== Aspire Host is running (PID $($proc.Id)) ==="

# Return the PID so callers can pass it to Stop-AspireHost.ps1
return $proc.Id
