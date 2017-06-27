. .\Functions.ps1

Write-Host Installing AzCopy...
Install-AzCopy 
Write-Host Installed AzCopy

Write-Host Register the scheduled task
Install-BackupV3Task
Write-Host Registered the scheduled task 
