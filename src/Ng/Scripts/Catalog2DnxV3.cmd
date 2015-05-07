@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.ngcatalog2dnx.Title}"

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.ngcatalog2dnx.Storage.Primary}

	title #{Jobs.ngcatalog2dnx.Title}

    start /w Ng.exe catalog2dnx -source #{Jobs.ngcatalog2dnx.Catalog.Source} -contentBaseAddress #{Jobs.ngcatalog2dnx.ContentBaseAddress} -storageType azure -storageAccountName #{Jobs.ngcatalog2dnx.Registration.StorageAccount.Name} -storageKeyValue #{Jobs.ngcatalog2dnx.Registration.StorageAccount.Key} -storageContainer #{Jobs.ngcatalog2dnx.Registration.Container} -verbose true -interval #{Jobs.ngcatalog2dnx.Interval}

	echo "Finished #{Jobs.ngcatalog2dnx.Title}"

	goto Top