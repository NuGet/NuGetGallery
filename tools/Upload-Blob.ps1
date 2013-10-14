param(
	[string]$File,
	[string]$ConnectionString,
	[string]$Container,
	[string]$Path)

$ScriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$asm = (dir $ScriptPath\..\packages\WindowsAzure.Storage.*\lib\net40\Microsoft.WindowsAzure.Storage.dll | select -first 1 -expand FullName)
[Reflection.Assembly]::LoadFrom($asm) | Out-Null

$account = [Microsoft.WindowsAzure.Storage.CloudStorageAccount]::Parse($ConnectionString);
$client = $account.CreateCloudBlobClient()
$containerRef = $client.GetContainerReference($Container)
$containerRef.CreateIfNotExists() | Out-Null

if($Path -and !$Path.EndsWith("/")) {
  $Path += "/";
}

$blobName = $Path + [IO.Path]::GetFileName($File)
$blob = $containerRef.GetBlockBlobReference($blobName)

try {
    $strm = [IO.File]::OpenRead((Convert-Path $File))
    Write-Host "Uploading..."
    $blob.UploadFromStream($strm)
    Write-Host "Uploaded!"
} finally {
    if($strm) {
        $strm.Dispose()
    }
}