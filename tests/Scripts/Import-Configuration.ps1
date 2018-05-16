[CmdletBinding()]
param (
    [string]$Instance,
    [string]$Project,
    [string]$PersonalAccessToken,
    [string]$Repository,
    [string]$Branch = "master",
    [ValidateSet("production", "staging")][string]$Slot = "production",
    [ValidateSet("Dev", "Int", "Prod")][string]$Environment,
    [ValidateSet("USNC", "USSC")][string]$Region
)

Write-Host "Importing configuration for Gallery Functional tests on $Environment $Region"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$basicAuth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f 'PAT', $PersonalAccessToken)))
$headers = @{ Authorization = ("Basic {0}" -f $basicAuth) }

Function Get-MergedObject {
    param(
        [PSObject]$source,
        [PSObject]$output
    )

    # For each property of the source object, add the property to the output object
    $source | `
        Get-Member -MemberType NoteProperty | `
        ForEach-Object {
            $name = $_.Name
            $value = $source."$name"
            $existingValue = $output."$name"
            if ($_.Definition.StartsWith("System.Management.Automation.PSCustomObject") -and $existingValue -ne $null) {
                # If the property is a nested object, merge the nested object in the source with the output object
                $value = Get-MergedObject -source $value -output $existingValue
            } else {
                # Add the property to the output object
                # If the property already exists on the output object, overwrite it
                $output | Add-Member -MemberType NoteProperty -Name $name -Value $value -Force
            }
        }
}

# Download the config files--common ("All"), per environment, and per region--and merge them into a single file
$configObject = New-Object PSObject
"All", $Environment, "$Environment-$Region" | ForEach-Object {
    $filename = "$_.json"
    $file = "temp-$filename"
    Write-Host "Downloading configuration file $filename"
    $requestUri = "https://$Instance.visualstudio.com/DefaultCollection/$Project/_apis/git/repositories/$Repository/items?api-version=1.0&versionDescriptor.version=$Branch&scopePath=GalleryFunctionalConfig\$filename"
    $response = Invoke-WebRequest -UseBasicParsing -Uri $requestUri -Headers $headers -OutFile $file
    $configData = Get-Content -Path $file | ConvertFrom-Json
    Remove-Item -Path $file
    # Merge the current file with the last files
    Get-MergedObject -source $configData -output $configObject
}

# Add a field to the file determining which slot should be tested
$configObject | Add-Member -MemberType NoteProperty -Name "Slot" -Value $Slot

# Save the file and set an environment variable to be used by the functional tests
$configurationFilePath = "$PSScriptRoot\Config-$Environment-$Region.json"
[Environment]::SetEnvironmentVariable("ConfigurationFilePath", $configurationFilePath)
Write-Host "##vso[task.setvariable variable=ConfigurationFilePath;]$configurationFilePath"
ConvertTo-Json $configObject | Out-File $configurationFilePath