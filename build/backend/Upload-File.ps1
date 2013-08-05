param(
  $storageName = $env:NUGET_GALLERY_AZURE_STORAGE_ACCOUNT_NAME,
  $storageKey = $env:NUGET_GALLERY_AZURE_STORAGE_ACCESS_KEY,
  $filename,
  $contentType
)

$scriptDir = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $scriptDir\Require-Param.ps1

require-param -value $storageName -paramName "storageName"
require-param -value $storageKey -paramName "storageKey"
require-param -value $filename -paramName "filename"
require-param -value $contentType -paramName "contentType"

$galopsExe = join-path $scriptDir "..\OpsExe\bin\Debug\galops.exe"

& "$galopsExe" /task:UploadFile /storagename:$storageName /storagekey:$storageKey /filename:$filename /contentType:$contentType



