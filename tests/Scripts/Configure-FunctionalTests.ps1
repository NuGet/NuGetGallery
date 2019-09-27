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

Write-Host "Using the following SearchServiceUrl: " $ServiceRoot
$env:SearchServiceUrl = $ServiceRoot
Write-Host "##vso[task.setvariable variable=SearchServiceUrl;]$SearchServiceUrl"