@echo OFF

:Top
	echo "Starting job - Catalog2Dnx"
	
	cd Ng

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.Storage.Primary}

	title #{Jobs.Catalog2Dnx.Title}

    start /w Ng.exe catalog2dnx -source #{Jobs.Catalog.Source} -contentBaseAddress #{Jobs.Catalog2Dnx.ContentBaseAddress} -storageBaseAddress #{Jobs.Catalog2Dnx.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.Catalog2Dnx.Registration.StorageAccount.Name} -storageKeyValue #{Jobs.Catalog2Dnx.Registration.StorageAccount.Key} -storageContainer #{Jobs.Catalog2Dnx.Registration.Container} -verbose true -interval #{Jobs.Catalog2Dnx.Interval}

	echo "Finished - Catalog2Dnx"

	goto Top