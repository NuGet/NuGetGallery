<#
.SYNOPSIS
Enters the NuGet Operations Console
#>

function Get-AvailableModule($name) {
	(Get-Module -ListAvailable $name | 
		Sort -Desc Version | 
		Select -First 1)
}

$MsftDomainNames = @("REDMOND","FAREAST","NORTHAMERICA","NTDEV")

$OpsProfile = $MyInvocation.MyCommand.Path
$root = (Split-Path -Parent (Split-Path -Parent $OpsProfile))
$OpsModules = Join-Path $root "Modules"
$env:PSModulePath = "$env:PSModulePath;$OpsModules"

if($EnvironmentList) {
	$env:NUGET_OPS_DEFINITION = $EnvironmentList;
}

if(!$env:NUGET_OPS_DEFINITION) {
	$msftNuGetShare = "\\nuget\nuget\Share\Environments"
	# Defaults for Microsoft CorpNet. If you're outside CorpNet, you'll have to VPN in. Of course, if you're hosting your own gallery, you have to build your own scripts :P
	if([Environment]::UserDomainName -and ($MsftDomainNames -contains [Environment]::UserDomainName) -and (Test-Path $msftNuGetShare)) {
		$env:NUGET_OPS_DEFINITION = $msftNuGetShare
	}
	else {
		Write-Warning "NUGET_OPS_DEFINITION is not set. Set it to a path containing an Environments.xml and a Subscriptions.xml file"
	}
}

$env:WinSDKRoot = "$(cat "env:\ProgramFiles(x86)")\Windows Kits\8.0"

$env:PATH = "$root;$env:PATH;$env:WinSDKRoot\bin\x86;$env:WinSDKRoot\Debuggers\x86"

function LoadOrReloadModule($name) {
	if(Get-Module $name) {
		Write-Host "Module $name already loaded, reloading."
		Remove-Module $name -Force
	}
	Import-Module $name
}

LoadOrReloadModule PS-CmdInterop
LoadOrReloadModule PS-VsVars

Import-VsVars -Architecture x86

if(!(Get-Module posh-git)) {
	if((Get-AvailableModule posh-git) -eq $null) {
		Write-Warning "Posh-Git not found, it is recommended you install it!"
	} else {
		Import-Module posh-git
	}
} else {
	Write-Host "Module posh-git already loaded, can't reload"
}

$azMod = Get-AvailableModule Azure
if(($azMod -eq $null) -or ($azMod.Version -lt (New-Object Version "0.7.0"))) {
	throw "NuGet Operations requires Azure PowerShell 0.7 or higher!"
}

LoadOrReloadModule Azure
LoadOrReloadModule NuGetOps

$Global:_OldPrompt = $function:prompt;
function Global:prompt {
	if(Get-Module NuGetOps) {
		return Write-NuGetOpsPrompt
	} else {
		return $oldprompt.InvokeReturnAsIs()
	}
}