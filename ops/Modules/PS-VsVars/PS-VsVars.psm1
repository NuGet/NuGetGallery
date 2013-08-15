$VisualStudioVersions = @{}

$SearchPath = "Software\Microsoft\VisualStudio"
if($env:PROCESSOR_ARCHITECTURE -eq "AMD64") {
	$SearchPath = "Software\Wow6432Node\Microsoft\VisualStudio"
}

Get-ChildItem "HKLM:\$SearchPath" | 
	Where-Object { 
		($_.Name -match "\d+\.\d+") -and
		(![String]::IsNullOrEmpty((Get-ItemProperty "HKLM:\$SearchPath\$($_.PSChildName)").InstallDir)) 
	} | ForEach-Object {
		$regPath = "HKLM:\$SearchPath\$($_.PSChildName)"
		
		# Gather VS data
		$installDir = (Get-ItemProperty $regPath).InstallDir

		$vsVars = $null;
		if(Test-Path "$installDir\..\..\VC\vcvarsall.bat") {
			$vsVars = Convert-Path "$installDir\..\..\VC\vcvarsall.bat"
		}
		$devenv = $null;
		if(Test-Path "$installDir\devenv.exe") {
			$devenv = Convert-Path "$installDir\devenv.exe"
		}
		
		# Make a VSInfo object
		$vsInfo = New-Object PSCustomObject
		Add-Member -InputObject $vsInfo -NotePropertyMembers @{
			"Version" = $_.PSChildName;
			"RegistryRoot" = $_;
			"InstallDir" = $installDir;
			"VsVarsPath" = $vsVars;
			"DevEnv" = $devenv;
		}

		# Add it to the dictionary
		$VisualStudioVersions[$_.PSChildName] = $vsInfo
	}

$latestVerWithVars = $VisualStudioVersions.Keys | sort -desc | where { $VisualStudioVersions[$_].VsVarsPath -ne $null } | select -first 1
$LatestVisualStudioVersion = $VisualStudioVersions[$latestVerWithVars]
Export-ModuleMember -Variable $VisualStudioVersions,$LatestVisualStudioVersion

function Import-VsVars {
	param(
		[Parameter(Mandatory=$false)][string]$VsVersion = $null,
		[Parameter(Mandatory=$false)][string]$VsVarsPath = $null,
		[Parameter(Mandatory=$false)][string]$Architecture = $env:PROCESSOR_ARCHITECTURE
	)
	
	if([String]::IsNullOrEmpty($VsVarsPath)) {
		Write-Debug "Finding vcvarsall.bat automatically..."
		
		if([String]::IsNullOrEmpty($VsVersion)) {
			Write-Debug "Finding most recent Visual Studio version..."
			$VsVersion = $LatestVisualStudioVersion.Version
		}

		if([String]::IsNullOrEmpty($VsVersion)) {
			"No Visual Studio Environments found"
		} else {
			$Vs = $VisualStudioVersions[$VsVersion]
			Write-Debug "Found VS $($Vs.Version) in $($Vs.InstallDir)"
			$VsVarsPath = $Vs.VsVarsPath
		}
	}
	if(![String]::IsNullOrEmpty($VsVarsPath) -and (Test-Path $VsVarsPath)) {
		# Run the cmd script
		Write-Debug "Invoking: `"$VsVarsPath`" $Architecture"
		Invoke-CmdScript "$VsVarsPath" $Architecture
		"Imported Visual Studio $VsVersion Environment into current shell"
	}
}
Export-ModuleMember -Function Import-VsVars

function Get-DevEnv {
	param(
		[Parameter(Mandatory=$false, Position=1)][string]$Version)
	$Vs = $LatestVisualStudioVersion;
	if($Version) {
		$Vs = $VisualStudioVersions[$Version]
	}
	if(!$Vs) {
		if($Version) {
			throw "Could not find visual studio $Version!"
		} else {
			throw "Could not find any visual studio version!"
		}
	}
	$Vs.DevEnv
}
Export-ModuleMember -Function Get-DevEnv

function Invoke-VisualStudio {
	param(
		[Parameter(Mandatory=$false, Position=0)][string]$Solution,
		[Parameter(Mandatory=$false, Position=1)][string]$Version)

	if([String]::IsNullOrEmpty($Solution)) {
		$Solution = "*.sln"
	}
	elseif(!$Solution.EndsWith(".sln")) {
		$Solution = $Solution + "*.sln";
	}

	if(!(Test-Path $Solution)) {
		throw "Could not find any matches for: $Solution"
	}
	$slns = @(dir $Solution)
	if($slns.Length -gt 1) {
		$names = [String]::Join(",", @($slns | foreach { $_.Name }))
		throw "Ambiguous matches for $($Solution): $names";
	}

	$devenv = Get-DevEnv -Version $Version
	&$devenv $slns[0];
}
Set-Alias -Name vs -Value Invoke-VisualStudio
Export-ModuleMember -Function Invoke-VisualStudio -Alias vs