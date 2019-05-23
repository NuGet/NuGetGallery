Start-Process `
	.\bin\NuGet.Jobs.Db2AzureSearch.exe `
	-ArgumentList "-Configuration `"bin\octopus.json`" -InstrumentationKey `"#{ApplicationInsightsInstrumentationKey}`" -Verbose true"
