@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.ngcatalog2dnx.Title.Secondary}"

	title #{Jobs.ngcatalog2dnx.Title.Secondary}
	
    start /w Ng.exe catalog2dnx -source #{Jobs.ngcatalog2dnx.Catalog.Source.Secondary} -contentBaseAddress #{Jobs.ngcatalog2dnx.ContentBaseAddress.Secondary} -storageBaseAddress #{Jobs.ngcatalog2dnx.StorageBaseAddress.Secondary} -storageType azure -storageAccountName #{Jobs.common.v3.Storage.Secondary.Name} -storageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} -storageContainer #{Jobs.ngcatalog2dnx.Container} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.ngcatalog2dnx.Interval}
	
	echo "Finished #{Jobs.ngcatalog2dnx.Title.Secondary}"

	goto Top