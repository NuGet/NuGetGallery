$Global:RepoRoot = (Convert-Path "$PsScriptRoot\..\..\..")
$Global:OpsRoot = (Convert-Path "$PsScriptRoot\..\..")

$GalOpsRoot = Join-Path $RepoRoot "src\galops"

# Find the Azure SDK
$SDKParent = "$env:ProgramFiles\Microsoft SDKs\Windows Azure\.NET SDK"
$Global:AzureSDKRoot = $null;
if(Test-Path $SDKParent) {
	# Pick the latest
	$AzureSDKRoot = (dir $SDKParent | sort Name -desc | select -first 1).FullName
}

if(!$AzureSDKRoot) {
	Write-Warning "Couldn't find the Azure SDK. Some commands may not work."
} else {
	Write-Host "Using Azure SDK at: $AzureSDKRoot"
}

$accounts = @(Get-AzureAccount)
if($accounts.Length -eq 0) {
	Write-Warning "No Azure Accounts found. Run Add-AzureAccount to configure your Azure account."
}

Write-Host -BackgroundColor Blue -ForegroundColor White @"
 _____     _____     _      _____ _____ _____ 
|   | |_ _|   __|___| |    |     |     |   __|
| | | | | |  |  |  | - |   |  |  |  |__|__   |
|_|___|___|_____|___|_|    |_____|__|  |_____|
                                              
"@
Write-Host -ForegroundColor Black -BackgroundColor Yellow "Welcome to the NuGet Operations Console (v$NuGetOpsVersion)"

if(!(Test-Path "$env:ProgramFiles\Microsoft SDKs\Windows Azure\.NET SDK\")) {
	Write-Warning "Couldn't find the Azure .NET SDK. Some operations may not work without it."
}

function Write-NuGetOpsPrompt() {
	$env = "<NONE>"
	if($NuOpsSession -and $NuOpsSession.CurrentEnvironment) { $env = $NuOpsSession.CurrentEnvironment.Name; }
	$host.UI.RawUI.WindowTitle = "NuGet Operations Console v$NuGetOpsVersion [Environment: $env]"

	Write-Host -noNewLine "$(Get-Location)"
	
	$realLASTEXITCODE = $LASTEXITCODE

	# Reset color, which can be messed up by Enable-GitColors
	$Host.UI.RawUI.ForegroundColor = $GitPromptSettings.DefaultForegroundColor
	
	Write-VcsStatus
	
	$global:LASTEXITCODE = $realLASTEXITCODE
	Write-Host
	Write-Host -noNewline "[env:"
	Write-Host -noNewLine -foregroundColor Magenta $env
	return "]> "
}
Export-ModuleMember -Function Write-NuGetOpsPrompt

function Reload-NuOpsSession() {
    $NuOps = $null;
    $NuOps = New-NuOpsSession
}
Export-ModuleMember Reload-NuOpsSession

Export-ModuleMember -Cmdlet *

$NuOps = New-NuOpsSession
Export-ModuleMember -Variable NuOps