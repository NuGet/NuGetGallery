param(
  $azureStorageAccessKey              = $env:NUGET_GALLERY_AZURE_STORAGE_ACCESS_KEY,
  $azureStorageAccountName            = $env:NUGET_GALLERY_AZURE_STORAGE_ACCOUNT_NAME,
  $azureStorageBlobUrl                = $env:NUGET_GALLERY_AZURE_STORAGE_BLOB_URL,
  $remoteDesktopAccountExpiration     = $env:NUGET_GALLERY_REMOTE_DESKTOP_ACCOUNT_EXPIRATION,
  $remoteDesktopCertificateThumbprint = $env:NUGET_GALLERY_REMOTE_DESKTOP_CERTIFICATE_THUMBPRINT,
  $remoteDesktopEnctyptedPassword     = $env:NUGET_GALLERY_REMOTE_DESKTOP_ENCRYPTED_PASSWORD,
  $remoteDesktopUsername              = $env:NUGET_GALLERY_REMOTE_DESKTOP_USERNAME,
  $sqlAzureConnectionString           = $env:NUGET_GALLERY_SQL_AZURE_CONNECTION_STRING,
  $sslCertificateThumbprint           = $env:NUGET_GALLERY_SSL_CERTIFICATE_THUMBPRINT,
  $validationKey                      = $env:NUGET_GALLERY_VALIDATION_KEY,
  $decryptionKey                      = $env:NUGET_GALLERY_DECRYPTION_KEY,
  $commitSha,
  $commitBranch
)

#Import Common Stuff
$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

#Validate Sutff
require-param -value $azureStorageAccessKey -paramName "azureStorageAccessKey"
require-param -value $azureStorageAccountName -paramName "azureStorageAccountName"
require-param -value $azureStorageBlobUrl -paramName "azureStorageBlobUrl"
require-param -value $remoteDesktopAccountExpiration -paramName "remoteDesktopAccountExpiration"
require-param -value $remoteDesktopCertificateThumbprint -paramName "remoteDesktopCertificateThumbprint"
require-param -value $remoteDesktopEnctyptedPassword -paramName "remoteDesktopEnctyptedPassword"
require-param -value $remoteDesktopUsername -paramName "remoteDesktopUsername"
require-param -value $sqlAzureConnectionString -paramName "sqlAzureConnectionString"
require-param -value $sslCertificateThumbprint -paramName "sslCertificateThumbprint"

#Helper Functions
function set-certificatethumbprint {
  param($path, $name, $value)
  $xml = [xml](get-content $path)
  $certificate = $xml.serviceconfiguration.role.Certificates.Certificate | where { $_.name -eq $name }
  $certificate.thumbprint = "$value"
  $resolvedPath = resolve-path($path) 
  $xml.save($resolvedPath)
} 

function set-configurationsetting {
  param($path, $name, $value)
  $xml = [xml](get-content $path)
  $setting = $xml.serviceconfiguration.role.configurationsettings.setting | where { $_.name -eq $name }
  $setting.value = "$value"
  $resolvedPath = resolve-path($path) 
  $xml.save($resolvedPath)
}

function set-connectionstring {
  param($path, $name, $value)
  $settings = [xml](get-content $path)
  $setting = $settings.configuration.connectionStrings.add | where { $_.name -eq $name }
  $setting.connectionString = "$value"
  $setting.providerName = "System.Data.SqlClient"
  $resolvedPath = resolve-path($path) 
  $settings.save($resolvedPath)
}

function set-appsetting {
    param($path, $name, $value)
    $settings = [xml](get-content $path)
    $setting = $settings.configuration.appSettings.selectsinglenode("add[@key='" + $name + "']")
    $setting.value = $value.toString()
    $resolvedPath = resolve-path($path) 
    $settings.save($resolvedPath)
}

function set-releasemode {
  param($path)
  $xml = [xml](get-content $path)
  $compilation = $xml.configuration."system.web".compilation
  $compilation.debug = "false"
  $resolvedPath = resolve-path($path) 
  $xml.save($resolvedPath)
}

