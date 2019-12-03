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
$msTest = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio 14.0\Common7\IDE\mstest.exe"
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

if(Test-Path $webTestsDirectory -PathType Container) { 
	& $msTest "/TestContainer:$webTestsDirectory\NuGetGallery.WebUITests.$TestCategory.dll" "/TestSettings:$rootName\Local.testsettings" "/detail:stdout" "/resultsfile:$webUITestResults"
	if ($LastExitCode) {
		$exitCode = 1
	}
}

# Run load tests
$loadTestsDirectory = "$rootName\NuGetGallery.LoadTests\bin\$Config"
& $msTest "/TestContainer:$loadTestsDirectory\NuGetGallery.LoadTests.dll" "/TestSettings:$rootName\Local.testsettings" "/detail:stdout" "/category:$fullTestCategory" "/resultsfile:$loadTestResults"
if ($LastExitCode) {
    $exitCode = 1
}

exit $exitCode