@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.search.generateauxiliarydata.Title}"
	
	set NUGETJOBS_STORAGE_PRIMARY=#{Jobs.search.generateauxiliarydata.Storage.Primary}

	title #{Jobs.search.generateauxiliarydata.Title}

    start /w search.generateauxiliarydata.exe -SourceDatabase "#{Jobs.search.generateauxiliarydata.SourceDatabase}" -PackageDatabase "#{Jobs.search.generateauxiliarydata.PackageDatabase}" -verbose true -sleep #{Jobs.search.generateauxiliarydata.Sleep}

	echo "Finished #{Jobs.search.generateauxiliarydata.Title}"

	goto Top