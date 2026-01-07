[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][string]$Instance,
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$PersonalAccessToken,
    [Parameter(Mandatory=$true)][string]$Repository,
    [Parameter(Mandatory=$true)][string]$Branch = "master",
    [Parameter(Mandatory=$true)][ValidateSet("production", "staging")][string]$Slot = "production",
    [Parameter(Mandatory=$true)][ValidateSet("Dev", "Int", "Prod")][string]$Environment,
    [Parameter(Mandatory=$true)][ValidateSet("USNC", "USSC", "USNC-PREVIEW", "USSC-PREVIEW", "USNC-ASE", "USSC-ASE")][string]$Region
)

Write-Host "Importing configuration for Gallery Functional tests on $Environment $Region"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$basicAuth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f 'PAT', $PersonalAccessToken)))
$headers = @{ Authorization = ("Basic {0}" -f $basicAuth) }

Import-Module "$PSScriptRoot\TestUtilities.psm1" -Force

# Download the config files--common, per environment, and per region--and merge them into a single file
$configObject = New-Object PSObject
"Common", $Environment, "$Environment-$Region" | `
    ForEach-Object {
        $filename = "$_.json"
        $file = "$PSScriptRoot\temp-$filename"
        Write-Host "Downloading temporary configuration file $filename"
        $requestUri = "https://$Instance.visualstudio.com/DefaultCollection/$Project/_apis/git/repositories/$Repository/items?api-version=1.0&versionDescriptor.version=$Branch&scopePath=GalleryFunctionalConfig\$filename"
        Invoke-WebRequest -UseBasicParsing -Uri $requestUri -Headers $headers -OutFile $file | Out-Null
        $configData = Get-Content -Path $file | ConvertFrom-Json
        Remove-Item -Path $file
        # Merge the current file with the last files
        Merge-Objects -Source $configData -Target $configObject
    }

# Add a field to the file determining which slot should be tested
$configObject | Add-Member -MemberType NoteProperty -Name "Slot" -Value $Slot

# Save the file and set an environment variable to be used by the functional tests
$configurationFilePath = "$PSScriptRoot\Config-$Environment-$Region.json"
[Environment]::SetEnvironmentVariable("ConfigurationFilePath", $configurationFilePath)
Write-Host "##vso[task.setvariable variable=ConfigurationFilePath;]$configurationFilePath"
ConvertTo-Json $configObject | Out-File $configurationFilePath