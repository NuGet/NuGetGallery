@echo OFF

:Top
	echo "Starting job - Catalog2Dnx"
	
	cd Ng

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.Catalog2Dnx.Storage.Primary}

	title #{Jobs.Catalog2Dnx.Title}

    start /w Ng.exe catalog2dnx -source #{Jobs.Catalog2Dnx.Catalog.Source} -contentBaseAddress #{Jobs.Catalog2Dnx.ContentBaseAddress} -storageType azure -storageAccountName #{Jobs.Catalog2Dnx.Registration.StorageAccount.Name} -storageKeyValue #{Jobs.Catalog2Dnx.Registration.StorageAccount.Key} -storageContainer #{Jobs.Catalog2Dnx.Registration.Container} -verbose true -interval #{Jobs.Catalog2Dnx.Interval}

	echo "Finished - Catalog2Dnx"

	goto Top