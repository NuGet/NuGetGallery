param(
    [Parameter(Mandatory=$true)][string]$StorageAccountName = $null,
    [Parameter(Mandatory=$true)][string]$subscriptionId = $null,     
    [Parameter(Mandatory=$true)][string]$Configuration = $null,      
    [Parameter(Mandatory=$true)][string]$certificateThumbprint=$null,
    [Parameter(Mandatory=$false)][string]$Slot="Production",
   )


#Get the certificates and subscription
$certificate = (Get-Item cert:\CurrentUser\MY\$certificateThumbprint)
Set-AzureSubscription -SubscriptionName "nugetbvt" -SubscriptionId $subscriptionId -Certificate $certificate
Select-AzureSubscription "nugetbvt"

# Select the Subscription
Set-AzureSubscription -SubscriptionName "nugetbvt" -CurrentStorageAccount $StorageAccountName
# target service is set specifically to bvt service so that we don't end up re-deploying other environments by mistake.
$TargetService = "nugetgallery-bvts"

Set-AzureDeployment -Config -ServiceName $TargetService -Configuration $Configuration -Slot $Slot