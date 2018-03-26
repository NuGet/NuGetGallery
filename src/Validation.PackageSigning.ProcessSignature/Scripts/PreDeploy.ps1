. .\Functions.ps1

$jobsToInstall = $OctopusParameters["Jobs.ServiceNames"].Split("{,}")

Write-Host Removing services...

$jobsToInstall.Split("{;}") | %{
	Uninstall-NuGetService -ServiceName $_
}

Write-Host Removed services.