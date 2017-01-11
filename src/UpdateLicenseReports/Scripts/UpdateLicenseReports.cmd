@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.updatelicensereports.Title}"
	
	title #{Jobs.updatelicensereports.Title}

    start /w updatelicensereports.exe -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -LogsAzureStorageConnectionString #{Jobs.updatelicensereports.Storage.Primary} -LicenseReportService "#{Jobs.updatelicensereports.LicenseReportServiceUri}" -LicenseReportUser "#{Jobs.updatelicensereports.LicenseReportUser}" -LicenseReportPassword "#{Jobs.updatelicensereports.LicenseReportPassword}" -PackageDatabase "#{Jobs.updatelicensereports.PackageDatabase}" -verbose true -Sleep #{Jobs.updatelicensereports.Sleep}

	echo "Finished #{Jobs.updatelicensereports.Title}"

	goto Top