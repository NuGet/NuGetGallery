@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.catalog2registrationv3reg1secondary.Title}"

	set NUGETJOBS_STORAGE_SECONDARY=#{Jobs.common.v3.Storage.Secondary}	

	title #{Jobs.catalog2registrationv3reg1secondary.Title}

    start /w Ng.exe catalog2registration -source #{Jobs.catalog2registrationv3reg1secondary.Source} -contentBaseAddress #{Jobs.catalog2registrationv3reg1secondary.ContentBaseAddress} -storageBaseAddress #{Jobs.catalog2registrationv3reg1secondary.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3.Storage.Secondary.Name} -storageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} -storageContainer #{Jobs.catalog2registrationv3reg1secondary.StorageContainer} -compressedStorageBaseAddress #{Jobs.catalog2registrationv3reg1secondary.StorageBaseAddressCompressed} -compressedStorageAccountName #{Jobs.common.v3.Storage.Secondary.Name} -compressedStorageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} -compressedStorageContainer #{Jobs.catalog2registrationv3reg1secondary.StorageContainerCompressed} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.catalog2registrationv3reg1secondary.Title}"

	goto Top