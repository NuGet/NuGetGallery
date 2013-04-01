$Global:OpsRoot = (Convert-Path "$PsScriptRoot\..\..")
$Global:EnvironmentsList = $env:NUGET_OPS_ENVIRONMENTS

$CurrentDeployment = $null
$CurrentEnvironment = $null
Export-ModuleMember -Variable CurrentDeployment, CurrentEnvironment

# Extract Ops NuGetOpsVersion
$NuGetOpsVersion = 
	cat .\Source\CommonAssemblyInfo.cs | 
	where { $_ -match "\[assembly:\s+AssemblyInformationalVersion\(`"(?<ver>[^`"]*)`"\)\]" } | 
	foreach { $matches["ver"] }

# Check for v0.2 level environment scripts
$Global:Environments = @{}
if($EnvironmentsList -and (Test-Path $EnvironmentsList)) {
	if([IO.Path]::GetExtension($EnvironmentsList) -eq ".xml") {
		$x = [xml](cat $EnvironmentsList)
		$Global:Environments = @{};
		$x.environments.environment | ForEach-Object {
			$Environments[$_.name] = New-Object PSCustomObject
			Add-Member -NotePropertyMembers @{
				Version = 0.2;
				Name = $_.name;
				Protected = $_.protected -and ([String]::Equals($_.protected, "true", "OrdinalIgnoreCase"));
				Service = $_.service;
				Worker = $_.worker;
				Subscription = $_.subscription
			} -InputObject $Environments[$_.name]
		}
	} else {
		throw "Your Environments are old and busted. Upgrade to the new hotness!`r`nhttps://github.com/NuGet/NuGetOperations/wiki/Setting-up-the-Operations-Console"
	}
}

function Get-Environment([switch]$ListAvailable) {
	if($ListAvailable) {
		@($Environments.Keys | ForEach-Object { 
			if(Test-Environment $_) {
				"* $_"
			} else {
				"  $_"
			}
		})
	} else {
		if(!$CurrentEnvironment) {
			$null;
		} else {
			$CurrentEnvironment.Name
		}
	}
}
Export-ModuleMember -Function Get-Environment

function Test-Environment([Parameter(Mandatory=$true)][String]$Environment, [Switch]$Exists) {
	if($Exists) {
		return $Environments.ContainsKey($Environment)
	} else {
		[String]::Equals((Get-Environment), $Environment, "OrdinalIgnoreCase");
	}
}
Export-ModuleMember -Function Test-Environment

function _IsProduction {
	$CurrentEnvironment -and ($CurrentEnvironment.Protected -eq "true")
}

function _RefreshGitColors {
	$global:GitPromptSettings = New-Object PSObject -Property @{
	    DefaultForegroundColor    = $Host.UI.RawUI.ForegroundColor
	
	    BeforeText                = ' ['
	    BeforeForegroundColor     = [ConsoleColor]::Yellow
	    BeforeBackgroundColor     = $Host.UI.RawUI.BackgroundColor    
	    DelimText                 = ' |'
	    DelimForegroundColor      = [ConsoleColor]::Yellow
	    DelimBackgroundColor      = $Host.UI.RawUI.BackgroundColor
	    
	    AfterText                 = ']'
	    AfterForegroundColor      = [ConsoleColor]::Yellow
	    AfterBackgroundColor      = $Host.UI.RawUI.BackgroundColor
	    
	    BranchForegroundColor       = [ConsoleColor]::Cyan
	    BranchBackgroundColor       = $Host.UI.RawUI.BackgroundColor
	    BranchAheadForegroundColor  = [ConsoleColor]::Green
	    BranchAheadBackgroundColor  = $Host.UI.RawUI.BackgroundColor
	    BranchBehindForegroundColor = [ConsoleColor]::Red
	    BranchBehindBackgroundColor = $Host.UI.RawUI.BackgroundColor
	    BranchBehindAndAheadForegroundColor = [ConsoleColor]::Yellow
	    BranchBehindAndAheadBackgroundColor = $Host.UI.RawUI.BackgroundColor
	    
	    BeforeIndexText           = ""
	    BeforeIndexForegroundColor= [ConsoleColor]::DarkGreen
	    BeforeIndexBackgroundColor= $Host.UI.RawUI.BackgroundColor
	    
	    IndexForegroundColor      = [ConsoleColor]::DarkGreen
	    IndexBackgroundColor      = $Host.UI.RawUI.BackgroundColor
	    
	    WorkingForegroundColor    = [ConsoleColor]::DarkRed
	    WorkingBackgroundColor    = $Host.UI.RawUI.BackgroundColor
	    
	    UntrackedText             = ' !'
	    UntrackedForegroundColor  = [ConsoleColor]::DarkRed
	    UntrackedBackgroundColor  = $Host.UI.RawUI.BackgroundColor
	    
	    ShowStatusWhenZero        = $true
	    
	    AutoRefreshIndex          = $true
	
	    EnablePromptStatus        = !$GitMissing
	    EnableFileStatus          = $true
	    RepositoriesInWhichToDisableFileStatus = @( ) # Array of repository paths
	
	    Debug                     = $false
	}
	if(_IsProduction) {
		$GitPromptSettings.WorkingForegroundColor = [ConsoleColor]::Yellow
		$GitPromptSettings.UntrackedForegroundColor = [ConsoleColor]::Yellow
	    	$GitPromptSettings.IndexForegroundColor = [ConsoleColor]::Cyan
	}
}

function env([string]$Name) {
	if([String]::IsNullOrEmpty($Name)) {
		Get-Environment -ListAvailable
	} else {
		Set-Environment -Name $Name
	}
}
Export-ModuleMember -Function env

$galopsExe = join-path $OpsRoot "Source\NuGetGallery.Operations.Tools\bin\Debug\galops.exe"
if(!(Test-Path $galopsExe)) {
	$answer = Read-Host "Gallery ops exe not built. Build it now? (Y/n)"
	if([String]::IsNullOrEmpty($answer) -or $answer.Equals("y", "OrdinalIgnoreCase") -or $answer.Equals("yes", "OrdinalIgnoreCase")) {
		pushd $OpsRoot
		Write-Host "Building GalOps.exe..."
		& "$OpsRoot\Scripts\Restore-Packages.ps1"
		& msbuild NuGetOperations.sln /v:m | Out-Host
		popd
	} else {
		Write-Host -Background Yellow -Foreground Black "Warning: Do not execute gallery ops tasks until you have built the GalOps.exe executable"
	}
}

# Load Private Functions
dir $PsScriptRoot\Private\*.ps1 | foreach {
	. $_
}

# Load Public Functions
dir $PsScriptRoot\Public\*.ps1 | foreach {
	. $_
	Export-ModuleMember -Function "$([IO.Path]::GetFileNameWithoutExtension($_.Name))"
}



Clear-Host
Write-Host -BackgroundColor Blue -ForegroundColor White @"
 _____     _____     _      _____ _____ _____ 
|   | |_ _|   __|___| |    |     |     |   __|
| | | | | |  |  |  | - |   |  |  |  |__|__   |
|_|___|___|_____|___|_|    |_____|__|  |_____|
                                              
"@
Write-Host -ForegroundColor Black -BackgroundColor Yellow "Welcome to the NuGet Operations Console (v$NuGetOpsVersion)"

if($Environments.Count -eq 0) {
	Write-Warning "No environments are available, the console will not function correctly.`r`nSee https://github.com/NuGet/NuGetOperations/wiki/Setting-up-the-Operations-Console for more info"
}
if(!(Test-Path "$env:ProgramFiles\Microsoft SDKs\Windows Azure\.NET SDK\")) {
	Write-Warning "Couldn't find the Azure .NET SDK. Some operations may not work without it."
}

function Write-NuGetOpsPrompt() {
	$envName = "<NONE>"
	if($CurrentEnvironment) { $env = $CurrentEnvironment.Name; }
	$host.UI.RawUI.WindowTitle = "NuGet Operations Console v$NuGetOpsVersion [Environment: $env]"

	Write-Host -noNewLine "$(Get-Location)"
	
	$realLASTEXITCODE = $LASTEXITCODE

	# Reset color, which can be messed up by Enable-GitColors
	$Host.UI.RawUI.ForegroundColor = $GitPromptSettings.DefaultForegroundColor
	
	Write-VcsStatus
	
	$global:LASTEXITCODE = $realLASTEXITCODE
	Write-Host
	Write-Host -noNewline "[env:"
	if(_IsProduction) {
		Write-Host -noNewLine -foregroundColor Yellow $env
	} else {
		Write-Host -noNewLine -foregroundColor Magenta $env
	}
	return "]> "
}
Export-ModuleMember -Function Write-NuGetOpsPrompt