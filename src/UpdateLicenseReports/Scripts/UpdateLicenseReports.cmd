@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.updatelicensereports.Title}"
	
	title #{Jobs.updatelicensereports.Title}

    start /w updatelicensereports.exe -LogsAzureStorageConnectionString #{Jobs.updatelicensereports.Storage.Primary} -LicenseReportService "#{Jobs.updatelicensereports.LicenseReportServiceUri}" -LicenseReportUser "#{Jobs.updatelicensereports.LicenseReportUser}" -LicenseReportPassword "#{Jobs.updatelicensereports.LicenseReportPassword}" -PackageDatabase "#{Jobs.updatelicensereports.PackageDatabase}" -verbose true -sleep #{Jobs.updatelicensereports.Sleep}

	echo "Finished #{Jobs.updatelicensereports.Title}"

	goto Top