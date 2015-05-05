@echo OFF

:Top
	echo "Starting job - aacatalog2registration"
	
	cd Ng

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.aacatalog2registration.Storage.Primary}

	title #{Jobs.aacatalog2registration.Title}

    start /w Ng.exe catalog2registration -source #{Jobs.ngcatalog2dnx.Catalog.Source} -contentBaseAddress #{Jobs.ngcatalog2dnx.ContentBaseAddress} -storageBaseAddress #{Jobs.ngcatalog2dnx.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.ngcatalog2dnx.Registration.StorageAccount.Name} -storageKeyValue #{Jobs.ngcatalog2dnx.Registration.StorageAccount.Key} -storageContainer #{Jobs.ngcatalog2dnx.Registration.Container} -verbose true -interval #{Jobs.aacatalog2registration.Interval}

	echo "Finished - aacatalog2registration"

	goto Top