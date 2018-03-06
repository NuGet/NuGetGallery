@echo OFF
	
cd bin

echo "Starting job - NuGet - SupportRequests.Notifications.OnCallDailyNotification.cmd"
	
title "NuGet - SupportRequests.Notifications.OnCallDailyNotification.cmd"

start /w nuget.supportrequests.notifications.exe ^
	-Task "OnCallDailyNotification" ^
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

echo "Finished job - NuGet - SupportRequests.Notifications.OnCallDailyNotification.cmd"