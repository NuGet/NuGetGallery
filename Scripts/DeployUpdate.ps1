   $subscriptionId        ="8282d469-4751-4e49-8ae6-86a405669429"
   $certificateThumbprint = "08E40392D848313AFBC3EC7D4A8CA462EBADCF8C"
$StorageAccountName = "nugetgallerydev"
$TargetSubscription = "8282d469-4751-4e49-8ae6-86a405669429"
$SourceBlob = $null
$TargetService = "nugetgallery-bvts"
$Configuration = "D:\Office\SourceCode\NugetGallops_Git\NuGetGallery.Preview.xml"
$AzureSdkPath = "C:\Program Files\Microsoft SDKs\Windows Azure\.NET SDK\2012-10"
$Slot = "Staging"
$DeploymentName = "AutoDeployment"
# Import common stuff
$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

$certificate = (get-item cert:\CurrentUser\MY\$certificateThumbprint)

Set-AzureSubscription -SubscriptionName "nugetbvt" -SubscriptionId $subscriptionId -Certificate $certificate
Select-AzureSubscription "nugetbvt"

if(!$SourceBlob) {
    # Get a list of available packages
    [System.Reflection.Assembly]::LoadFrom("$AzureSdkPath\bin\Microsoft.WindowsAzure.StorageClient.dll") | Out-Null
    $StorageConnectionString = Get-StorageAccountConnectionString $StorageAccountName
    $Account = [Microsoft.WindowsAzure.CloudStorageAccount]::Parse($StorageConnectionString)
    $BlobClient = [Microsoft.WindowsAzure.StorageClient.CloudStorageAccountStorageClientExtensions]::CreateCloudBlobClient($Account)
    $ContainerRef = $BlobClient.GetContainerReference("deployment-packages");
    $BlobRef = SelectOrUseProvided $CommitId ($ContainerRef.ListBlobs()) { ([Microsoft.WindowsAzure.StorageClient.CloudBlockBlob]$_).Name -like "NuGetGallery_*.cspkg" } "Deployable Packages" {
        $blob = ([Microsoft.WindowsAzure.StorageClient.CloudBlockBlob]$_)
        $blob.Name.Substring("NuGetGallery_".Length, $blob.Name.Length - "NuGetGallery_".Length - ".cspkg".Length);
    }
    $SourceBlob = $BlobRef.Uri
    $hash = $BlobRef.Name.Substring("NuGetGallery_".Length, $BlobRef.Name.Length - "NuGetGallery_".Length - ".cspkg".Length);
    $DateName = (Get-Date -format "MMMdd @ HHmm")
    $DeploymentName = "$DateName ($($hash.Substring(0,10)))"
}

# Select the Subscription
# $Subscription = SelectOrUseProvided $TargetSubscription (Get-AzureSubscription) { $true } "Subscription" { $_.SubscriptionName }
# Write-Host "** Target Subscription: $($Subscription.SubscriptionName)" -ForegroundColor Black -BackgroundColor Green
# Select-AzureSubscription $Subscription.SubscriptionName
Set-AzureSubscription -SubscriptionName "nugetbvt" -CurrentStorageAccount $StorageAccountName

# Select the Cloud Service
$Service = SelectOrUseProvided $TargetService (Get-AzureService) { !$_.ServiceName.EndsWith("ops", "OrdinalIgnoreCase") } "Service" { $_.ServiceName }
Write-Host "** Target Service: $($Service.ServiceName)" -ForegroundColor Black -BackgroundColor Green

Write-Host "Deploying with the following parameters: "
Write-Host "* Target Cloud Service = $($Service.ServiceName)"
Write-Host "* Target Configuration = $Configuration"
Write-Host "* Site Package = $SourceBlob"
Write-Host "* Deployment Name = $DeploymentName"
$result = YesNoPrompt "Perform Deployment? [Y/n]"

# Final Guard
if($Service.ServiceName -eq "nugetgallery") {
    throw "No way dude, you can't auto-deploy Production. Sorry."
}

New-AzureDeployment -ServiceName $Service.ServiceName -Package $SourceBlob -Configuration $Configuration -Slot $Slot -Label $DeploymentName