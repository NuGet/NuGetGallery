@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.feed2catalogv3secondary.Title}"

	title #{Jobs.feed2catalogv3secondary.Title}

    start /w .\Ng.exe feed2catalog -gallery #{Jobs.common.v3.f2c.Gallery} -storageBaseAddress #{Jobs.feed2catalogv3secondary.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3.Storage.Secondary.Name} -storageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} -storageContainer #{Jobs.feed2catalogv3secondary.StorageContainer} -storageTypeAuditing azure -storageAccountNameAuditing #{Jobs.feed2catalogv3.AuditingStorageAccountName} -storageKeyValueAuditing #{Jobs.feed2catalogv3.AuditingStorageAccountKey} -storageContainerAuditing auditing -storagePathAuditing package -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.feed2catalogv3secondary.Title}"

	goto Top
	