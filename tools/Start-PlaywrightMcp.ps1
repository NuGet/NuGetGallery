# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OutputDirectory,
    [switch]$ShowHelp
)

$ErrorActionPreference = "Stop"
$npx = Get-Command npx.cmd -ErrorAction SilentlyContinue

if ($null -eq $npx)
{
    $vsWherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    $installationPaths = @()
    if (Test-Path $vsWherePath)
    {
        $installationPaths += & $vsWherePath -products * -all -property installationPath
    }

    $visualStudioRoot = Join-Path $env:ProgramFiles "Microsoft Visual Studio"
    if (Test-Path $visualStudioRoot)
    {
        $installationPaths += Get-ChildItem $visualStudioRoot -Directory |
            Get-ChildItem -Directory |
            Select-Object -ExpandProperty FullName
    }

    foreach ($installationPath in $installationPaths | Select-Object -Unique)
    {
        $candidate = Join-Path $installationPath "MSBuild\Microsoft\VisualStudio\NodeJs\npx.cmd"
        if (Test-Path $candidate)
        {
            $npx = Get-Item $candidate
            break
        }
    }
}

if ($null -eq $npx)
{
    throw "Node.js 18 or later with npx is required to run Playwright MCP."
}

$env:PATH = "$(Split-Path $npx.FullName);$env:PATH"
$arguments = @(
    "-y",
    "@playwright/mcp@0.0.81"
)

if ($ShowHelp)
{
    $arguments += "--help"
}
else
{
    $arguments += @(
        "--isolated",
        "--ignore-https-errors",
        "--browser",
        "msedge",
        "--codegen",
        "csharp",
        "--output-dir",
        $OutputDirectory
    )
}

& $npx.FullName @arguments
exit $LASTEXITCODE
