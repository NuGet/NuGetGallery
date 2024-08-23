[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$parentDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoDir = Resolve-Path (Join-Path $parentDir "..")

# Required tools
$BuiltInVsWhereExe = "${Env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$VsInstallationPath = & $BuiltInVsWhereExe -latest -prerelease -property installationPath
$msbuild = Join-Path $VsInstallationPath "MSBuild\Current\Bin\msbuild"
$nuget = Join-Path $parentDir "nuget.exe"
& (Join-Path $PSScriptRoot "DownloadLatestNuGetExeRelease.ps1") $parentDir

Write-Host "Restoring solution tools"
& $nuget install (Join-Path $repoDir "packages.config") -SolutionDirectory $repoDir -NonInteractive -ExcludeVersion

# Restore packages
Write-Host "Restoring solution"
$solutionPath = Join-Path $repoDir "NuGetGallery.FunctionalTests.sln"
& $nuget restore $solutionPath -NonInteractive
if ($LASTEXITCODE -ne 0) {
    throw "Failed to restore packages!"
}

# Build the solution
Write-Host "Building solution"
& $msbuild $solutionPath "/p:Configuration=$Configuration" ('/p:VSINSTALLDIR="' + $VsInstallationPath + '"')
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build solution!"
}

$functionalTestsDirectory = Join-Path $parentDir "NuGetGallery.FunctionalTests\bin\$Configuration\net472"
Copy-Item $nuget $functionalTestsDirectory
