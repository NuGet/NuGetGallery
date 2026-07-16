[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [Parameter(Mandatory)][string]$TestCategory
)

$parentDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoDir = Resolve-Path (Join-Path $parentDir "..")

# Test results files
$functionalTestsResults = Join-Path $parentDir "functionaltests.$TestCategory.xml"

# Clean previous test results
Remove-Item $functionalTestsResults -ErrorAction Ignore

$functionalTestsProject = Join-Path $parentDir "NuGetGallery.FunctionalTests\NuGetGallery.FunctionalTests.csproj"

# Run functional tests
$fullTestCategory = "$($testCategory)Tests"
$exitCode = 0

& dotnet test $functionalTestsProject --configuration $Configuration --no-build --filter "Category=$fullTestCategory" --results-directory (Split-Path $functionalTestsResults) --logger "trx;LogFileName=$(Split-Path $functionalTestsResults -Leaf)"
if ($LASTEXITCODE -ne 0) {
    $exitCode = 1
}

if (Test-Path $functionalTestsResults) {
    Write-Host "Test results for category $TestCategory are located at $functionalTestsResults"
}
else {
    Write-Error "The test run failed to produce a result file for category $TestCategory";
    $exitCode = 1
}

exit $exitCode
