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
    # If the slot is 'Production', then we can assume that we are testing the same url as the service root. 
    $GalleryUrl = $ServiceRoot
}
else
{
    # Use Azure PowerShell cmdlets to find the url of the desired slot
    Try
    {
        Write-Host "Logging into Azure as service principal."
        # Log into Azure using a service principal with a certificate
        $login = Add-AzureRmAccount -ApplicationId "$ApplicationId" -CertificateThumbprint "$AzureCertificateThumbprint" -ServicePrincipal -SubscriptionId "$SubscriptionId" -TenantId "$TenantId"
        # Get the resource group name of the cloud service resource
        $resourceGroupName = (Find-AzureRmResource -ResourceNameEquals "$CloudServiceName").ResourceGroupName
        # Use the resource group name to construct the id of the slot resource
        $slotResource = Get-AzureRmResource -Id "/subscriptions/$SubscriptionId/resourceGroups/$resourceGroupName/providers/Microsoft.ClassicCompute/domainNames/$CloudServiceName/slots/$Slot"
        # Get the uri of the slot resource
        $GalleryUrl = ($slotResource.Properties.uri).Replace("http", "https")
    }
    Catch [System.Exception]
    {
        Write-Host "Failed to retrieve URL for testing!"
        Write-Host $_.Exception.Message
        Exit 1
    }
}

Write-Host "Using the following GalleryURL: " $GalleryUrl
$env:GalleryUrl = $GalleryUrl
Write-Host "##vso[task.setvariable variable=GalleryUrl;]$GalleryUrl"