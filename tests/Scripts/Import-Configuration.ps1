[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][string]$Instance,
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$PersonalAccessToken,
    [Parameter(Mandatory=$true)][string]$Repository,
    [Parameter(Mandatory=$true)][string]$Branch = "master",
    [Parameter(Mandatory=$true)][string]$ConfigurationName
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

Write-Host "Downloading temporary configuration file '$filename' to '$destinationPath'"
$requestUri = "https://$Instance.visualstudio.com/DefaultCollection/$Project/_apis/git/repositories/$Repository/items?api-version=1.0&versionDescriptor.version=$Branch&scopePath=SearchFunctionalConfig\$filename"
Invoke-WebRequest -UseBasicParsing -Uri $requestUri -Headers $headers -OutFile $destinationPath