@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.catalog2lucenev3reg1.Title}"

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.common.v3.Storage.Primary}

	title #{Jobs.catalog2lucenev3reg1.Title}

    start /w Ng.exe catalog2lucene -source #{Jobs.common.v3.Source} -luceneDirectoryType azure -luceneStorageAccountName #{Jobs.common.v3.Storage.Primary.Name} -luceneStorageKeyValue #{Jobs.common.v3.Storage.Primary.Key} -luceneStorageContainer #{Jobs.catalog2lucenev3reg1.LuceneContainer} -registration #{Jobs.catalog2lucenev3reg1.Registration} -instrumentationkey #{Jobs.common.v3.Logging.InstrumentationKey} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.catalog2lucenev3reg1.Title}"

	goto Top