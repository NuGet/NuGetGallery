@echo OFF

:Top
	echo "Starting job - Catalog2Registration"
	
	cd Ng

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.Storage.Primary}

	title #{Jobs.Catalog2Registration.Title}

    start /w Ng.exe catalog2registration -source #{Jobs.Catalog.Source} -contentBaseAddress #{Jobs.Catalog2Registration.ContentBaseAddress} -storageBaseAddress #{Jobs.Catalog2Registration.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.Catalog2Registration.Registration.StorageAccount.Name} -storageKeyValue #{Jobs.Catalog2Registration.Registration.StorageAccount.Name} -storageContainer #{Jobs.Catalog2Registration.Registration.Container} -verbose true -interval #{Jobs.Catalog2Registration.Interval}

	echo "Finished - Catalog2Registration"

	goto Top