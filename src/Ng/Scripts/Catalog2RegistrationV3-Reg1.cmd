@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.catalog2registrationv3reg1.Title}"

	set NUGETJOBS_STORAGE_PRIMARY=DefaultEndpointsProtocol=#{Jobs.common.v3.Storage.Primary}

	title #{Jobs.catalog2registrationv3reg1.Title}

    start /w Ng.exe catalog2registration -source #{Jobs.common.v3.Source} -contentBaseAddress #{Jobs.common.v3.ContentBaseAddress} -storageBaseAddress #{Jobs.catalog2registrationv3reg1.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.catalog2registrationv3reg1.StorageContainer} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.catalog2registrationv3reg1.Title}"

	goto Top