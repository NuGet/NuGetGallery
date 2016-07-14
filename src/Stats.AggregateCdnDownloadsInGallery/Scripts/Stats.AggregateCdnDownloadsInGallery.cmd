@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.aggregatecdndownloadsingallery.Title}"
	
	title #{Jobs.stats.aggregatecdndownloadsingallery.Title}

	start /w stats.aggregatecdndownloadsingallery.exe -ConsoleLogOnly -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -StatisticsDatabase "#{Jobs.stats.aggregatecdndownloadsingallery.StatisticsDatabase}" -DestinationDatabase "#{Jobs.stats.aggregatecdndownloadsingallery.DestinationDatabase}" -InstrumentationKey "#{Jobs.stats.aggregatecdndownloadsingallery.InstrumentationKey}" -verbose true -interval #{Jobs.stats.aggregatecdndownloadsingallery.Interval} 

	echo "Finished #{Jobs.stats.aggregatecdndownloadsingallery.Title}"

	goto Top
