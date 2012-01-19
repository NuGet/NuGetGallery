param($connectionString = "")

$scriptPath = Split-Path $MyInvocation.MyCommand.Path
. (join-path $scriptPath Get-ConnectionString.ps1)

if ("$connectionString".Trim() -eq "") 
{
  $connectionString = Get-ConnectionString -configPath (join-path $scriptPath ..\Website\web.config) -connectionStringName NuGetGallery
}

$projFile = join-path $scriptPath NuGetGallery.msbuild
 
& "$(get-content env:windir)\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" $projFile /p:DbConnection=$connectionString /t:RevertDatabase
