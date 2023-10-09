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

$sourceNugetExeUrl = "https://dist.nuget.org/win-x86-commandline/v6.7.0/nuget.exe"
Write-Host "Downloading nuget.exe from $sourceNugetExeUrl"
Invoke-WebRequest $sourceNugetExeUrl -OutFile $targetNugetExePath
