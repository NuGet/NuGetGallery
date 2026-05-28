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

# Diagnostic: show what sources dotnet sees with and without the repo config
Write-Host "##[group]NuGet source diagnostics"
Write-Host "--- Sources without --configfile (full hierarchy) ---"
& dotnet nuget list source
Write-Host "--- Sources with --configfile (repo config only) ---"
& dotnet nuget list source --configfile $nugetConfig
Write-Host "--- All NuGet.config files on disk ---"
Get-ChildItem -Path (Split-Path $repoDir -Qualifier) -Filter "NuGet.config" -Recurse -ErrorAction SilentlyContinue -Depth 6 | ForEach-Object { Write-Host $_.FullName }
Write-Host "##[endgroup]"

& dotnet restore $solutionPath --configfile $nugetConfig
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
