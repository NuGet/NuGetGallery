[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [string]$StorageAccountName,
    [string]$StorageAccountKey,
    [string]$Environment
)

Write-Host "Uploading static $Environment gallery content to $StorageAccountName."

[System.Reflection.Assembly]::LoadFrom("C:\Program Files\Microsoft SDKs\Azure\.NET SDK\v2.9\bin\Microsoft.WindowsAzure.StorageClient.dll")

$account = [Microsoft.WindowsAzure.CloudStorageAccount]::Parse("DefaultEndpointsProtocol=https;AccountName=$StorageAccountName;AccountKey=$StorageAccountKey")
$client = [Microsoft.WindowsAzure.StorageClient.CloudStorageAccountStorageClientExtensions]::CreateCloudBlobClient($account)

$files = Get-ChildItem ".\content\$Environment"
foreach ($file in $files) {
	$blob = $client.GetBlockBlob("content/$file")
	try {
		$snappy = $blob.CreateSnapshot()
		Write-Host "Created snapshot of existing 'content/$file'."
	} catch {}
	$blob.UploadFile($file.FullName)
	Write-Host "Uploaded 'content/$file'."
}