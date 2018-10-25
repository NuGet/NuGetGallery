@echo OFF
	
cd bin

:Top
echo "Starting job - NuGet.Jobs.Db2AzureSearch"

title NuGet.Jobs.Db2AzureSearch

start /w NuGet.Jobs.Db2AzureSearch.exe ^
	-Configuration "#{Jobs.Db2AzureSearch.Configuration}" ^
	-InstrumentationKey "#{Jobs.Db2AzureSearch.ApplicationInsightsInstrumentationKey}" ^
	-Verbose true

echo "Finished NuGet.Jobs.Db2AzureSearch"
