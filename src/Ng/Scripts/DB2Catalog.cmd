@echo OFF
	
cd Ng

:Top
	echo "Starting job - #{Jobs.db2catalog.Title}"

	title #{Jobs.db2catalog.Title}

    start /w DB2Catalog 250

	echo "Finished #{Jobs.db2catalog.Title}"

	goto Top