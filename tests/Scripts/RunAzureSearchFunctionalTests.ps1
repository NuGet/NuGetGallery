[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$parentDir = Split-Path $PSScriptRoot
$repoDir = Resolve-Path (Join-Path $parentDir "..")

# Required tools
$nuget = Join-Path $parentDir "nuget.exe"
& (Join-Path $PSScriptRoot "DownloadLatestNuGetExeRelease.ps1") $parentDir

Write-Host "Restoring solution tools"
& $nuget install (Join-Path $repoDir "packages.config") -SolutionDirectory $repoDir -NonInteractive -ExcludeVersion

# Clean previous test results
$functionalTestsResults = Join-Path $repoDir "functionaltests.*.xml"
Remove-Item $functionalTestsResults -ErrorAction Ignore

# Restore packages
Write-Host "Restoring solution"
$solutionPath = Join-Path $repoDir "NuGet.Jobs.FunctionalTests.sln"
& $nuget restore $solutionPath -NonInteractive
if ($LASTEXITCODE -ne 0) {
    throw "Failed to restore packages!"
}

# Build the solution
Write-Host "Building solution"
dotnet build $solutionPath --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build solution!"
}

# Run functional tests
Write-Host "Running Azure Search functional tests..."
$testResultFile = Join-Path $repoDir "functionaltests.AzureSearchTests.xml"
$project = Join-Path $parentDir "NuGet.Services.AzureSearch.FunctionalTests"
dotnet test $project --no-restore --no-build --configuration $Configuration "-l:trx;LogFileName=$testResultFile"
if (-not (Test-Path $testResultFile)) {
    Write-Error "The test run failed to produce a result file";
    exit 1
}
