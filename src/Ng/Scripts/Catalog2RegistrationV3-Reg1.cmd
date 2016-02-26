@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.catalog2registrationv3reg1.Title}"

	set NUGETJOBS_STORAGE_PRIMARY=DefaultEndpointsProtocol=#{Jobs.common.v3.Storage.Primary}

	title #{Jobs.catalog2registrationv3reg1.Title}

    start /w Ng.exe catalog2registration -source #{Jobs.common.v3.Source} -contentBaseAddress #{Jobs.catalog2registrationv3reg1.ContentBaseAddress} -storageBaseAddress #{Jobs.catalog2registrationv3reg1.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.catalog2registrationv3reg1.StorageContainer} -compressedStorageBaseAddress #{Jobs.catalog2registrationv3reg1.StorageBaseAddressCompressed} -compressedStorageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -compressedStorageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -compressedStorageContainer #{Jobs.catalog2registrationv3reg1.StorageContainerCompressed} -elasticsearchendpoint #{Jobs.common.v3.Logging.ElasticsearchEndpoint} -elasticsearchusername #{Jobs.common.v3.Logging.ElasticsearchUsername} -elasticsearchpassword #{Jobs.common.v3.Logging.ElasticsearchPassword} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.catalog2registrationv3reg1.Title}"

	goto Top
