function Test-Symbols {
	param(
		[Parameter(Mandatory=$true)][string]$ReleaseShare,
		[string]$SymbolServer,
		[switch]$PublicOnly,
		[switch]$PrivateOnly
	)
	
	$path = $ReleaseShare
	if(!(Test-Path $path)) {
		$path = "\\nuget\Releases\$ReleaseShare"
	}
	if(!(Test-Path $path)) {
		throw "Could not find release share $ReleaseShare. Checked $ReleaseShare, $path";
	}
	
	if(![String]::IsNullOrEmpty($SymbolServer)) {
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
	else {
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
		
		if(!$PrivateOnly) {
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