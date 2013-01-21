param(
	[Parameter(Mandatory=$false)][string]$StorageAccountName = "nugetgallerydev",
	[Parameter(Mandatory=$false)][string]$Container = "deployment-packages",
    [Parameter(Mandatory=$false)][string]$PackageFile = $null,
    [Parameter(Mandatory=$false)][string]$AzureSdkPath = $null
)

# Import common stuff
$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

$items = @(dir "$ScriptRoot\..\_AzurePackage\NuGetGallery_*.cspkg");
if($items.Length -eq 0) {
    throw "No packages found in _AzurePackage folder"
}

if($items.Length -eq 1) {
    $PackageFilePath = $items[0]
}
else {
    $PackageFilePath = SelectOrUseProvided $PackageFile $items  { $true } "Package" { 
        $_.Name.Substring("NuGetGallery_".Length, $_.Name.Length - "NuGetGallery_".Length - ".cspkg".Length)
    }
}
Write-Host "Pushing $PackageFilePath to $Container on $StorageAccountName"

$AzureSdkPath = Get-AzureSdkPath $AzureSdkPath

[System.Reflection.Assembly]::LoadFrom("$AzureSdkPath\bin\Microsoft.WindowsAzure.StorageClient.dll") | Out-Null

$StorageConnectionString = Get-StorageAccountConnectionString $StorageAccountName
$Account = [Microsoft.WindowsAzure.CloudStorageAccount]::Parse($StorageConnectionString)
$BlobClient = [Microsoft.WindowsAzure.StorageClient.CloudStorageAccountStorageClientExtensions]::CreateCloudBlobClient($Account)
$ContainerRef = $BlobClient.GetContainerReference($Container);
$ContainerRef.CreateIfNotExist() | Out-Null
$Blob = $ContainerRef.GetBlockBlobReference($PackageFilePath.Name)
Write-Host "** Uploading Blob" -ForegroundColor Black -BackgroundColor Green
$Blob.UploadFile($PackageFilePath.FullName)