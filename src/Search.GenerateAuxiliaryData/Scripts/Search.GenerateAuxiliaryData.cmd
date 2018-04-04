@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.search.generateauxiliarydata.Title}"

	title #{Jobs.search.generateauxiliarydata.Title}

    start /w search.generateauxiliarydata.exe ^
	-VaultName "#{Deployment.Azure.KeyVault.VaultName}" ^
	-ClientId "#{Deployment.Azure.KeyVault.ClientId}" ^
	-CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" ^
	-PrimaryDestination #{Jobs.search.generateauxiliarydata.Storage.Primary} ^
	-PackageDatabase "#{Jobs.search.generateauxiliarydata.PackageDatabase}" ^
	-StatisticsDatabase "#{Jobs.search.generateauxiliarydata.StatisticsDatabase}" ^
	-AzureCdnCloudStorageAccount "#{Jobs.stats.createazurecdnwarehousereports.AzureCdn.CloudStorageAccount}"
	-AzureCdnCloudStorageContainerName "#{Jobs.stats.createazurecdnwarehousereports.AzureCdn.CloudStorageContainerName}"
	-verbose true ^
	-Sleep #{Jobs.search.generateauxiliarydata.Sleep} ^
	-InstrumentationKey "#{Jobs.search.generateauxiliarydata.ApplicationInsightsInstrumentationKey}"

	echo "Finished #{Jobs.search.generateauxiliarydata.Title}"

	goto Top