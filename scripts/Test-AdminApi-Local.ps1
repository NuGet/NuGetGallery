<#
.SYNOPSIS
    Tests the Geneva Admin API reflow endpoint against a local or remote NuGetGallery instance.

.DESCRIPTION
    Sends sample POST /api/admin/reflow requests and displays the results.
    Requires the target Gallery instance to have these web.config/app settings:
        Gallery.AdminPanelEnabled = true     (usually already true for admin builds)
        Gallery.GenevaAdminApiEnabled = true
        Gallery.GenevaAdminApiAudience = <your Entra ID audience>
        Gallery.GenevaAdminApiAllowedCallers = <tid>:<appid>

    For local development without real Entra tokens, you can temporarily
    bypass the auth filter by modifying GenevaAdminApiAuthAttribute.

.PARAMETER BaseUrl
    Base URL of the Gallery admin instance (default: https://localhost).

.PARAMETER BearerToken
    An Entra ID access token. If omitted, requests are sent without auth.
    Acquire one via:
        az login
        az account get-access-token --resource <Gallery.GenevaAdminApiAudience>

.PARAMETER SkipAuth
    When set, sends requests without an Authorization header. Useful for
    testing when the auth filter is temporarily disabled for local dev.

.EXAMPLE
    .\Test-AdminApi-Local.ps1 -BaseUrl https://localhost -SkipAuth
    .\Test-AdminApi-Local.ps1 -BearerToken (az account get-access-token --resource api://nuget-admin-api --query accessToken -o tsv)
#>
[CmdletBinding()]
param (
    [string] $BaseUrl = "https://localhost",
    [string] $BearerToken,
    [switch] $SkipAuth
)

$ErrorActionPreference = "Stop"

# Allow self-signed certs for local dev
if ($BaseUrl -match "localhost") {
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    try {
        Add-Type @"
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
}
"@
        [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
    } catch {
        # Type may already be added
    }
}

$endpoint = "$BaseUrl/api/admin/reflow-package"

function Invoke-AdminApi {
    param (
        [string] $TestName,
        [object] $Body,
        [string] $RawBody
    )

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "TEST: $TestName" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    $headers = @{ "Content-Type" = "application/json" }
    if (-not $SkipAuth -and $BearerToken) {
        $headers["Authorization"] = "Bearer $BearerToken"
    }

    $jsonBody = if ($RawBody) { $RawBody } else { $Body | ConvertTo-Json -Depth 5 }
    Write-Host "Request body:" -ForegroundColor Gray
    Write-Host $jsonBody

    try {
        $response = Invoke-WebRequest `
            -Uri $endpoint `
            -Method POST `
            -Headers $headers `
            -Body $jsonBody `
            -UseBasicParsing `
            -ErrorAction Stop

        Write-Host "`nStatus: $($response.StatusCode) $($response.StatusDescription)" -ForegroundColor Green
        Write-Host "Response:" -ForegroundColor Gray
        $response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 5 | Write-Host
    }
    catch {
        $ex = $_.Exception
        if ($ex.Response) {
            $statusCode = [int]$ex.Response.StatusCode
            Write-Host "`nStatus: $statusCode" -ForegroundColor Yellow
            try {
                $reader = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
                $responseBody = $reader.ReadToEnd()
                Write-Host "Response:" -ForegroundColor Gray
                Write-Host $responseBody
            } catch {
                Write-Host "(Could not read response body)"
            }
        }
        else {
            Write-Host "`nError: $($ex.Message)" -ForegroundColor Red
        }
    }
}

# -------------------------------------------------------
# Test 1: Valid request with known packages
# -------------------------------------------------------
Invoke-AdminApi -TestName "Valid packages" -Body @{
    packages = @(
        @{ id = "Newtonsoft.Json"; version = "13.0.3" }
        @{ id = "NuGet.Versioning"; version = "6.0.0" }
    )
    reason = "Local testing of admin API"
}

# -------------------------------------------------------
# Test 2: Mixed valid + invalid + not-found
# -------------------------------------------------------
Invoke-AdminApi -TestName "Mixed statuses" -Body @{
    packages = @(
        @{ id = "Newtonsoft.Json"; version = "13.0.3" }
        @{ id = "Does.Not.Exist.Package"; version = "1.0.0" }
        @{ id = "Bad!Id"; version = "not-a-version" }
    )
    reason = "Testing mixed results"
}

# -------------------------------------------------------
# Test 3: Empty packages list
# -------------------------------------------------------
Invoke-AdminApi -TestName "Empty packages list (expect 400)" -Body @{
    packages = @()
    reason = "Should fail"
}

# -------------------------------------------------------
# Test 4: Over 100 packages (expect 400)
# -------------------------------------------------------
$tooManyPackages = 1..101 | ForEach-Object {
    @{ id = "Pkg$_"; version = "1.0.0" }
}
Invoke-AdminApi -TestName "Over 100 packages (expect 400)" -Body @{
    packages = $tooManyPackages
    reason = "Too many packages"
}

# -------------------------------------------------------
# Test 5: Invalid JSON body
# -------------------------------------------------------
Invoke-AdminApi -TestName "Invalid JSON (expect 400)" -RawBody "{not valid json"

# -------------------------------------------------------
# Test 6: Duplicate packages (should de-duplicate)
# -------------------------------------------------------
Invoke-AdminApi -TestName "Duplicate packages" -Body @{
    packages = @(
        @{ id = "Newtonsoft.Json"; version = "13.0.3" }
        @{ id = "Newtonsoft.Json"; version = "13.0.3" }
        @{ id = "Newtonsoft.Json"; version = "13.0.3.0" }
    )
    reason = "Testing deduplication"
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "All tests complete." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Notes:" -ForegroundColor Gray
Write-Host "  - If you got 404, ensure Gallery.GenevaAdminApiEnabled=true and"
Write-Host "    Gallery.AdminPanelEnabled=true in your dev web.config appSettings."
Write-Host "  - If you got 401, provide a valid -BearerToken or use -SkipAuth for local dev."
Write-Host "  - To get a token: az account get-access-token --resource <your-audience>"
