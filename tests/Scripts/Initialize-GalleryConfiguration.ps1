[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][string]$SourceConfigDir,
    [Parameter(Mandatory=$true)][ValidateSet("production", "staging")][string]$Slot,
    [Parameter(Mandatory=$true)][ValidateSet("Dev", "Int", "Prod")][string]$Environment,
    [Parameter(Mandatory=$true)][ValidateSet("USNC", "USSC", "USNC-PREVIEW", "USSC-PREVIEW", "USNC-ASE", "USSC-ASE")][string]$Region
)

Write-Host "Initializing configuration for Gallery Functional tests on $Environment $Region"

Import-Module "$PSScriptRoot\TestUtilities.psm1" -Force

$configObject = New-Object PSObject
"Common", $Environment, "$Environment-$Region" | `
    ForEach-Object {
        $filename = "$_.json"
        $file = "$PSScriptRoot\temp-$filename"
        # Copy the configuration file from the specified directory
        Copy-Item (Join-Path $SourceConfigDir $filename) $file -Verbose
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
