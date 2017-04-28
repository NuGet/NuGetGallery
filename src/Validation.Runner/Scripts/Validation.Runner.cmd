@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.validation.Title}"
	
	title #{Jobs.validation.Title}

	start /w Validation.Runner.exe -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -LogsAzureStorageConnectionString #{Jobs.validation.DataStorageAccount} -RunValidationTasks "#{Jobs.validation.RunValidationTasks}" -RequestValidationTasks "#{Jobs.validation.RequestValidationTasks}" -GalleryBaseAddress "#{Jobs.validation.GalleryBaseAddress}" -DataStorageAccount "#{Jobs.validation.DataStorageAccount}" -ContainerName "#{Jobs.validation.ContainerName}" -VcsValidatorServiceUrl "#{Jobs.validation.VcsValidatorServiceUrl}" -VcsValidatorCallbackUrl "#{Jobs.validation.VcsValidatorCallbackUrl}" -VcsValidatorAlias "#{Jobs.validation.VcsValidatorAlias}" -VcsPackageUrlTemplate "#{Jobs.validation.VcsPackageUrlTemplate}" -verbose true -Interval #{Jobs.validation.Interval} -InstrumentationKey "#{Jobs.validation.ApplicationInsightsInstrumentationKey}" -VcsContactAlias "#{Jobs.validation.NugetVcsContactAlias}"

	echo "Finished #{Jobs.validation.Title}"

	goto Top
