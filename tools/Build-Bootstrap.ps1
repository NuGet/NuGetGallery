# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

<#
.SYNOPSIS
    Rebuilds Bootstrap from LESS sources and copies the output to the NuGetGallery project.

.DESCRIPTION
    Runs the Grunt default task in src/Bootstrap which compiles LESS sources, minifies CSS/JS,
    and copies the output to the appropriate locations in src/NuGetGallery.

    Run this script after modifying any LESS files in src/Bootstrap/less/.

.EXAMPLE
    .\tools\Build-Bootstrap.ps1
#>

$ErrorActionPreference = "Stop"
$bootstrapDir = Join-Path $PSScriptRoot "..\src\Bootstrap"

if (-not (Test-Path (Join-Path $bootstrapDir "node_modules")))
{
    Write-Host "Installing npm dependencies..."
    Push-Location $bootstrapDir
    npm install
    Pop-Location
}

Write-Host "Building Bootstrap..."
Push-Location $bootstrapDir
npx grunt
Pop-Location

Write-Host "Bootstrap build complete. Files copied to src/NuGetGallery."
