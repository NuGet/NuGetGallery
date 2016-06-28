@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.stats.createazurecdnwarehousereports.Title}"

	title #{Jobs.stats.createazurecdnwarehousereports.Title}

	start /w stats.createazurecdnwarehousereports.exe -LogsAzureStorageConnectionString #{Jobs.stats.createazurecdnwarehousereports.Storage.Primary} -AzureCdnCloudStorageAccount "#{Jobs.stats.createazurecdnwarehousereports.AzureCdn.CloudStorageAccount}" -AzureCdnCloudStorageContainerName "#{Jobs.stats.createazurecdnwarehousereports.AzureCdn.CloudStorageContainerName}" -StatisticsDatabase "#{Jobs.stats.createazurecdnwarehousereports.StatisticsDatabase}" -SourceDatabase "#{Jobs.stats.createazurecdnwarehousereports.SourceDatabase}" -DataStorageAccount "#{Jobs.stats.createazurecdnwarehousereports.DataStorageAccount}" -InstrumentationKey "#{Jobs.stats.createazurecdnwarehousereports.InstrumentationKey}" -DataContainerName "#{Jobs.stats.createazurecdnwarehousereports.DataContainerName}" -verbose true -interval #{Jobs.stats.createazurecdnwarehousereports.Interval}

	echo "Finished #{Jobs.stats.createazurecdnwarehousereports.Title}"

	goto Top
