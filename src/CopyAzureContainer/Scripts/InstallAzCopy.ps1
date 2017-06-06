. .\Functions.ps1

Write-Host Installing AzCopy...
$toolsPath="$PSScriptRoot\..\bin\Debug\tools\azcopy"
Install-AzCopy -toolsPath $toolsPath
Write-Host Installed AzCopy
