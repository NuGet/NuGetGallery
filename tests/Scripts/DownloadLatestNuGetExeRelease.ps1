[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Directory
)

$targetNugetExePath = "$Directory\nuget.exe"

if (Test-Path $targetNugetExePath) {
    Write-Host "nuget.exe found in $Directory"
    return
}

Write-Host "nuget.exe not found in $Directory"

$sourceNuGetExePath = Join-Path $PSScriptRoot "nuget.exe"

if (Test-Path $sourceNuGetExePath) {
    Write-Host "Copying nuget.exe from $sourceNuGetExePath"
    Copy-Item $sourceNuGetExePath $targetNugetExePath
} else {
    $sourceNugetExeUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
    Write-Host "Downloading nuget.exe from $sourceNugetExeUrl"
    Invoke-WebRequest $sourceNugetExeUrl -OutFile $targetNugetExePath
}