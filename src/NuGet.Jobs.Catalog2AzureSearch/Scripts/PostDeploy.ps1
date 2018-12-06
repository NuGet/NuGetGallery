. .\Functions.ps1

$jobsToInstall = $OctopusParameters["Jobs.ServiceNames"].Split("{,}")

Write-Host Installing services...

$currentDirectory = [string](Get-Location)

$jobsToInstall.Split("{;}") | %{
	$serviceName = $_
	$serviceTitle = $OctopusParameters["Jobs.$serviceName.Title"]
	$scriptToRun = $OctopusParameters["Jobs.$serviceName.Script"]
	$scriptToRun = "$currentDirectory\$scriptToRun"

	Install-NuGetService -ServiceName $serviceName -ServiceTitle $serviceTitle -ScriptToRun $scriptToRun
}

Write-Host Installed services.