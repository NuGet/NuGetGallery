#$oldSize = $host.UI.RawUI.BufferSize;
#$oldSize.Width = 120;
#$oldSize.Height = 3000;
#$host.UI.RawUI.BufferSize = $oldSize;
#$oldSize.Height = 50;
#$host.UI.RawUI.WindowSize = $oldSize;

$root = (Split-Path -Parent $MyInvocation.MyCommand.Path)
$OpsProfile = $MyInvocation.MyCommand.Path
$OpsModules = Join-Path $root "Modules"
$OpsTools = Join-Path $root "Tools"
$env:PSModulePath = "$env:PSModulePath;$OpsModules"

$env:WinSDKRoot = "$(cat "env:\ProgramFiles(x86)")\Windows Kits\8.0"

$env:PATH = "$root;$OpsTools\bin;$env:PATH;$env:WinSDKRoot\bin\x86;$env:WinSDKRoot\Debuggers\x86"

Import-Module PS-CmdInterop
Import-Module PS-VsVars

Import-VsVars -Architecture x86

if(Test-Path "$OpsTools\Paths.txt") {
	cat "$OpsTools\Paths.txt" | ForEach {
		$env:PATH = "$($env:PATH);$OpsTools\$_"
	}
}

Import-Module posh-git
Import-Module WAPPSCmdlets
Import-Module NuGetOps

function prompt() {
	$env = Get-Environment;
	if($env -eq $null) { $env = "<NONE>"; }
	$host.UI.RawUI.WindowTitle = "NuGet Operations Console v$NuGetOpsVersion [Environment: $env]"

	Write-Host -noNewLine "$(Get-Location)"
	
	$realLASTEXITCODE = $LASTEXITCODE

	# Reset color, which can be messed up by Enable-GitColors
	$Host.UI.RawUI.ForegroundColor = $GitPromptSettings.DefaultForegroundColor
	
	Write-Host -noNewline " branch:"
	Write-VcsStatus
	
	$global:LASTEXITCODE = $realLASTEXITCODE
	Write-Host
	Write-Host -noNewline "[env:"
	if(Test-Environment "Production") {
		Write-Host -noNewLine -foregroundColor Yellow $env
	} else {
		Write-Host -noNewLine -foregroundColor Magenta $env
	}
	return "]> "
}