@echo OFF

:Top
	echo "Starting job - Catalog2Lucene"
	
	cd Ng

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.Storage.Primary}

	title #{Jobs.Catalog2Lucene.Title}

    start /w Ng.exe catalog2lucene -source #{Jobs.Catalog.Source} -luceneDirectoryType azure -luceneStorageAccountName #{Jobs.Catalog2Lucene.Lucene.StorageAccount.Name} -luceneStorageKeyValue #{Jobs.Catalog2Lucene.Lucene.StorageAccount.Key} -luceneStorageContainer #{Jobs.Catalog2Lucene.Lucene.Container} -registration #{Jobs.Catalog2Lucene.RegistrationCursor} -verbose true -interval #{Jobs.Catalog2Lucene.Interval}

	echo "Finished - Catalog2Lucene"

	goto Top