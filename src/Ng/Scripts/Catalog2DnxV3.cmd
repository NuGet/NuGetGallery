@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.ngcatalog2dnx.Title}"

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.ngcatalog2dnx.Storage.Primary}

	title #{Jobs.ngcatalog2dnx.Title}
	
    start /w Ng.exe catalog2dnx -source #{Jobs.ngcatalog2dnx.Catalog.Source} -contentBaseAddress #{Jobs.ngcatalog2dnx.ContentBaseAddress} -storageBaseAddress #{Jobs.ngcatalog2dnx.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.ngcatalog2dnx.StorageAccount.Name} -storageKeyValue #{Jobs.ngcatalog2dnx.StorageAccount.Key} -storageContainer #{Jobs.ngcatalog2dnx.Container} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.ngcatalog2dnx.Interval}
	
	echo "Finished #{Jobs.ngcatalog2dnx.Title}"

	goto Top