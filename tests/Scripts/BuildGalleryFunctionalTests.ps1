[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$parentDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoDir = Resolve-Path (Join-Path $parentDir "..")

# Required tools
$nuget = Join-Path $parentDir "nuget.exe"
& (Join-Path $PSScriptRoot "DownloadLatestNuGetExeRelease.ps1") $parentDir

Write-Host "##[group]Restoring and building functional tests"

# Restore and build using dotnet CLI
Write-Host "Restoring and building solution"
$solutionPath = Join-Path $repoDir "NuGetGallery.FunctionalTests.sln"
$nugetConfig = Join-Path $repoDir "NuGet.config"

# Diagnostic: show what sources dotnet sees
Write-Host "##[group]NuGet source diagnostics"
Write-Host "--- Sources (full hierarchy) ---"
& dotnet nuget list source
Write-Host "--- Sources (repo config only) ---"
& dotnet nuget list source --configfile $nugetConfig
Write-Host "##[endgroup]"

# The nuget-server-upstreams source may be registered at the machine/user level on build agents
# (e.g., by the Azure Artifacts credential provider during a prior nuget.exe restore).
# It requires devdiv org credentials that CI agents don't have. Remove it if present.
$sourceCheck = & dotnet nuget list source 2>&1 | Out-String
if ($sourceCheck -match 'nuget-server-upstreams') {
    Write-Host "Removing nuget-server-upstreams source (not needed, causes 401 on public agents)"
    & dotnet nuget remove source nuget-server-upstreams 2>$null
}

& dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) {
    throw "Failed to restore packages!"
}

Write-Host "Building solution"
& dotnet build $solutionPath --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build solution!"
}

Write-Host "Copying nuget.exe to functional tests directory"
$functionalTestsDirectory = Join-Path $parentDir "NuGetGallery.FunctionalTests\bin\$Configuration\net10.0"
Copy-Item $nuget $functionalTestsDirectory
Write-Host "##[endgroup]"

Write-Host "##[group]Installing Playwright browsers"
# used to suppress Node.js warnings about url.parse deprecation
# https://github.com/microsoft/playwright/issues/36404
$env:NODE_NO_WARNINGS = "1"
& "$functionalTestsDirectory\playwright.ps1" install
$env:NODE_NO_WARNINGS = ""
Write-Host "##[endgroup]"
