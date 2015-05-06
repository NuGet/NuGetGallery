@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.feed2catalogv3.Title}"

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.common.v3.Storage.Primary}

	title #{Jobs.feed2catalogv3.Title}

    start /w .\Ng.exe feed2catalog -gallery #{Jobs.common.v3.f2c.Gallery} -storageBaseAddress #{Jobs.feed2catalogv3.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValue #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainer #{Jobs.feed2catalogv3.StorageContainer} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.feed2catalogv3.Title}"

	goto Top
	