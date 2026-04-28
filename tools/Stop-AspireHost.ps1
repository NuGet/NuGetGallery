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

$hostPid = [int](Get-Content $pidFile -Raw).Trim()
$proc = Get-Process -Id $hostPid -ErrorAction SilentlyContinue

if ($proc)
{
	Write-Host "Stopping Aspire host process tree (root PID $hostPid)..."
	# Kill the entire process tree. The root is cmd.exe which wraps dotnet run,
	# which wraps the AppHost, which manages DCP/Azurite/IIS Express.
	$children = Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $hostPid }
	foreach ($child in $children)
	{
		# Recursively kill grandchildren first
		$grandchildren = Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $child.ProcessId }
		foreach ($gc in $grandchildren)
		{
			Stop-Process -Id $gc.ProcessId -Force -ErrorAction SilentlyContinue
		}
		Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue
	}
	Stop-Process -Id $hostPid -Force -ErrorAction SilentlyContinue
	Write-Host "Aspire host stopped."
}
else
{
	Write-Host "Aspire host process (PID $hostPid) is not running."
	Write-Host "=== aspire-stderr.log (last 30 lines) ==="
	Get-Content (Join-Path $repoRoot "aspire-stderr.log") -Tail 30 -ErrorAction SilentlyContinue
	Write-Host "=== aspire-stdout.log (last 30 lines) ==="
	Get-Content (Join-Path $repoRoot "aspire-stdout.log") -Tail 30 -ErrorAction SilentlyContinue
}

Remove-Item $pidFile -ErrorAction SilentlyContinue
