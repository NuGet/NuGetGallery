@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.gallery.credentialexpiration.Title}"
	
	title #{Jobs.gallery.credentialexpiration.Title}

	REM SmtpUri is expected to be of the format: smtps://username:password@host:port. Note that if username contains an "@", you need to URI encode it!

	start /w gallery.credentialexpiration.exe -LogsAzureStorageConnectionString #{Jobs.gallery.credentialexpiration.Storage.Primary} -WhatIf #{Jobs.gallery.credentialexpiration.WhatIf} -WarnDaysBeforeExpiration #{Jobs.gallery.credentialexpiration.WarnDaysBeforeExpiration} -MailFrom "#{Jobs.gallery.credentialexpiration.MailFrom}" -GalleryBrand "#{Jobs.gallery.credentialexpiration.GalleryBrand}" -GalleryAccountUrl "#{Jobs.gallery.credentialexpiration.GalleryAccountUrl}"	-SmtpUri "#{Jobs.gallery.credentialexpiration.SmtpUri}"	-GalleryDatabase "#{Jobs.gallery.credentialexpiration.GalleryDatabase}"	-InstrumentationKey "#{Jobs.gallery.credentialexpiration.InstrumentationKey}" -verbose true -interval #{Jobs.gallery.credentialexpiration.Interval}

	echo "Finished #{Jobs.gallery.credentialexpiration.Title}"

	goto Top