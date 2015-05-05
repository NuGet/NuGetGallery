. .\Functions.ps1

$serviceNameC2R = $OctopusParameters["Jobs.Catalog2Registration.Service.Name"]
$serviceNameC2L = $OctopusParameters["Jobs.Catalog2Lucene.Service.Name"]
$serviceNameC2D = $OctopusParameters["Jobs.Catalog2Dnx.Service.Name"]

# Stop and remove services

Write-Host Removing services...

Uninstall-NuGetService($serviceNameC2R)
Uninstall-NuGetService($serviceNameC2L)
Uninstall-NuGetService($serviceNameC2D)

Write-Host Removed services.
