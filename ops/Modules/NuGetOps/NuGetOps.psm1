$Global:RepoRoot = (Convert-Path "$PsScriptRoot\..\..\..")
$Global:OpsRoot = (Convert-Path "$PsScriptRoot\..\..")
$Global:NuGetOpsDefinition = $env:NUGET_OPS_DEFINITION

$GalOpsRoot = Join-Path $RepoRoot "src\galops"

$CurrentDeployment = $null
$CurrentEnvironment = $null
Export-ModuleMember -Variable CurrentDeployment, CurrentEnvironment

# Extract Ops NuGetOpsVersion
$NuGetOpsVersion = 
	cat (Join-Path $RepoRoot "src\CommonAssemblyInfo.cs") | 
	where { $_ -match "\[assembly:\s+AssemblyInformationalVersion\(`"(?<ver>[^`"]*)`"\)\]" } | 
	foreach { $matches["ver"] }

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

# Check for v0.2 level environment scripts
$Global:Environments = @{}
if($NuGetOpsDefinition -and (Test-Path $NuGetOpsDefinition)) {
	$EnvironmentsList = Join-Path $NuGetOpsDefinition "Environments.xml"
	if(Test-Path $EnvironmentsList) {
		$x = [xml](cat $EnvironmentsList)
		$Global:Environments = @{};
		$x.environments.environment | ForEach-Object {
			$Environments[$_.name] = New-Object PSCustomObject
			Add-Member -NotePropertyMembers @{
				Version = $NuGetOpsVersion;
				Name = $_.name;
				Protected = $_.protected -and ([String]::Equals($_.protected, "true", "OrdinalIgnoreCase"));
				Frontend = $_.frontend;
				Backend = $_.backend;
				Subscription = $_.subscription
				Type = $_.type
			} -InputObject $Environments[$_.name]
		}
	} else {
		Write-Warning "Environments list not found at $EnvironmentsList. No Environments will be available."
	}

	$SubscriptionsList = Join-Path $NuGetOpsDefinition "Subscriptions.xml"
	if(Test-Path $SubscriptionsList) {
		$x = [xml](cat $SubscriptionsList)
		$Global:Subscriptions = @{};
		$x.subscriptions.subscription | ForEach-Object {
			$Subscriptions[$_.name] = New-Object PSCustomObject
			Add-Member -NotePropertyMembers @{
				Version = $NuGetOpsVersion;
				Id = $_.id;
				Name = $_.name
			} -InputObject $Subscriptions[$_.name]
		}

		$Environments.Keys | foreach {
			$sub = $Environments[$_].Subscription
			if($Subscriptions[$sub] -ne $null) {
				$Environments[$_].Subscription = $Subscriptions[$sub];
			}
		}
	} else {
		Write-Warning "Subscriptions list not found at $SubscriptionsList. No Subscriptions will be available."
	}
}

try {
	if(@(Get-AzureSubscription).Length -eq 0) {
		Write-Warning "No Azure Subscriptions registered with the Azure Management Tools! Use the 'New-AzureManagementCertificate' function to generate a cert, and then the 'Enable-AzurePowerShell' script to configure it (both have '-?' help parameters if you need further info)"
	}
} catch {
	
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

$Global:GalOpsExe = join-path $GalOpsRoot "bin\Debug\galops.exe"
if(!(Test-Path $GalOpsExe)) {
	$answer = Read-Host "Gallery ops exe not built. Build it now? (Y/n)"
	if([String]::IsNullOrEmpty($answer) -or $answer.Equals("y", "OrdinalIgnoreCase") -or $answer.Equals("yes", "OrdinalIgnoreCase")) {
		pushd $RepoRoot
		Write-Host "Building GalOps.exe..."
		& msbuild /v:m | Out-Host
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



#Clear-Host
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