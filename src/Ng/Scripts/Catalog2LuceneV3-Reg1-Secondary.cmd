@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.catalog2lucenev3reg1secondary.Title}"

	set NUGETJOBS_STORAGE_SECONDARY=#{Jobs.common.v3.Storage.Secondary}	

	title #{Jobs.catalog2lucenev3reg1secondary.Title}

    start /w Ng.exe catalog2lucene -source #{Jobs.catalog2lucenev3reg1secondary.Source} -luceneDirectoryType azure -luceneStorageAccountName #{Jobs.common.v3.Storage.Secondary.Name} -luceneStorageKeyValue #{Jobs.common.v3.Storage.Secondary.Key} -luceneStorageContainer #{Jobs.catalog2lucenev3reg1secondary.LuceneContainer} -registration #{Jobs.catalog2lucenev3reg1secondary.Registration} -elasticsearchendpoint #{Jobs.common.v3.Logging.ElasticsearchEndpoint} -elasticsearchusername #{Jobs.common.v3.Logging.ElasticsearchUsername} -elasticsearchpassword #{Jobs.common.v3.Logging.ElasticsearchPassword} -verbose true -interval #{Jobs.common.v3.Interval}

	echo "Finished #{Jobs.catalog2lucenev3reg1secondary.Title}"

	goto Top