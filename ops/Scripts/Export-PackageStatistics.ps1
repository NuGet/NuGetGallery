param(
  $connectionString = $env:NUGET_GALLERY_SQL_AZURE_CONNECTION_STRING,
  $storageName = $env:NUGET_GALLERY_AZURE_STORAGE_ACCOUNT_NAME,
  $storageKey = $env:NUGET_GALLERY_AZURE_STORAGE_ACCESS_KEY
)

$scriptDir = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $scriptDir\Require-Param.ps1

require-param -value $connectionString -paramName "connectionString"
require-param -value $storageName -paramName "storageName"
require-param -value $storageKey -paramName "storageKey"

$galopsExe = join-path $scriptDir "..\OpsExe\bin\Debug\galops.exe"

& "$galopsExe" /task:pps /connectionstring:$connectionString /storagename:$storageName /storagekey:$storageKey