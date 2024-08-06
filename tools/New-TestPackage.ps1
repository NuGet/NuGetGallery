<#
.SYNOPSIS
    Creates a test package

.PARAMETER Id
    The ID of the package to create

.PARAMETER Version
    The version of the package to create (defaults to 1.0.0)

.PARAMETER Title
    The title of the package to create (defaults to the ID)

.PARAMETER Description
    The description of the package to create (defaults to "A test package")

.PARAMETER OutputDirectory
    The directory in which to save the package (defaults to the current directory)

.PARAMETER AutoGenerateId
    Set this switch to auto generate the ID
#>
param(
    [Parameter(Mandatory=$true, ParameterSetName="AutoId")][switch]$AutoGenerateId,
    [Parameter(Mandatory=$true, Position=0, ParameterSetName="ManualId")][string]$Id, 
    [Parameter(Mandatory=$false, Position=1)][string]$Version = "1.0.0",
    [Parameter(Mandatory=$false, Position=2)][string]$Title,
    [Parameter(Mandatory=$false)][string]$Description = "A test package",
    [Parameter(Mandatory=$false)][string]$OutputDirectory)

if(!(Get-Command nuget -ErrorAction SilentlyContinue)) {
    throw "You must have nuget.exe in your path to use this command!"
}

if(($PsCmdlet.ParameterSetName -eq "AutoId") -and $AutoGenerateId) {
    $ts = [DateTime]::Now.ToString("yyMMddHHmmss")
    $Id = "$([Environment]::UserName)_test_$ts"
}

if(!$OutputDirectory) {
    $OutputDirectory = Get-Location
}
$OutputDirectory = (Convert-Path $OutputDirectory)

if(!$Title) {
    $Title = $Id
}

$tempdir = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
mkdir $tempdir | Out-Null

$contentDir = Join-Path $tempdir "content"
mkdir $contentDir | Out-Null

$testFile = Join-Path $contentDir "Test.txt"
"Test" | Out-File -Encoding UTF8 -FilePath $testFile

$nuspec = Join-Path $tempdir "$Id.nuspec"
@"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
    <metadata>
        <id>$Id</id>
        <version>$Version</version>
        <title>$Title</title>
        <authors>$([Environment]::UserName)</authors>
        <requireLicenseAcceptance>false</requireLicenseAcceptance>
        <description>$Description</description>
    </metadata>
</package>
"@ | Out-File -Encoding UTF8 -FilePath $nuspec

nuget pack "$nuspec" -BasePath "$tempdir" -OutputDirectory $OutputDirectory

rm -Recurse -Force $tempdir