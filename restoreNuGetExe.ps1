$sourceNugetExe = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
$toolsFolder = "tools"
$targetNugetExe = "$toolsFolder\nuget.exe"

if((Test-Path $toolsFolder) -eq 0)
{
    New-Item -ItemType Directory -Force -Path $toolsFolder
}

Invoke-WebRequest $sourceNugetExe -OutFile $targetNugetExe