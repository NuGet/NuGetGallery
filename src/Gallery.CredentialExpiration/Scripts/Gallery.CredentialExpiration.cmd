@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.gallery.credentialexpiration.Title}"
	
	title #{Jobs.gallery.credentialexpiration.Title}

	REM SmtpUri is expected to be of the format: smtps://username:password@host:port. Note that if username contains an "@", you need to URI encode it!

	start /w gallery.credentialexpiration.exe -ConsoleLogOnly -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}"  -WhatIf #{Jobs.gallery.credentialexpiration.WhatIf} -WarnDaysBeforeExpiration #{Jobs.gallery.credentialexpiration.WarnDaysBeforeExpiration} -MailFrom "#{Jobs.gallery.credentialexpiration.MailFrom}" -GalleryBrand "#{Jobs.gallery.credentialexpiration.GalleryBrand}" -GalleryAccountUrl "#{Jobs.gallery.credentialexpiration.GalleryAccountUrl}" -SmtpUri "#{Jobs.gallery.credentialexpiration.SmtpUri}" -GalleryDatabase "#{Jobs.gallery.credentialexpiration.GalleryDatabase}" -InstrumentationKey "#{Jobs.gallery.credentialexpiration.InstrumentationKey}" -verbose true -Interval #{Jobs.gallery.credentialexpiration.Interval} -DataStorageAccount "#{Jobs.gallery.credentialexpiration.Storage.Primary}" -ContainerName "#{Jobs.gallery.credentialexpiration.ContainerName}"

	echo "Finished #{Jobs.gallery.credentialexpiration.Title}"

	goto Top