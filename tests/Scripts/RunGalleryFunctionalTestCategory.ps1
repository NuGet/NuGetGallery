[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [Parameter(Mandatory)][string]$TestCategory
)

$parentDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoDir = Resolve-Path (Join-Path $parentDir "..")

# Required tools
$xunit = Join-Path $repoDir "packages\xunit.runner.console\tools\net472\xunit.console.exe"

# Test results files
$functionalTestsResults = Join-Path $parentDir "functionaltests.$TestCategory.xml"

# Clean previous test results
Remove-Item $functionalTestsResults -ErrorAction Ignore

$functionalTestsDirectory = Join-Path $parentDir "NuGetGallery.FunctionalTests\bin\$Configuration\net472"

# Run functional tests
$fullTestCategory = "$($testCategory)Tests"
$exitCode = 0

& $xunit "$functionalTestsDirectory\NuGetGallery.FunctionalTests.dll" "-trait" "Category=$fullTestCategory" "-xml" $functionalTestsResults
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
