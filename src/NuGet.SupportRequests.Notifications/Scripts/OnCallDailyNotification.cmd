@echo OFF
	
cd bin

echo "Starting job - NuGet - SupportRequests.Notifications.OnCallDailyNotification.cmd"
	
title "NuGet - SupportRequests.Notifications.OnCallDailyNotification.cmd"

start /w nuget.supportrequests.notifications.exe ^
    -Task "OnCallDailyNotification" ^
    -Configuration "#{Jobs.supportrequests.notifications.Configuration}" ^
    -InstrumentationKey "#{Jobs.supportrequests.notifications.InstrumentationKey}" ^
    -verbose true ^
    -Once

echo "Finished job - NuGet - SupportRequests.Notifications.OnCallDailyNotification.cmd"