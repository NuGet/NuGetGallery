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

Copy-Item (Join-Path $galleryPath "\Content\gallery\css\*") $CssExportPath
Copy-Item (Join-Path $galleryPath "\Scripts\gallery\*") $JsExportPath