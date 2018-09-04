@echo OFF
	
cd bin

echo "Starting job - NuGet - SupportRequests.Notifications.WeeklySummaryNotification.cmd"
	
title "NuGet - SupportRequests.Notifications.WeeklySummaryNotification.cmd"

start /w nuget.supportrequests.notifications.exe ^
    -Task "WeeklySummaryNotification" ^
    -Configuration "#{Jobs.supportrequests.notifications.Configuration}" ^
    -InstrumentationKey "#{Jobs.supportrequests.notifications.InstrumentationKey}" ^
    -verbose true ^
    -Once

echo "Finished job - NuGet - SupportRequests.Notifications.WeeklySummaryNotification.cmd"