@echo OFF
	
cd bin

:Top
echo "Starting job - #{Jobs.stats.createazurecdnwarehousereports.Title}"

title #{Jobs.stats.createazurecdnwarehousereports.Title}

start /w stats.createazurecdnwarehousereports.exe ^
    -Configuration "#{Jobs.stats.createazurecdnwarehousereports.Configuration}"
	-InstrumentationKey "#{Jobs.stats.createazurecdnwarehousereports.InstrumentationKey}" ^
	-Interval #{Jobs.stats.createazurecdnwarehousereports.Interval}  ^
	-verbose true

echo "Finished #{Jobs.stats.createazurecdnwarehousereports.Title}"

goto Top
