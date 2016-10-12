@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.archivepackages.Title}"

	title #{Jobs.archivepackages.Title}

    start /w archivepackages.exe -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -LogsAzureStorageConnectionString "#{Jobs.archivepackages.Storage.Primary}" -PackageDatabase "#{Jobs.archivepackages.PackageDatabase}" -Source "#{Jobs.archivepackages.Source}" -PrimaryDestination "#{Jobs.archivepackages.PrimaryDestination}" -SecondaryDestination "#{Jobs.archivepackages.SecondaryDestination}" -Sleep "#{Jobs.archivepackages.Sleep}"

	echo "Finished #{Jobs.archivepackages.Title}"

	goto Top
	