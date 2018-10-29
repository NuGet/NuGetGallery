$sourceNugetExe = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
$targetNugetExe = ".\nuget.exe"

Invoke-WebRequest $sourceNugetExe -OutFile $targetNugetExe