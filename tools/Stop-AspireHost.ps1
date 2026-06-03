# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

<#
.SYNOPSIS
    Stops the Aspire AppHost process started by Start-AspireHost.ps1.

.PARAMETER HostPid
    The process ID of the Aspire AppHost to stop.
#>
param(
	[Parameter(Mandatory = $true)]
	[int]$HostPid
)

$proc = Get-Process -Id $HostPid -ErrorAction SilentlyContinue

if ($proc)
{
	Write-Host "Stopping Aspire host (PID $HostPid)..."
	Stop-Process -Id $HostPid -Force -ErrorAction SilentlyContinue
	Write-Host "Aspire host stopped."
}
else
{
	Write-Host "Aspire host process (PID $HostPid) is not running."
}
