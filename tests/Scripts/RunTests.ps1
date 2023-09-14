[CmdletBinding()]
param(
    [string]$Config = "Release",
    [Parameter(Mandatory)][string]$TestCategory
)

# Move working directory one level up
$root = (Get-Item $PSScriptRoot).parent
$rootName = $root.FullName
$rootRootName = $root.parent.FullName

# Required tools
$nuget = "$rootName\nuget.exe"
$BuiltInVsWhereExe = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$VsInstallationPath = & $BuiltInVsWhereExe -latest -prerelease -property installationPath
$vsTest = Join-Path $VsInstallationPath "Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
$xunit = "$rootRootName\packages\xunit.runner.console.2.3.1\tools\net452\xunit.console.exe"

# Test results files
$functionalTestsResults = "$rootName/functionaltests.$TestCategory.xml"
$webUITestResults = "$rootName/NuGetGallery.$TestCategory.WebUITests.trx"
$loadTestResults = "$rootName/NuGetGallery.$TestCategory.LoadTests.trx"

# Clean previous test results
Remove-Item $functionalTestsResults -ErrorAction Ignore
Remove-Item $webUITestResults -ErrorAction Ignore
Remove-Item $loadTestResults -ErrorAction Ignore

# Run functional tests
$fullTestCategory = "$($testCategory)Tests"
$exitCode = 0

$functionalTestsDirectory = "$rootName\NuGetGallery.FunctionalTests\bin\$Config"
& $xunit "$functionalTestsDirectory\NuGetGallery.FunctionalTests.dll" "-trait" "Category=$fullTestCategory" "-xml" $functionalTestsResults
if ($LastExitCode) {
    $exitCode = 1
}

# Run web UI tests
$webTestsDirectory = "$rootName\NuGetGallery.WebUITests.$TestCategory\bin\$Config"

if (Test-Path $webTestsDirectory -PathType Container) { 
    & $vsTest "$webTestsDirectory\NuGetGallery.WebUITests.$TestCategory.dll" "/Settings:$rootName\Local.testsettings" "/Logger:trx;LogFileName=$webUITestResults"
    if ($LastExitCode) {
        $exitCode = 1
    }
}

# Run load tests
$loadTestsDirectory = "$rootName\NuGetGallery.LoadTests\bin\$Config"
& $vsTest "$loadTestsDirectory\NuGetGallery.LoadTests.dll" "/Settings:$rootName\Local.testsettings" "/TestCaseFilter:`"TestCategory=$fullTestCategory`"" "/Logger:trx;LogFileName=$loadTestResults"
if ($LastExitCode) {
    $exitCode = 1
}

exit $exitCode