param(
	[string]$File
	[string]$ConnectionString,
	[string]$Container,
	[string]$Path)

$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$asm = (dir $ScriptPath\..\packages\WindowsAzure.Storage.*\lib\net40\Microsoft.WindowsAzure.Storage.dll | select -first 1 -expand FullName)
[Assembly]::LoadFrom($asm);

$account = [Microsoft.WindowsAzure.Storage.CloudStorageAccount]::Parse($ConnectionString);
$client = $account.CreateCloudBlobClient()
$container = $client.GetContainerReference($Container);

if($Path && !$Path.EndsWith("/")) {
  $Path += "/";
}

$blobName = $Path + $File;
$blob = $container.GetBlockBlobReference($blobName);

try {
    $strm = [IO.File]::OpenRead((Convert-Path $File));
    $blob.UploadFromStream($strm)
} finally {
    if($strm) {
        $strm.Dispose();
    }
}