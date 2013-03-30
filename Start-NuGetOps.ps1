$root = (Split-Path -Parent $MyInvocation.MyCommand.Path)
$OpsProfile = $MyInvocation.MyCommand.Path
$OpsModules = Join-Path $root "Modules"
$OpsTools = Join-Path $root "Tools"
$env:PSModulePath = "$env:PSModulePath;$OpsModules"

$env:WinSDKRoot = "$(cat "env:\ProgramFiles(x86)")\Windows Kits\8.0"

$env:PATH = "$root;$OpsTools\bin;$env:PATH;$env:WinSDKRoot\bin\x86;$env:WinSDKRoot\Debuggers\x86"

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

if(Test-Path "$OpsTools\Paths.txt") {
	cat "$OpsTools\Paths.txt" | ForEach {
		$env:PATH = "$($env:PATH);$OpsTools\$_"
	}
}

if(!(Get-Module posh-git)) {
	Import-Module posh-git
} else {
	Write-Host "Module posh-git already loaded, can't reload"
}
LoadOrReloadModule WAPPSCmdlets
LoadOrReloadModule NuGetOps

$oldprompt = $function:prompt;
function Global:prompt {
	if(Get-Module NuGetOps) {
		return Write-NuGetOpsPrompt
	} else {
		return $oldprompt.InvokeReturnAsIs()
	}
}