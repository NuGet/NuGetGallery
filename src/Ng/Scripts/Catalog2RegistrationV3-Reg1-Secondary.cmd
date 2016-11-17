@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.catalog2registrationv3reg1secondary.Title}"

	title #{Jobs.catalog2registrationv3reg1secondary.Title}

    start /w Ng.exe catalog2registration -source #{Jobs.catalog2registrationv3reg1secondary.Source} -contentBaseAddress #{Jobs.catalog2registrationv3reg1secondary.ContentBaseAddress} -storageBaseAddress #{Jobs.catalog2registrationv3reg1secondary.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3.Storage.Secondary.Name} -storageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} -storageContainer #{Jobs.catalog2registrationv3reg1secondary.StorageContainer} -useCompressedStorage #{Jobs.catalog2registrationv3reg1secondary.UseCompressedStorage} -compressedStorageBaseAddress #{Jobs.catalog2registrationv3reg1secondary.StorageBaseAddressCompressed} -compressedStorageAccountName #{Jobs.common.v3.Storage.Secondary.Name} -compressedStorageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} -compressedStorageContainer #{Jobs.catalog2registrationv3reg1secondary.StorageContainerCompressed} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -vaultName #{Deployment.Azure.KeyVault.VaultName} -clientId #{Deployment.Azure.KeyVault.ClientId} -certificateThumbprint #{Deployment.Azure.KeyVault.CertificateThumbprint} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.catalog2registrationv3reg1secondary.Title}"

	goto Top