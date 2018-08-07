@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.gallery.credentialexpiration.Title}"
	
	title #{Jobs.gallery.credentialexpiration.Title}

	REM SmtpUri is expected to be of the format: smtps://username:password@host:port. Note that if username contains an "@", you need to URI encode it!

	start /w gallery.credentialexpiration.exe -Configuration "#{Jobs.gallery.credentialexpiration.Configuration}" -InstrumentationKey "#{Jobs.gallery.credentialexpiration.InstrumentationKey}" -verbose true -Interval #{Jobs.gallery.credentialexpiration.Interval}

	echo "Finished #{Jobs.gallery.credentialexpiration.Title}"

	goto Top