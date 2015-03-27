@ECHO OFF

:Top
	Echo "Starting Job"
	
	cd "C:\nuget\Jobs\Ng"

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.Storage.Primary}

	title #{Jobs.Catalog2Registration.Title}

        Start /w Ng.exe catalog2registration -source #{Jobs.Catalog.Source} -contentBaseAddress #{Jobs.Catalog2Registration.ConcentBaseAddress} -storageBaseAddress #{Jobs.Catalog2Registration.StorageBaseAddress} -storageType azure -storageAccountName #{Jobs.Catalog2Registration.Registration.StorageAccount.Name} -storageKeyValue #{Jobs.Catalog2Registration.Registration.StorageAccount.Name} -storageContainer #{Jobs.Catalog2Registration.Registration.Container} -verbose true -interval #{Jobs.Catalog2Registration.Interval}

	Echo "Finished"

	Goto Top