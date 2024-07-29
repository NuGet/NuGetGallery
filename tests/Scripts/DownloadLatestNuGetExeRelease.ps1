[CmdletBinding()]
param(
    [string]$Directory = "."
)

$targetNugetExePath = "$Directory\nuget.exe"

if (Test-Path $targetNugetExePath) {
    Write-Host "nuget.exe found in $Directory"
    return
}

$sourceNugetExeUrl = "https://dist.nuget.org/win-x86-commandline/v6.10.1/nuget.exe"
Write-Host "Downloading nuget.exe from $sourceNugetExeUrl to $targetNugetExePath"
$ProgressPreference = "SilentlyContinue"
Invoke-WebRequest $sourceNugetExeUrl -OutFile $targetNugetExePath
$ProgressPreference = "Continue"
