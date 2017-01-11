@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.rollupdownloadfacts.Title}"
	
	title #{Jobs.stats.rollupdownloadfacts.Title}

    start /w stats.rollupdownloadfacts.exe -ConsoleLogOnly -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -MinAgeInDays "#{Jobs.stats.rollupdownloadfacts.MinAgeInDays}" -StatisticsDatabase "#{Jobs.stats.rollupdownloadfacts.StatisticsDatabase}" -InstrumentationKey "#{Jobs.stats.rollupdownloadfacts.InstrumentationKey}" -verbose true -Interval #{Jobs.stats.rollupdownloadfacts.Interval}

	echo "Finished #{Jobs.stats.rollupdownloadfacts.Title}"

	goto Top