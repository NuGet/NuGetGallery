param(
    [Parameter(Mandatory=$true)][string]$ConfigurationsPath,
    [Parameter(Mandatory=$true)][ValidateSet("production", "staging")][string]$Slot = "production",
    [Parameter(Mandatory=$true)][ValidateSet("Dev", "Int", "Prod")][string]$Environment,
    [Parameter(Mandatory=$true)][ValidateSet("USNC", "USSC", "USNC-PREVIEW", "USSC-PREVIEW", "USNC-ASE", "USSC-ASE")][string]$Region,
    [Parameter(Mandatory)][string[]]$TestCategories
)

Import-Module "$PSScriptRoot\TestUtilities.psm1" -Force

$configObject = New-Object PSObject
"Common", $Environment, "$Environment-$Region" | `
    ForEach-Object {
        $file = "$ConfigurationsPath\$_.json"
        if (-not (Test-Path $file)) {
            Write-Error "Missing configuration file: $file"
            exit 1
        }
        $configData = Get-Content -Path $file | ConvertFrom-Json
        # Merge the current file with the last files
        Merge-Objects -Source $configData -Target $configObject
    }

$configObject | Add-Member -MemberType NoteProperty -Name "Slot" -Value $Slot
$configurationFilePath = "$PSScriptRoot\Config-$Environment-$Region.json"
ConvertTo-Json $configObject | Out-File $configurationFilePath

$env:ConfigurationFilePath = $configurationFilePath
& $PSScriptRoot\RunGalleryFunctionalTests.ps1 -TestCategories ($TestCategories -join ";")