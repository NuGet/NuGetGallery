param(
    [Parameter(Mandatory=$false)][string]$Configuration = "Release")

$MyPath = Split-Path $MyInvocation.MyCommand.Path
$ScriptsDir = Join-Path $MyPath Scripts

& $ScriptsDir\Restore-Packages.ps1
& "$(get-content env:windir)\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" "$ScriptsDir\NuGetOperations.msbuild" /t:Build /p:"Configuration=$Configuration"