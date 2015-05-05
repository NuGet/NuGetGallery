. .\Functions.ps1

$serviceNameC2R = $OctopusParameters["Jobs.Catalog2Registration.Service.Name"]
$serviceNameC2L = $OctopusParameters["Jobs.Catalog2Lucene.Service.Name"]
$serviceNameC2D = $OctopusParameters["Jobs.Catalog2Dnx.Service.Name"]

$titleC2R = $OctopusParameters["Jobs.Catalog2Registration.Title"]
$titleC2L = $OctopusParameters["Jobs.Catalog2Lucene.Title"]
$titleC2D = $OctopusParameters["Jobs.Catalog2Dnx.Title"]

$currentDirectory = [string](Get-Location)

# Install services	
Install-NuGetService -ServiceName $serviceNameC2R -ServiceTitle $titleC2R -ScriptToRun "$currentDirectory\Catalog2RegistrationV3.cmd"
Install-NuGetService -ServiceName $serviceNameC2L -ServiceTitle $titleC2L -ScriptToRun "$currentDirectory\Catalog2LuceneV3.cmd"
Install-NuGetService -ServiceName $serviceNameC2D -ServiceTitle $titleC2D -ScriptToRun "$currentDirectory\Catalog2DnxV3.cmd"
