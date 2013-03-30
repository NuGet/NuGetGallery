$Global:OpsRoot = (Convert-Path "$PsScriptRoot\..\..")
$EnvsRoot = $env:NUGET_OPS_ENVIRONMENTS

# Extract Ops NuGetOpsVersion
$NuGetOpsVersion = 
	cat .\Source\CommonAssemblyInfo.cs | 
	where { $_ -match "\[assembly:\s+AssemblyInformationalVersion\(`"(?<ver>[^`"]*)`"\)\]" } | 
	foreach { $matches["ver"] }

# Defaults for Microsoft CorpNet. If you're outside CorpNet, you'll have to VPN in. Of course, if you're hosting your own gallery, you have to build your own scripts :P
if([String]::IsNullOrEmpty($EnvsRoot) -and (Test-Path "\\nuget\Environments\Environments.xml")) {
	$EnvsRoot = "\\nuget\Environments\Environments.xml"
}
$emulatorOnly = [String]::IsNullOrEmpty($EnvsRoot);

# Check for v0.2 level environment scripts
$Global:Environments = @{}
if($EnvsRoot -and (Test-Path $EnvsRoot)) {
	if([IO.Path]::GetExtension($EnvsRoot) -eq ".xml") {
		$x = [xml](cat $EnvsRoot)
		$Global:Environments = @{};
		$x.environments.environment | ForEach-Object {
			$Environments[$_.name] = New-Object PSCustomObject
			Add-Member -NotePropertyMembers @{
				Version = 0.2;
				Service = $_.service;
				Subscription = $_.subscription
			} -InputObject $Environments[$_.name]
		}
	} else {
		# Old v0.1 environment scripts
		dir "$EnvsRoot\*.ps1" | Where-Object { !$_.Name.StartsWith("_") } | ForEach-Object {
			$name = [IO.Path]::GetFileNameWithoutExtension($_.FullName);
			$Environments[$name] = New-Object PSCustomObject
			Add-Member -NotePropertyMembers @{
				Version = 0.1;
				Script = $_.FullName
			} -InputObject $Environments[$name]
		}
	}
}

function Get-Environment([switch]$ListAvailable) {
	if($ListAvailable) {
		$emulator = "  Emulator"
		if(Test-Environment "Emulator") {
			$Emulator = "* Emulator"
		}
		@(dir "$EnvsRoot\*.ps1" | Where-Object { !$_.Name.StartsWith("_") } | ForEach-Object { 
			$envName = [IO.Path]::GetFileNameWithoutExtension($_.Name) 
			if(Test-Environment $envName) {
				"* $envName"
			} else {
				"  $envName"
			}
		}) + $Emulator
	} else {
		if(!(Test-Path env:\NUGET_GALLERY_ENV)) {
			$null;
		} else {
			$env:NUGET_GALLERY_ENV
		}
	}
}
Export-ModuleMember -Function Get-Environment

function Test-Environment([Parameter(Mandatory=$true)][String]$Environment, [Switch]$Exists) {
	if($Exists) {
		if($Environment.Equals("Emulator", "OrdinalIgnoreCase")) {
			$true;
		} elseif([String]::IsNullOrEmpty($EnvsRoot)) {
			$false;
		} else {
			Test-Path (Join-Path $EnvsRoot "$Environment.ps1")
		}
	} else {
		[String]::Equals((Get-Environment), $Environment, "OrdinalIgnoreCase");
	}
}
Export-ModuleMember -Function Test-Environment

function _IsProduction {
	Test-Environment "Production"	
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

function _SetEmulatorEnvironment() {
	del env:\NUGET*
	$env:NUGET_GALLERY_USE_EMULATOR = $true
	$env:NUGET_GALLERY_ENV = "Emulator"
}

function Set-Environment {
	param([Parameter(Mandatory=$true)][string]$Name)
	$env = Get-Environment
	if($env -ne $Name) {
		if([String]::IsNullOrEmpty($Name)) {
			del env:\NUGET_*;
			return;
		}
		
		if([String]::IsNullOrEmpty($EnvsRoot) -or !(Test-Path (Join-Path $EnvsRoot "$Name*.ps1"))) {
			if("Emulator" -like "$Name*") {
				_SetEmulatorEnvironment
				return;
			}
			throw "No such environment: $Name";
		}


		. "$EnvsRoot\$Name*.ps1"
		if(_IsProduction) {
			$host.UI.RawUI.BackgroundColor = "DarkRed";
		} elseif(![String]::IsNullOrEmpty($Name)) {
			$host.UI.RawUI.BackgroundColor = "Black";
		} else {
			$host.UI.RawUI.BackgroundColor = "DarkMagenta";
		}
		_RefreshGitColors
		#cls;
		"Environment is now $(Get-Environment)"
	}
}
Export-ModuleMember -Function Set-Environment

function Write-DeploymentSettings {
	dir env:\NUGET_* | ForEach {
		"$($_.Name) = $($_.Value)"
	}
}
Set-Alias -Name settings -Value Write-DeploymentSettings
Export-ModuleMember -Function Write-DeploymentSettings -Alias settings

function env([string]$Name) {
	if([String]::IsNUllOrEmpty($Name)) {
		Get-Environment -ListAvailable
	} else {
		Set-Environment $Name
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

if(Test-Environment -Exists Preview) {
	Set-Environment Preview | Out-Null
} else {
	Set-Environment Emulator | Out-Null
}
Clear-Host
Write-Host @"
 ______         ______            
|  ___ \       / _____)      _    
| |   | |_   _| /  ___  ____| |_  
| |   | | | | | | (___)/ _  )  _) 
| |   | | |_| | \____/( (/ /| |__ 
|_|   |_|\____|\_____/ \____)\___)
"@
Write-Host -ForegroundColor Black -BackgroundColor Yellow "Welcome to the NuGet Operations Console (v$NuGetOpsVersion)"

if($EmulatorOnly) {
	Write-Warning "NUGET_OPS_ENVIRONMENTS is not specified, only the built-in Emulator environment will be available"
}
if(!(Test-Path "$env:ProgramFiles\Microsoft SDKs\Windows Azure\.NET SDK\")) {
	Write-Warning "Couldn't find the Azure .NET SDK. Some operations may not work without it."
}