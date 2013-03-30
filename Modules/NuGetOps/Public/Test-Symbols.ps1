<#
.SYNOPSIS
Tests that the NuGet Symbols exist on the specified path

.PARAMETER ReleaseShare
A share containing the NuGet Binaries

.PARAMETER SymbolServer
A specific symbol server to test

.PARAMETER PublicOnly
Test only the public Microsoft symbol server

.PARAMETER InternalOnly
Test only the internal Microsoft symbol server
#>
function Test-Symbols {
	param(
		[Parameter(Mandatory=$true)][string]$ReleaseShare,
		[Parameter(ParameterSetName="SpecificServer")][string]$SymbolServer,
		[Parameter(ParameterSetName="MicrosoftServers")][switch]$PublicOnly,
		[Parameter(ParameterSetName="MicrosoftServers")][switch]$InternalOnly
	)
	
	$path = $ReleaseShare
	if(!(Test-Path $path)) {
		$path = "\\nuget\Releases\$ReleaseShare"
	}
	if(!(Test-Path $path)) {
		throw "Could not find release share $ReleaseShare. Checked $ReleaseShare, $path";
	}
	
	if($PsCmdlet.ParameterSetName -eq "SpecificServer") {
		Write-Host -Foreground Black -Background Yellow "*********************************************"
		Write-Host -Foreground Black -Background Yellow "Testing Custom Symbol Server: $SymbolServer"
		Write-Host -Foreground Black -Background Yellow "*********************************************"
		symchk /s $SymbolServer /r $path /op
		if($lastexitcode -ne 0) { 
			Write-Host -Foreground White -Background Red "****************************"
			Write-Host -Foreground White -Background Red "Some Symbols were not found."
			Write-Host -Foreground White -Background Red "****************************"
		} else {
			Write-Host -Foreground Black -Background Green "***********************"
			Write-Host -Foreground Black -Background Green "All symbols were found."
			Write-Host -Foreground Black -Background Green "***********************"				
		}
	}
	elseif($PsCmdlet.ParameterSetName -eq "MicrosoftServers") {
		if(!$PublicOnly) {
			Write-Host -Foreground Black -Background Yellow "*********************************************"
			Write-Host -Foreground Black -Background Yellow "Testing Internal Symbol Server: http://symweb"
			Write-Host -Foreground Black -Background Yellow "*********************************************"
			symchk /s http://symweb /r $path /op
			if($lastexitcode -ne 0) { 
				Write-Host -Foreground White -Background Red "****************************"
				Write-Host -Foreground White -Background Red "Some Symbols were not found."
				Write-Host -Foreground White -Background Red "****************************"
			} else {
				Write-Host -Foreground Black -Background Green "***********************"
				Write-Host -Foreground Black -Background Green "All symbols were found."
				Write-Host -Foreground Black -Background Green "***********************"				
			}
		}
		
		if(!$InternalOnly) {
			Write-Host -Foreground Black -Background Yellow "************************************************************************"
			Write-Host -Foreground Black -Background Yellow "Testing Public Symbol Server: http://msdl.microsoft.com/download/symbols"
			Write-Host -Foreground Black -Background Yellow "************************************************************************"
			symchk /s http://msdl.microsoft.com/download/symbols /r $path /op
			if($lastexitcode -ne 0) { 
				Write-Host -Foreground White -Background Red "****************************"
				Write-Host -Foreground White -Background Red "Some Symbols were not found."
				Write-Host -Foreground White -Background Red "****************************"
			} else {
				Write-Host -Foreground Black -Background Green "***********************"
				Write-Host -Foreground Black -Background Green "All symbols were found."
				Write-Host -Foreground Black -Background Green "***********************"				
			}
		}
	}
}