@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.db2catalog.Title}"

	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.common.v3.Storage.Primary}

	title #{Jobs.db2catalog.Title}

    start /w DB2Catalog 250

	echo "Finished #{Jobs.db2catalog.Title}"

	goto Top