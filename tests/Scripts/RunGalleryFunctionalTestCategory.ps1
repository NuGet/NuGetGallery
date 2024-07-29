[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [Parameter(Mandatory)][string]$TestCategory
)

$parentDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoDir = Resolve-Path (Join-Path $parentDir "..")

# Required tools
$BuiltInVsWhereExe = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$VsInstallationPath = & $BuiltInVsWhereExe -latest -prerelease -property installationPath
$vsTest = Join-Path $VsInstallationPath "Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
$xunit = "$repoDir\packages\xunit.runner.console\tools\net472\xunit.console.exe"

# Test results files
$functionalTestsResults = "$parentDir/functionaltests.$TestCategory.xml"
$webUITestResults = "$parentDir/NuGetGallery.$TestCategory.WebUITests.trx"
$loadTestResults = "$parentDir/NuGetGallery.$TestCategory.LoadTests.trx"

# Clean previous test results
Remove-Item $functionalTestsResults -ErrorAction Ignore
Remove-Item $webUITestResults -ErrorAction Ignore
Remove-Item $loadTestResults -ErrorAction Ignore

# Run functional tests
$fullTestCategory = "$($testCategory)Tests"
$exitCode = 0

$functionalTestsDirectory = "$parentDir\NuGetGallery.FunctionalTests\bin\$Configuration\net472"
& $xunit "$functionalTestsDirectory\NuGetGallery.FunctionalTests.dll" "-trait" "Category=$fullTestCategory" "-xml" $functionalTestsResults
if ($LASTEXITCODE -ne 0) {
    $exitCode = 1
}

# Run web UI tests
$webTestsDirectory = "$parentDir\NuGetGallery.WebUITests.$TestCategory\bin\$Configuration\net472"

if (Test-Path $webTestsDirectory -PathType Container) { 
    & $vsTest "$webTestsDirectory\NuGetGallery.WebUITests.$TestCategory.dll" "/Settings:$parentDir\Local.testsettings" "/Logger:trx;LogFileName=$webUITestResults"
    if ($LASTEXITCODE -ne 0) {
        $exitCode = 1
    }
}

# Run load tests
$loadTestsDirectory = "$parentDir\NuGetGallery.LoadTests\bin\$Configuration\net472"
& $vsTest "$loadTestsDirectory\NuGetGallery.LoadTests.dll" "/Settings:$parentDir\Local.testsettings" "/TestCaseFilter:`"TestCategory=$fullTestCategory`"" "/Logger:trx;LogFileName=$loadTestResults"
if ($LASTEXITCODE -ne 0) {
    $exitCode = 1
}

exit $exitCode
