$sourceNugetExe = "https://nuget.org/nuget.exe"
$targetNugetExe = ".\nuget.exe"

Invoke-WebRequest $sourceNugetExe -OutFile $targetNugetExe