function set-machinekey {
    param($path)
    if($validationKey -AND $decryptionKey){
        $xml = [xml](get-content $path)
        $machinekey = $xml.CreateElement("machineKey")
        $machinekey.setattribute("validation", "HMACSHA256")
        $machinekey.setattribute("validationKey", $validationKey)
        $machinekey.setattribute("decryption", "AES")
        $machinekey.setattribute("decryptionKey", $decryptionKey)       
        $xml.configuration."system.web".AppendChild($machineKey)
        $resolvedPath = resolve-path($path) 
        $xml.save($resolvedPath)
    }
}

#Do Work Brah
$scriptPath = split-path $MyInvocation.MyCommand.Path
$rootPath = resolve-path(join-path $scriptPath "..")
$csdefFile = join-path $scriptPath "NuGetGallery.csdef"
$websitePath = join-path $rootPath "Website"
$webConfigPath = join-path $websitePath "Web.config"
$webConfigBakPath = join-path $scriptPath "Web.config.bak"
$rolePropertiesPath = join-path $scriptPath "NuGetGallery.RoleProperties.txt"
$cscfgPath = join-path $scriptPath "NuGetGallery.cscfg"
$cscfgBakPath = join-path $scriptPath "NuGetGallery.cscfg.bak"
$cspkgFolder = join-path $rootPath "_AzurePackage"
$cspkgFile = join-path $cspkgFolder "NuGetGallery.cspkg"
$gitPath = join-path (programfiles-dir) "Git\bin\git.exe"
$compressionCmdScriptsPath = join-path $scriptPath "EnableDynamicHttpCompression.cmd"
$binPath = join-path $websitePath "bin"
$compressionCmdBinPath = join-path $binPath "EnableDynamicHttpCompression.cmd"

if ($commitSha -eq $null) {
    $commitSha = (& "$gitPath" rev-parse HEAD)
}

if ($commitBranch -eq $null) {
    $commitBranch = (& "$gitPath" name-rev --name-only HEAD)
}

if ((test-path $cspkgFolder) -eq $false) {
  mkdir $cspkgFolder | out-null
}

cp $webConfigPath $webConfigBakPath
cp $cscfgPath $cscfgBakPath

set-configurationsetting -path $cscfgPath -name "AzureStorageAccessKey" -value $azureStorageAccessKey
set-configurationsetting -path $cscfgPath -name "AzureStorageAccountName" -value $azureStorageAccountName
set-configurationsetting -path $cscfgPath -name "AzureStorageBlobUrl" -value $azureStorageBlobUrl
set-configurationsetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountExpiration" -value $remoteDesktopAccountExpiration
set-certificatethumbprint -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.PasswordEncryption" -value $remoteDesktopCertificateThumbprint
set-configurationsetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountEncryptedPassword" -value $remoteDesktopEnctyptedPassword
set-configurationsetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountUsername" -value $remoteDesktopUsername
set-connectionstring -path $webConfigPath -name "NuGetGallery" -value $sqlAzureConnectionString
set-certificatethumbprint -path $cscfgPath -name "nuget.org" -value $sslCertificateThumbprint
set-releasemode $webConfigPath
set-machinekey $webConfigPath

#Release Tag stuff
print-message("Setting the release tags")
set-appsetting -path $webConfigPath -name "Gallery:ReleaseName" -value "NuGet 1.6 'Hershey'"
set-appsetting -path $webConfigPath -name "Gallery:ReleaseTime" -value (Get-Date -format "dd/MM/yyyy HH:mm:ss")
set-appsetting -path $webConfigPath -name "Gallery:ReleaseSha" -value $commitSha
set-appsetting -path $webConfigPath -name "Gallery:ReleaseBranch" -value $commitBranch

cp $compressionCmdScriptsPath $compressionCmdBinPath

& 'C:\Program Files\Windows Azure SDK\v1.6\bin\cspack.exe' "$csdefFile" /out:"$cspkgFile" /role:"Website;$websitePath" /sites:"Website;Web;$websitePath" /rolePropertiesFile:"Website;$rolePropertiesPath"

cp $cscfgPath $cspkgFolder

cp $webConfigBakPath $webConfigPath
cp $cscfgBakPath $cscfgPath
rm $compressionCmdBinPath

print-success("Azure package and configuration dropped to $cspkgFolder.")
write-host ""

Exit 0
