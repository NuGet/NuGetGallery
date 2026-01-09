[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [Parameter(Mandatory)][string]$TestCategory
)

$parentDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoDir = Resolve-Path (Join-Path $parentDir "..")

# Required tools
$xunit = "$repoDir\packages\xunit.runner.console\tools\net472\xunit.console.exe"

# Test results files
$functionalTestsResults = "$parentDir/functionaltests.$TestCategory.xml"

# Clean previous test results
Remove-Item $functionalTestsResults -ErrorAction Ignore

$functionalTestsDirectory = "$parentDir\NuGetGallery.FunctionalTests\bin\$Configuration\net472"

# Run functional tests
$fullTestCategory = "$($testCategory)Tests"
$exitCode = 0

& $xunit "$functionalTestsDirectory\NuGetGallery.FunctionalTests.dll" "-trait" "Category=$fullTestCategory" "-xml" $functionalTestsResults
if ($LASTEXITCODE -ne 0) {
    $exitCode = 1
}

exit $exitCode
