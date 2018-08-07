@echo OFF
	
cd bin

:Top
	echo "Starting job - #{Jobs.Gallery.Maintenance.Title}"
	
	title #{Jobs.Gallery.Maintenance.Title}
	
	start /w Gallery.Maintenance.exe -Configuration "#{Jobs.gallery.maintenance.Configuration}"  -InstrumentationKey "#{Jobs.gallery.maintenance.InstrumentationKey}" -Interval #{Jobs.gallery.maintenance.Interval} 

	echo "Finished #{Jobs.Gallery.Maintenance.Title}"

	goto Top