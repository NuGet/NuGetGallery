[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][string]$Instance,
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$PersonalAccessToken,
    [Parameter(Mandatory=$true)][string]$Repository,
    [Parameter(Mandatory=$true)][string]$Branch = "master",
    [Parameter(Mandatory=$true)][string]$ConfigurationName,
    [Parameter(Mandatory=$true)][ValidateSet("production", "staging")][string]$Slot = "production"
)

Write-Host "Importing configuration for Azure Search Functional tests for '$ConfigurationName'"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$basicAuth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f 'PAT', $PersonalAccessToken)))
$headers = @{ Authorization = ("Basic {0}" -f $basicAuth) }

$filename = "$ConfigurationName.json"
$destinationDirectory = Join-Path $PSScriptRoot "..\NuGet.Services.AzureSearch.FunctionalTests\ExternalConfig"
$destinationPath = Join-Path $destinationDirectory $filename
if (-not (Test-Path $destinationDirectory)) {
    New-Item -Path $destinationDirectory -ItemType "directory"
}

$tempFileName = "temp-$filename"
$tempDestinationPath = Join-Path $destinationDirectory $tempFileName

Write-Host "Downloading temporary configuration file '$filename' to '$tempDestinationPath'"
$requestUri = "https://$Instance.visualstudio.com/DefaultCollection/$Project/_apis/git/repositories/$Repository/items?api-version=1.0&versionDescriptor.version=$Branch&scopePath=SearchFunctionalConfig\$filename"
$response = Invoke-WebRequest -UseBasicParsing -Uri $requestUri -Headers $headers -OutFile $tempDestinationPath
$configObject = Get-Content -Path $tempDestinationPath | ConvertFrom-Json
Remove-Item -Path $tempDestinationPath

# Add a field to the file determining which slot should be tested
$configObject | Add-Member -MemberType NoteProperty -Name "Slot" -Value $Slot

# Save the file and set an environment variable to be used by the functional tests
Write-Host "Writing configuration file with updated values: $destinationPath"
ConvertTo-Json $configObject | Out-File $destinationPath
[Environment]::SetEnvironmentVariable("ConfigurationFilePath", $destinationPath)
Write-Host "##vso[task.setvariable variable=ConfigurationFilePath;]$destinationPath"