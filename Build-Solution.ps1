param($connectionString = "")

$scriptPath = Split-Path $MyInvocation.MyCommand.Path

$projFile = join-path $scriptPath scripts\frontend\NuGetGallery.msbuild
 
& "$(get-content env:windir)\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" $projFile /t:ReleaseBuild /v:M