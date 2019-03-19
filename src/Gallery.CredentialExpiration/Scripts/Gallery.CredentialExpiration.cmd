@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.gallery.credentialexpiration.Title}"
	
	title #{Jobs.gallery.credentialexpiration.Title}

	start /w gallery.credentialexpiration.exe -Configuration "#{Jobs.gallery.credentialexpiration.Configuration}" -InstrumentationKey "#{Jobs.gallery.credentialexpiration.InstrumentationKey}" -verbose true -Interval #{Jobs.gallery.credentialexpiration.Interval}

	echo "Finished #{Jobs.gallery.credentialexpiration.Title}"

	goto Top