[CmdletBinding()]
param(
    [string]$Config = "Release",
    [string]$SolutionPath = "NuGetGallery.FunctionalTests.sln"
)

# Move working directory one level up
$rootName = (Get-Item $PSScriptRoot).parent.FullName

# Required tools
$msBuild = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\msbuild"
$nuget = "$rootName\nuget.exe"
& "$rootName\Scripts\DownloadLatestNuGetExeRelease.ps1" $rootName

# Restore packages
Write-Host "Restoring packages"
$fullSolutionPath = "$rootName\$SolutionPath"
& $nuget "restore" $fullSolutionPath "-NonInteractive"
if ($LastExitCode) {
    throw "Failed to restore packages!"
}

# Build the solution
Write-Host "Building solution"
& $msBuild $fullSolutionPath "/p:Configuration=$Config" "/p:Platform=Any CPU" "/p:CodeAnalysis=true" "/m" "/v:M" "/fl" "/flp:LogFile=$rootName\msbuild.log;Verbosity=diagnostic" "/nr:false"
if ($LastExitCode) {
    throw "Failed to build solution!"
}

$functionalTestsDirectory = "$rootName\NuGetGallery.FunctionalTests\bin\$Config"
Copy-Item $nuget $functionalTestsDirectory