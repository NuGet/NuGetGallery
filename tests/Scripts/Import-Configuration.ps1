[CmdletBinding()]
param (
    [string]$Instance,
    [string]$Project,
    [string]$PersonalAccessToken,
    [string]$Repository,
    [string]$Branch = "master",
    [ValidateSet("Dev", "Int", "Prod")][string]$Environment,
    [ValidateSet("USNC", "USSC")][string]$Region
)

Write-Host "Importing configuration for Gallery Functional tests on $Environment $Region"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$basicAuth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f 'PAT', $PersonalAccessToken)))
$headers = @{ Authorization = ("Basic {0}" -f $basicAuth) }

"All", $Environment, "$Environment-$Region" | ForEach-Object {
    $filename = "$_.json"
    Write-Host "Downloading configuration file $filename"
    $requestUri = "https://$Instance.visualstudio.com/DefaultCollection/$Project/_apis/git/repositories/$Repository/items?api-version=1.0&versionDescriptor.version=$Branch&scopePath=GalleryFunctionalConfig\$filename"
    $response = Invoke-WebRequest -UseBasicParsing -Uri $requestUri -Headers $headers -OutFile $filename
    $configData = Get-Content -Path $filename | ConvertFrom-Json
    $configProperties = $configData | `
        Get-Member -MemberType NoteProperty | `
        ForEach-Object {
            $name = $_.Name
            [Environment]::SetEnvironmentVariable($name, $configData."$name")
        }

    Remove-Item -Path $filename
}