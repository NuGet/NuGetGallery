param(
    [Parameter(Mandatory=$false)][string]$StorageAccountName = $null,
    [Parameter(Mandatory=$false)][string]$TargetSubscription = $null,
    [Parameter(Mandatory=$false)][string]$SourceBlob = $null,
    [Parameter(Mandatory=$false)][string]$TargetService = $null,
    [Parameter(Mandatory=$true)][string]$Configuration = $null,
    [Parameter(Mandatory=$false)][string]$AzureSdkPath = $null,
    [Parameter(Mandatory=$true)][string]$Slot = $null,
    [Parameter(Mandatory=$false)][string]$DeploymentName = "AutoDeployment")
# Import common stuff
$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

# Set some defaults for microsofities
if([String]::Equals([Environment]::UserDomainName, "REDMOND", "OrdinalIgnoreCase")) {
    # These defaults don't give anyone access to our secrets, but they make it easier to deal with our defaults
    if(!$StorageAccountName) {
        $StorageAccountName = "nugetgallerydev"
    }
}

$AzureSdkPath = Get-AzureSdkPath $AzureSdkPath

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
$Subscription = SelectOrUseProvided $TargetSubscription (Get-AzureSubscription) { $true } "Subscription" { $_.SubscriptionName }
Write-Host "** Target Subscription: $($Subscription.SubscriptionName)" -ForegroundColor Black -BackgroundColor Green
Select-AzureSubscription $Subscription.SubscriptionName
Set-AzureSubscription -SubscriptionName $Subscription.SubscriptionName -CurrentStorageAccount $StorageAccountName

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