param(
    [Parameter(Mandatory=$true)][string]$StorageAccountName = $null,
    [Parameter(Mandatory=$true)][string]$subscriptionId = $null,     
    [Parameter(Mandatory=$true)][string]$Configuration = $null,
    [Parameter(Mandatory=$true)][string]$AzureSdkPath = $null,
    [Parameter(Mandatory=$true)][string]$AzurePowerShellPath = $null,        
    [Parameter(Mandatory=$true)][string]$certificateThumbprint=$null,
    [Parameter(Mandatory=$false)][string]$branch = "dev",
    [Parameter(Mandatory=$false)][string]$Slot = "Production",
    [Parameter(Mandatory=$false)][string]$DeploymentName = "AutoDeployment")

Import-Module $AzurePowerShellPath

#Get the certificates and subscription
$certificate = (Get-Item cert:\LocalMachine\MY\$certificateThumbprint)
Set-AzureSubscription -SubscriptionName "nugetbvt" -SubscriptionId $subscriptionId -Certificate $certificate
Select-AzureSubscription "nugetbvt"

if(!$SourceBlob) {
    #Get the storage account
    [System.Reflection.Assembly]::LoadFrom("$AzureSdkPath\bin\Microsoft.WindowsAzure.StorageClient.dll") | Out-Null
    $StorageAccountKeyContext = Get-AzureStorageKey  –StorageAccountName $StorageAccountName
    $StorageConnectionString =  "DefaultEndpointsProtocol=https;AccountName=$($StorageAccountName);AccountKey=$($StorageAccountKeyContext.Primary)";
    # Get a list of available packages
    $Account = [Microsoft.WindowsAzure.CloudStorageAccount]::Parse($StorageConnectionString)
    $BlobClient = [Microsoft.WindowsAzure.StorageClient.CloudStorageAccountStorageClientExtensions]::CreateCloudBlobClient($Account)
    $ContainerRef = $BlobClient.GetContainerReference("deployment-packages");
    $allItems = $ContainerRef.ListBlobs(); 
    # Get the specific container related to dev packages  
    $devBlobsDirectory =  @($allItems | Where-Object { ([Microsoft.WindowsAzure.StorageClient.CloudBlobDirectory]$_).Uri -like  '*' +$branch +'*' })    
    # Get the list of all Blobs from the dev directory
    $bro = New-Object Microsoft.WindowsAzure.StorageClient.BlobRequestOptions
    $bro.UseFlatBlobListing = $true
    $devBlobs = $devBlobsDirectory.ListBlobs($bro);   
    # For each blob fetch the attributes ( whichout this we get a crazy error when type casting from IListBlobItem to CloudBlockBlob
    foreach( $item in $debBlobs)
    {
     ([Microsoft.WindowsAzure.StorageClient.CloudBlockBlob]$item).FetchAttributes($bro)
    }
    # Get the latest package based on its time stamp
    [Microsoft.WindowsAzure.StorageClient.CloudBlockBlob] $latestBlob =  ([Microsoft.WindowsAzure.StorageClient.CloudBlockBlob]$devBlobs[0]) ;
    foreach( $element in $devBlobs)
    {
       if(([Microsoft.WindowsAzure.StorageClient.CloudBlockBlob]$element).Properties.LastModifiedUtc -gt ([Microsoft.WindowsAzure.StorageClient.CloudBlockBlob]$latestBlob).Properties.LastModifiedUtc)
       {
            $latestBlob = $element
       }
    }
    
    # Get the commit hash and set it as deployment name
    $SourceBlob = $latestBlob.Uri
    $hash = $latestBlob.Name.Substring("NuGetGallery_".Length, $latestBlob.Name.Length - "NuGetGallery_".Length - ".cspkg".Length);
    $DateName = (Get-Date -format "MMMdd @ HHmm")
    $DeploymentName = "$DateName ($($hash.Substring(0,10)))"
}

# Select the Subscription
Set-AzureSubscription -SubscriptionName "nugetbvt" -CurrentStorageAccount $StorageAccountName
# target service is set specifically to bvt service so that we don't end up re-deploying other environments by mistake.
$TargetService = "nugetgallery-bvts"

Write-Host "Deploying with the following parameters: "
Write-Host "* Target Cloud Service = $TargetService "
Write-Host "* Target Configuration = $Configuration"
Write-Host "* Site Package = $SourceBlob"
Write-Host "* Deployment Name = $DeploymentName"

Set-AzureDeployment -Upgrade -ServiceName $TargetService -Mode Auto -Package $SourceBlob -Configuration $Configuration -Slot $Slot -Label $DeploymentName