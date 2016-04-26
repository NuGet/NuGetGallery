@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.feed2catalogv3secondary.Title}"

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.common.v3.Storage.Primary}
	set NUGETJOBS_STORAGE_SECONDARY=#{Jobs.common.v3.Storage.Secondary}	

	title #{Jobs.feed2catalogv3secondary.Title}

    start /w .\Ng.exe feed2catalog -gallery #{Jobs.common.v3.f2c.Gallery} -storageBaseAddress #{Jobs.feed2catalogv3secondary.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.common.v3.Storage.Secondary.Name} -storageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} -storageContainer #{Jobs.feed2catalogv3secondary.StorageContainer} -storageTypeAuditing azure -storageAccountNameAuditing #{Jobs.common.v3.c2r.StorageAccountName} -storageKeyValueAuditing #{Jobs.common.v3.c2r.StorageAccountKey} -storageContainerAuditing auditing -storagePathAuditing package -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.feed2catalogv3secondary.Title}"

	goto Top
	