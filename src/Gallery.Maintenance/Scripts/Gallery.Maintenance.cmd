@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.Gallery.Maintenance.Title}"
	
	title #{Jobs.Gallery.Maintenance.Title}
	
	start /w Gallery.Maintenance.exe -ConsoleLogOnly -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -GalleryDatabase "#{Jobs.Gallery.Maintenance.GalleryDatabase}" -InstrumentationKey "#{Jobs.Gallery.Maintenance.InstrumentationKey}" -verbose true -Interval #{Jobs.Gallery.Maintenance.Interval} 

	echo "Finished #{Jobs.Gallery.Maintenance.Title}"

	goto Top