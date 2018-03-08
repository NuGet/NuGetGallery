@echo OFF
	
cd bin

echo "Starting job - NuGet - SupportRequests.Notifications.WeeklySummaryNotification.cmd"
	
title "NuGet - SupportRequests.Notifications.WeeklySummaryNotification.cmd"

start /w nuget.supportrequests.notifications.exe ^
    -Task "WeeklySummaryNotification" ^
    -TargetEmailAddress "#{Jobs.supportrequests.notifications.weeklysummarynotification.TargetEmailAddress}" ^
    -SourceDatabase "#{Jobs.supportrequests.notifications.SupportRequestsDatabase}" ^
    -PagerDutyAccountName "nuget" ^
    -PagerDutyApiKey "$$Prod-PagerDuty-ApiKey$$" ^
    -SmtpUri "#{Jobs.supportrequests.notifications.SmtpUri}" ^
    -VaultName "#{Deployment.Azure.KeyVault.VaultName}" ^
    -ClientId "#{Deployment.Azure.KeyVault.ClientId}" ^
    -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" ^
    -InstrumentationKey "#{Jobs.supportrequests.notifications.InstrumentationKey}" ^
    -verbose true ^
    -Once

echo "Finished job - NuGet - SupportRequests.Notifications.WeeklySummaryNotification.cmd"