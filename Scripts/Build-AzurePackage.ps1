param(
  $azureStorageAccessKey              = $env:NUGET_GALLERY_AZURE_STORAGE_ACCESS_KEY,
  $azureStorageAccountName            = $env:NUGET_GALLERY_AZURE_STORAGE_ACCOUNT_NAME,
  $azureStorageBlobUrl                = $env:NUGET_GALLERY_AZURE_STORAGE_BLOB_URL,
  $remoteDesktopAccountExpiration     = $env:NUGET_GALLERY_REMOTE_DESKTOP_ACCOUNT_EXPIRATION,
  $remoteDesktopCertificateThumbprint = $env:NUGET_GALLERY_REMOTE_DESKTOP_CERTIFICATE_THUMBPRINT,
  $remoteDesktopEnctyptedPassword     = $env:NUGET_GALLERY_REMOTE_DESKTOP_ENCRYPTED_PASSWORD,
  $remoteDesktopUsername              = $env:NUGET_GALLERY_REMOTE_DESKTOP_USERNAME,
  $smtpHost                           = $env:NUGET_GALLERY_SMTP_HOST,
  $smtpPassword                       = $env:NUGET_GALLERY_SMTP_PASSWORD,
  $smtpPort                           = $env:NUGET_GALLERY_SMTP_PORT,
  $smtpUsername                       = $env:NUGET_GALLERY_SMTP_USERNAME,
  $sqlAzureConnectionString           = $env:NUGET_GALLERY_SQL_AZURE_CONNECTION_STRING,
  $sslCertificateThumbprint           = $env:NUGET_GALLERY_SSL_CERTIFICATE_THUMBPRINT
)

function require-param {
  param($value, $paramName)
  
  if ($value -eq $null) {
    write-error "The parameter -$paramName is required."
    exit
  }
}

require-param -value $azureStorageAccessKey -paramName "azureStorageAccessKey"
require-param -value $azureStorageAccountName -paramName "azureStorageAccountName"
require-param -value $azureStorageBlobUrl -paramName "azureStorageBlobUrl"
require-param -value $remoteDesktopAccountExpiration -paramName "remoteDesktopAccountExpiration"
require-param -value $remoteDesktopCertificateThumbprint -paramName "remoteDesktopCertificateThumbprint"
require-param -value $remoteDesktopEnctyptedPassword -paramName "remoteDesktopEnctyptedPassword"
require-param -value $remoteDesktopUsername -paramName "remoteDesktopUsername"
require-param -value $smtpHost -paramName "smtpHost"
require-param -value $smtpPassword -paramName "smtpPassword"
require-param -value $smtpPort -paramName "smtpPort"
require-param -value $smtpUsername -paramName "smtpUsername"
require-param -value $sqlAzureConnectionString -paramName "sqlAzureConnectionString"
require-param -value $sslCertificateThumbprint -paramName "sslCertificateThumbprint"
  
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
$projFile = join-path $scriptPath NuGetGallery.msbuild
 
& "$(get-content env:windir)\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" $projFile /p:Configuration=Release /t:CIBuild

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
set-configurationsetting -path $cscfgPath -name "SmtpHost" -value $smtpHost
set-configurationsetting -path $cscfgPath -name "SmtpPassword" -value $smtpPassword
set-configurationsetting -path $cscfgPath -name "SmtpPort" -value $smtpPort
set-configurationsetting -path $cscfgPath -name "SmtpUsername" -value $smtpUsername
set-connectionstring -path $webConfigPath -name "NuGetGallery" -value $sqlAzureConnectionString
set-certificatethumbprint -path $cscfgPath -name "nuget.org" -value $sslCertificateThumbprint

& 'C:\Program Files\Windows Azure SDK\v1.5\bin\cspack.exe' "$csdefFile" /out:"$cspkgFile" /role:"Website;$websitePath" /sites:"Website;Web;$websitePath" /rolePropertiesFile:"Website;$rolePropertiesPath"

cp $cscfgPath $cspkgFolder

cp $webConfigBakPath $webConfigPath
cp $cscfgBakPath $cscfgPath

write-host "Azure package and configuration dropped to $cspkgFolder."
write-host ""