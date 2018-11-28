@echo OFF
	
cd bin

:Top
echo "Starting job - #{Jobs.Stats.CollectAzureChinaCDNLogs.Title}"

title #{Jobs.Stats.CollectAzureChinaCDNLogs.Title}

start /w Stats.CollectAzureChinaCDNLogs.exe ^
-InstrumentationKey "#{Jobs.Stats.CollectAzureChinaCDNLogs.InstrumentationKey}" ^
-verbose true ^
-Interval #{Jobs.Stats.CollectAzureChinaCDNLogs.Interval}

echo "Finished #{Jobs.Stats.CollectAzureChinaCDNLogs.Title}"

goto Top