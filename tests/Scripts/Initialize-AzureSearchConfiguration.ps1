[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][string]$SourceConfigDir,
    [Parameter(Mandatory=$true)][string]$ConfigurationName,
    [Parameter(Mandatory=$true)][ValidateSet("production", "staging")][string]$Slot
)

Write-Host "Initializing configuration for Azure Search Functional tests for '$ConfigurationName'"

$filename = "$ConfigurationName.json"
$destinationDirectory = Join-Path $PSScriptRoot "..\NuGet.Services.AzureSearch.FunctionalTests\ExternalConfig"
$destinationPath = Join-Path $destinationDirectory $filename
if (-not (Test-Path $destinationDirectory)) {
    New-Item -Path $destinationDirectory -ItemType "directory"
}

$tempFileName = "temp-$filename"
$tempDestinationPath = Join-Path $destinationDirectory $tempFileName

# Copy the configuration file from the specified directory
Copy-Item (Join-Path $SourceConfigDir $filename) $tempDestinationPath -Verbose
$configObject = Get-Content -Path $tempDestinationPath | ConvertFrom-Json
Remove-Item -Path $tempDestinationPath

# Add a field to the file determining which slot should be tested
$configObject | Add-Member -MemberType NoteProperty -Name "Slot" -Value $Slot

# Save the file and set an environment variable to be used by the functional tests
Write-Host "Writing configuration file with updated values: $destinationPath"
ConvertTo-Json $configObject | Out-File $destinationPath
[Environment]::SetEnvironmentVariable("ConfigurationFilePath", $destinationPath)
Write-Host "##vso[task.setvariable variable=ConfigurationFilePath;]$destinationPath"
