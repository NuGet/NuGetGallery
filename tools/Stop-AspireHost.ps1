# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

<#
.SYNOPSIS
    Stops the Aspire AppHost process started by Start-AspireHost.ps1.
#>

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$pidFile = Join-Path $repoRoot "aspire-host.pid"

if (-not (Test-Path $pidFile))
{
	Write-Host "No Aspire host PID file found. Nothing to stop."
	exit 0
}

$pid = [int](Get-Content $pidFile -Raw).Trim()
$proc = Get-Process -Id $pid -ErrorAction SilentlyContinue

if ($proc)
{
	Write-Host "Stopping Aspire host (PID $pid)..."
	Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
	Write-Host "Aspire host stopped."
}
else
{
	Write-Host "Aspire host process (PID $pid) is not running."
}

Remove-Item $pidFile -ErrorAction SilentlyContinue
