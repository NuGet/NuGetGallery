$serviceNameC2R = $OctopusParameters["Jobs.Catalog2Registration.Service.Name"]
$serviceNameC2L = $OctopusParameters["Jobs.Catalog2Lucene.Service.Name"]
$serviceNameC2D = $OctopusParameters["Jobs.Catalog2Dnx.Service.Name"]

$titleC2R = $OctopusParameters["Jobs.Catalog2Registration.Title"]
$titleC2L = $OctopusParameters["Jobs.Catalog2Lucene.Title"]
$titleC2D = $OctopusParameters["Jobs.Catalog2Dnx.Title"]

$currentDirectory = [string](Get-Location)

# Install services
# Uses nssm - from http://nssm.cc/download

## Catalog2RegistrationV3
$installC2R = "nssm install $serviceNameC2R $currentDirectory\Catalog2RegistrationV3.cmd"
cmd /C $installC2R 
Set-Service -Name $serviceNameC2R -DisplayName "$titleC2R - $serviceNameC2R" -Description "Runs Catalog2RegistrationV3." -StartupType Automatic
sc.exe failure $serviceNameC2R reset= 30 actions= restart/5000 

## Catalog2LuceneV3
$installC2L = "nssm install $serviceNameC2L $currentDirectory\Catalog2LuceneV3.cmd"
cmd /C $installC2L 
Set-Service -Name $serviceNameC2L -DisplayName "$titleC2L - $serviceNameC2L" -Description "Runs Catalog2LuceneV3." -StartupType Automatic
sc.exe failure $serviceNameC2L reset= 30 actions= restart/5000 

## Catalog2DnxV3
$installC2D = "nssm install $serviceNameC2D $currentDirectory\Catalog2DnxV3.cmd"
cmd /C $installC2D 
Set-Service -Name $serviceNameC2D -DisplayName "$titleC2D - $serviceNameC2D" -Description "Runs Catalog2DnxV3." -StartupType Automatic
sc.exe failure $serviceNameC2D reset= 30 actions= restart/5000 


# Run services
net start $serviceNameC2R
net start $serviceNameC2L
net start $serviceNameC2D
