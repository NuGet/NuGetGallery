# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

<#
.SYNOPSIS
Seeds the local Gallery database with test data needed for functional tests.

.DESCRIPTION
Creates a test user, two organizations (admin + collaborator), and six API keys
with different scopes. Outputs a settings.CI.json config file and sets the
ConfigurationFilePath environment variable for xunit test discovery.

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
$testNupkg = Join-Path $repoRoot "src\NuGetGallery.AppHost\testdata\basetestpackage.1.0.0.nupkg.testdata"
$settingsOutput = Join-Path $repoRoot "tests\NuGetGallery.FunctionalTests\settings.CI.json"

if (-not (Test-Path $galleryToolsExe))
{
	throw "GalleryTools.exe not found at $galleryToolsExe. Build GalleryTools first."
}

function Invoke-GalleryTool
{
	param ([string[]]$Arguments)
	$result = & $galleryToolsExe @Arguments 2>&1
	if ($LASTEXITCODE -ne 0)
	{
		$stderr = ($result | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }) -join "`n"
		$stdout = ($result | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }) -join "`n"
		throw "GalleryTools failed (exit $LASTEXITCODE).`nArgs: $($Arguments -join ' ')`nStdout: $stdout`nStderr: $stderr"
	}
	# Return only stdout lines (not ErrorRecord objects)
	return ($result | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }) -join "`n"
}

# ─── Test account and password ────────────────────────────────────────────────
$testUser = "NugetTestAccount"
$testPassword = "Password1!"
$testEmail = "testnuget@localhost"

# ─── Organization names ──────────────────────────────────────────────────────
$adminOrgName = "NugetTestAdminOrganization"
$collaboratorOrgName = "NugetTestCollaboratorOrganization"

# ─── Second user for collaborator org admin ──────────────────────────────────
$orgAdminUser = "NugetOrgAdmin"
$orgAdminPassword = "Password1!"

Write-Host "=== Seeding functional test data ==="

# 1. Create test user
Write-Host "Creating test user '$testUser'..."
Invoke-GalleryTool "createuser", "--username", $testUser, "--password", $testPassword, "--email", $testEmail

# 2. Create second user (will be admin of the collaborator org)
Write-Host "Creating org admin user '$orgAdminUser'..."
Invoke-GalleryTool "createuser", "--username", $orgAdminUser, "--password", $orgAdminPassword

# 3. Create admin organization (testUser is admin)
Write-Host "Creating admin organization '$adminOrgName'..."
Invoke-GalleryTool "createorganization", "--name", $adminOrgName, "--admin", $testUser

# 4. Create collaborator organization (orgAdminUser is admin, testUser is collaborator)
Write-Host "Creating collaborator organization '$collaboratorOrgName'..."
Invoke-GalleryTool "createorganization", "--name", $collaboratorOrgName, "--admin", $orgAdminUser, "--collaborator", $testUser

# 5. Push the base test package
Write-Host "Pushing BaseTestPackage..."
Invoke-GalleryTool "pushpackage", "--owner", $testUser, "--package", $testNupkg

# 6. Create API keys
Write-Host "Creating API keys..."

$accountApiKey = (Invoke-GalleryTool "createapikey", "--user", $testUser, "--description", "CI Full Access", "--scope", "all").Trim()
Write-Host "  Account API key (all): created"

$apiKeyPush = (Invoke-GalleryTool "createapikey", "--user", $testUser, "--description", "CI Push", "--scope", "push").Trim()
Write-Host "  Account API key (push): created"

$apiKeyPushVersion = (Invoke-GalleryTool "createapikey", "--user", $testUser, "--description", "CI Push Version", "--scope", "pushversion").Trim()
Write-Host "  Account API key (pushversion): created"

$apiKeyUnlist = (Invoke-GalleryTool "createapikey", "--user", $testUser, "--description", "CI Unlist", "--scope", "unlist").Trim()
Write-Host "  Account API key (unlist): created"

$adminOrgApiKey = (Invoke-GalleryTool "createapikey", "--user", $testUser, "--description", "CI Admin Org", "--scope", "all", "--owner-scope", $adminOrgName).Trim()
Write-Host "  Admin org API key: created"

$collabOrgApiKey = (Invoke-GalleryTool "createapikey", "--user", $testUser, "--description", "CI Collaborator Org", "--scope", "all", "--owner-scope", $collaboratorOrgName).Trim()
Write-Host "  Collaborator org API key: created"

# 7. Write settings.CI.json with real keys embedded
$settings = @{
	DefaultSecurityPoliciesEnforced = $true
	TestPackageLock = $false
	TyposquattingCheckAndBlockUsers = $true
	Branding = @{
		BrandingMessage = "&#169; Microsoft {0}"
		PrivacyPolicyUrl = "https://go.microsoft.com/fwlink/?LinkId=521839"
		TrademarksUrl = "https://www.microsoft.com/trademarks"
	}
	Account = @{
		Name = $testUser
		Email = $testEmail
		Password = $testPassword
		ApiKey = $accountApiKey
		ApiKeyPush = $apiKeyPush
		ApiKeyPushVersion = $apiKeyPushVersion
		ApiKeyUnlist = $apiKeyUnlist
	}
	AdminOrganization = @{
		Name = $adminOrgName
		ApiKey = $adminOrgApiKey
	}
	CollaboratorOrganization = @{
		Name = $collaboratorOrgName
		ApiKey = $collabOrgApiKey
	}
	ProductionBaseUrl = "https://localhost"
	StagingBaseUrl = ""
} | ConvertTo-Json -Depth 3

Set-Content -Path $settingsOutput -Value $settings -Encoding UTF8
Write-Host "Settings written to: $settingsOutput"

# 8. Set the config file path for the functional tests
$env:ConfigurationFilePath = $settingsOutput
Write-Host "ConfigurationFilePath = $settingsOutput"

Write-Host "=== Functional test data seeding complete ==="
