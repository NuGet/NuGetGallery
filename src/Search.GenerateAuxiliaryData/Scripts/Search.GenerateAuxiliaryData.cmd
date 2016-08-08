@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.search.generateauxiliarydata.Title}"

	title #{Jobs.search.generateauxiliarydata.Title}

    start /w search.generateauxiliarydata.exe -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -LogsAzureStorageConnectionString #{Jobs.search.generateauxiliarydata.Storage.Primary} -PrimaryDestination #{Jobs.search.generateauxiliarydata.Storage.Primary} -PackageDatabase "#{Jobs.search.generateauxiliarydata.PackageDatabase}" -StatisticsDatabase "#{Jobs.search.generateauxiliarydata.StatisticsDatabase}" -verbose true -sleep #{Jobs.search.generateauxiliarydata.Sleep}

	echo "Finished #{Jobs.search.generateauxiliarydata.Title}"

	goto Top