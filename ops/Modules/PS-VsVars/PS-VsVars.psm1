function Import-VsVars {
	param(
		[Parameter(Mandatory=$false)][string]$VsVersion = $null,
		[Parameter(Mandatory=$false)][string]$VsVarsPath = $null,
		[Parameter(Mandatory=$false)][string]$Architecture = $env:PROCESSOR_ARCHITECTURE
	)
	
	$SearchPath = "Software\Microsoft\VisualStudio"
	if($env:PROCESSOR_ARCHITECTURE -eq "AMD64") {
		$SearchPath = "Software\Wow6432Node\Microsoft\VisualStudio"
	}

	if([String]::IsNullOrEmpty($VsVarsPath)) {
		Write-Debug "Finding vcvarsall.bat automatically..."
		
		if([String]::IsNullOrEmpty($VsVersion)) {
			Write-Debug "Finding most recent Visual Studio version..."
			11..1 |
				Where-Object { 
					(Test-Path "HKLM:\$SearchPath\$_.0") -and
					![String]::IsNullOrEmpty((Get-ItemProperty "HKLM:\$SearchPath\$_.0").InstallDir)
				} | 
				ForEach-Object {
					Write-Host "Found Visual Studio $_.0"
					$_
				} |
				Select-Object -Index 0  |
				ForEach-Object {
					$regPath = "HKLM:\$SearchPath\$_.0"
					Write-Debug "Checking $regPath"
					if(Test-Path $regPath) {
						Write-Debug "Found VS $_.0"
						$VsVersion = "$_.0"
					} else {
						Write-Debug "VS $_.0 not installed"
					}
				}
		}
		
		if(![String]::IsNullOrEmpty($VsVersion)) {
			$VsRoot = (Get-ItemProperty "HKLM:\$SearchPath\$VsVersion").InstallDir
			Write-Debug "Found VS $VsVersion in $VsRoot"
			$VsVarsPath = Convert-Path "$VsRoot\..\..\VC\vcvarsall.bat"
		} else {
			"No Visual Studio Environments found"
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

function Invoke-VisualStudio {
	param([string]$Solution)
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
	devenv $slns[0];
}
Set-Alias -Name vs -Value Invoke-VisualStudio
Export-ModuleMember -Function Invoke-VisualStudio -Alias vs