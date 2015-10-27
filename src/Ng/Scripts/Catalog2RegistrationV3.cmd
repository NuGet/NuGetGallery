@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.catalog2registrationv3.Title}"

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.common.v3.Storage.Primary}

	title #{Jobs.catalog2registrationv3.Title}

    start /w Ng.exe catalog2registration -source #{Jobs.catalog2registrationv3.Source} -contentBaseAddress #{Jobs.catalog2registrationv3.ContentBaseAddress} -storageBaseAddress #{Jobs.catalog2registrationv3.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.catalog2registrationv3.StorageContainer} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.catalog2registrationv3.Title}"

	goto Top
