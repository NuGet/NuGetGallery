@echo OFF

:Top
	echo "Starting job - aacatalog2lucene"
	
	cd Ng

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.aacatalog2lucene.Storage.Primary}

	title #{Jobs.aacatalog2lucene.Title}

    start /w Ng.exe catalog2lucene -source #{Jobs.aacatalog2lucene.Catalog.Source} -luceneDirectoryType azure -luceneStorageAccountName #{Jobs.aacatalog2lucene.Lucene.StorageAccount.Name} -luceneStorageKeyValue #{Jobs.aacatalog2lucene.Lucene.StorageAccount.Key} -luceneStorageContainer #{Jobs.aacatalog2lucene.Lucene.Container} -registration #{Jobs.aacatalog2lucene.RegistrationCursor} -verbose true -interval #{Jobs.aacatalog2lucene.Interval}

	echo "Finished - aacatalog2lucene"

	goto Top