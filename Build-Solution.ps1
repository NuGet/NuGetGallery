$scriptPath = Split-Path $MyInvocation.MyCommand.Path
$projFile = join-path $scriptPath DeployScripts\NuGetGalleryOps.msbuild
 
& "$(get-content env:windir)\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" $projFile /t:Build