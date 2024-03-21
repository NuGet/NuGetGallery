Function Uninstall-NuGetService() {
	Param ([string]$ServiceName)

	if (Get-Service $ServiceName -ErrorAction SilentlyContinue)
	{
		Write-Host Removing service $ServiceName...
		Stop-Service $ServiceName -Force
		sc.exe delete $ServiceName 
		Write-Host Removed service $ServiceName.
	} else {
		Write-Host Skipping removal of service $ServiceName - no such service exists.
	}
}

Function Install-NuGetService() {
	Param (
		[string]$ServiceName,
		[string]$ServiceTitle,
		[string]$ScriptToRun
	)

	Write-Host Installing service $ServiceName...

	& .\nssm.exe install $ServiceName $ScriptToRun
	
	Set-Service -Name $ServiceName -DisplayName "$ServiceTitle - $ServiceName" -Description "Runs $ServiceTitle." -StartupType Automatic
	sc.exe failure $ServiceName reset= 30 actions= restart/5000

	# Run service
	net start $ServiceName
		
	Write-Host Installed service $ServiceName.
}
