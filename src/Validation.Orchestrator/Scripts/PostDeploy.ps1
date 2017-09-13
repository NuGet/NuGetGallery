. .\Functions.ps1

$jobsToInstall = $OctopusParameters["Jobs.ServiceNames"].Split("{,}")

Write-Host Installing services...

$currentDirectory = [string](Get-Location)

$jobsToInstall.Split("{;}") | %{
	$serviceName = $_
	$serviceTitle = $OctopusParameters["Jobs.$serviceName.Title"]
	$serviceUsername = $OctopusParameters["Jobs.$serviceName.Username"]
	$servicePassword = $OctopusParameters["Jobs.$serviceName.Password"]
	$scriptToRun = $OctopusParameters["Jobs.$serviceName.Script"]
	$scriptToRun = "$currentDirectory\$scriptToRun"

	Install-NuGetService -ServiceName $serviceName -ServiceTitle $serviceTitle -ScriptToRun $scriptToRun -Username $serviceUsername -Password $servicePassword
}

Write-Host Installed services.