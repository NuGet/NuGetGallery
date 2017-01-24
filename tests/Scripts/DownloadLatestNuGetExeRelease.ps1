$sourceNugetExeUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
$sourceNuGetExePath = Join-Path $PSScriptRoot "nuget.exe"
$targetNugetExePath = ".\nuget.exe"

if (-Not (Test-Path $targetNugetExePath)) {
    if (Test-Path $sourceNuGetExePath) {
        Copy-Item $sourceNuGetExePath $targetNugetExePath
    } else {
        Invoke-WebRequest $sourceNugetExeUrl -OutFile $targetNugetExePath
    }
}
