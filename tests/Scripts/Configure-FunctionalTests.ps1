[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [string]$ServiceRoot,
    [string]$Slot,
    [string]$CloudServiceName,
    [string]$SubscriptionId,
    [string]$ApplicationId,
    [string]$TenantId,
    [string]$AzureCertificateThumbprint
)

# Delete leftover tests
Get-ChildItem "$PSScriptRoot\.." -Recurse | Where-Object {$_.Extension -eq '.trx' -Or $_.Name -match 'functionaltests.*.xml'} | ForEach-Object {
    Remove-Item "$($_.FullName)"
}

# Determine the url to run the tests against
if ($Slot -eq "Production")
{
    $GalleryUrl = $ServiceRoot
}
else
{
    # Use Azure PowerShell cmdlets to find the url of the desired slot
    try
    {
        Write-Host "Logging into Azure as service principal."
        Add-AzureRmAccount -ApplicationId "$ApplicationId" -CertificateThumbprint "$AzureCertificateThumbprint" -ServicePrincipal -SubscriptionId "$SubscriptionId" -TenantId "$TenantId"
        Write-Host "Fetching url of $Slot slot of $CloudServiceName."
        Set-AzureRmContext -SubscriptionId "$SubscriptionId" -TenantId "$TenantId"
        $GalleryUrl = (Get-AzureDeployment -ServiceName "$CloudServiceName" -Slot "$Slot").Url
    }
    catch [System.Exception]
    {
        Write-Host "Failed to retrieve URL for testing!"
        Write-Host $_.Exception.Message
        Exit 1
    }
}

Write-Host "Using the following GalleryURL: " $GalleryUrl
$env:GalleryUrl = $GalleryUrl
Write-Host "##vso[task.setvariable variable=GalleryUrl;]$GalleryUrl"