<#
.SYNOPSIS
    Exports Compiled Non-Minified Static Assets

.PARAMETER CssExportPath
    The path to export the Gallery's CSS assets

.PARAMETER JsExportPath
    The path to export the Gallery's JS assets
#>
param(
    [Parameter(Mandatory=$true)]
    [string] $CssExportPath,

    [Parameter(Mandatory=$true)]
    [string] $JsExportPath)

$galleryPath = Join-Path $PSScriptRoot "\..\src\NuGetGallery"

# Export all CSS assets
Copy-Item (Join-Path $galleryPath "\Content\gallery\css\*") $CssExportPath

# Export JS assets that do not have the "page-" prefix
$scriptsPath = Join-Path $galleryPath "\Scripts\Gallery"
$scripts = Get-ChildItem $scriptsPath | ? { $_.Name.StartsWith("page-") -eq $false }

$scripts | % {
	$scriptPath = Join-Path $scriptsPath $_.Name

	Copy-Item (Join-Path $scriptsPath $_.Name) $JsExportPath
}