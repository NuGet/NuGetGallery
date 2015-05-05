@echo OFF

:Top
	echo "Starting job - aacatalog2registration"
	
	cd Ng

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.aacatalog2registration.Storage.Primary}

	title #{Jobs.aacatalog2registration.Title}

    start /w Ng.exe catalog2registration -source #{Jobs.aacatalog2registration.Catalog.Source} -contentBaseAddress #{Jobs.aacatalog2registration.ContentBaseAddress} -storageBaseAddress #{Jobs.aacatalog2registration.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.aacatalog2registration.Registration.StorageAccount.Name} -storageKeyValue #{Jobs.aacatalog2registration.Registration.StorageAccount.Key} -storageContainer #{Jobs.aacatalog2registration.Registration.Container} -verbose true -interval #{Jobs.aacatalog2registration.Interval}

	echo "Finished - aacatalog2registration"

	goto Top