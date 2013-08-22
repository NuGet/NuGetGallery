param(
  $connectionString = $env:NUGET_GALLERY_SQL_AZURE_CONNECTION_STRING
)

$scriptDir = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $scriptDir\Require-Param.ps1

$continue = read-host "Curate Windows8 packages on $($connectionString)? (y/n)"
if ($continue -ne "y") {
  write-host "Did not answer 'y'; exiting."
  exit 1
}

require-param -value $connectionString -paramName "connectionString"

$galopsExe = join-path $scriptDir "..\OpsExe\bin\Debug\galops.exe"

& "$galopsExe" /task:curatewindows8packages /connectionstring:$connectionString