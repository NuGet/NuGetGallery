param(
  $azureStorageAccessKey              = $env:NUGET_GALLERY_AZURE_STORAGE_ACCESS_KEY,
  $azureStorageAccountName            = $env:NUGET_GALLERY_AZURE_STORAGE_ACCOUNT_NAME,
  $azureStorageBlobUrl                = $env:NUGET_GALLERY_AZURE_STORAGE_BLOB_URL,
  $azureCdnHost                       = $env:NuGET_GALLERY_AZURE_CDN_HOST,
  $remoteDesktopAccountExpiration     = $env:NUGET_GALLERY_REMOTE_DESKTOP_ACCOUNT_EXPIRATION,
  $remoteDesktopCertificateThumbprint = $env:NUGET_GALLERY_REMOTE_DESKTOP_CERTIFICATE_THUMBPRINT,
  $remoteDesktopEnctyptedPassword     = $env:NUGET_GALLERY_REMOTE_DESKTOP_ENCRYPTED_PASSWORD,
  $remoteDesktopUsername              = $env:NUGET_GALLERY_REMOTE_DESKTOP_USERNAME,
  $sqlAzureConnectionString           = $env:NUGET_GALLERY_SQL_AZURE_CONNECTION_STRING,
  $azureStatisticsConnectionString    = $env:NUGET_WAREHOUSE_REPORTS_STORAGE,
  $sslCertificateThumbprint           = $env:NUGET_GALLERY_SSL_CERTIFICATE_THUMBPRINT,
  $validationKey                      = $env:NUGET_GALLERY_VALIDATION_KEY,
  $decryptionKey                      = $env:NUGET_GALLERY_DECRYPTION_KEY,
  $vmSize                             = $env:NUGET_GALLERY_AZURE_VM_SIZE,
  $googleAnalyticsPropertyId          = $env:NUGET_GALLERY_GOOGLE_ANALYTICS_PROPERTY_ID,
  $azureDiagStorageAccessKey          = $env:NUGET_GALLERY_AZURE_DIAG_STORAGE_ACCESS_KEY,
  $azureDiagStorageAccountName        = $env:NUGET_GALLERY_AZURE_DIAG_STORAGE_ACCOUNT_NAME,
  $facebookAppId                      = $env:NUGET_FACEBOOK_APP_ID,
  $cacheServiceEndpoint               = $env:NUGET_GALLERY_CACHE_SERVICE_ENDPOINT,
  $cacheServiceAccessKey              = $env:NUGET_GALLERY_CACHE_SERVICE_ACCESS_KEY,
  $commitSha,
  $commitBranch
)

#Import Common Stuff
$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

#Validate Sutff
if(!$UseEmulator) {
  require-param -value $azureStorageAccessKey -paramName "azureStorageAccessKey"
  require-param -value $azureStorageAccountName -paramName "azureStorageAccountName"
  require-param -value $azureDiagStorageAccessKey -paramName "azureDiagStorageAccessKey"
  require-param -value $azureDiagStorageAccountName -paramName "azureDiagStorageAccountName"
  require-param -value $azureStorageBlobUrl -paramName "azureStorageBlobUrl"
  require-param -value $remoteDesktopAccountExpiration -paramName "remoteDesktopAccountExpiration"
  require-param -value $remoteDesktopCertificateThumbprint -paramName "remoteDesktopCertificateThumbprint"
  require-param -value $remoteDesktopEnctyptedPassword -paramName "remoteDesktopEnctyptedPassword"
  require-param -value $remoteDesktopUsername -paramName "remoteDesktopUsername"
  require-param -value $sqlAzureConnectionString -paramName "sqlAzureConnectionString"
  require-param -value $azureStatisticsConnectionString -paramName "azureStatisticsConnectionString"
  require-param -value $sslCertificateThumbprint -paramName "sslCertificateThumbprint"
  require-param -value $validationKey -paramName "validationKey"
  require-param -value $decryptionKey -paramName "decryptionKey"
  require-param -value $vmSize -paramName "vmSize"
  require-param -value $googleAnalyticsPropertyId -paramName "googleAnalyticsPropertyId"
}

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

    if ($value -ne $null) {
      $setting.value = $value.toString()
    } else {
      $setting.value = ""
    }
    $resolvedPath = resolve-path($path) 
    $settings.save($resolvedPath)
}

function set-vmsize {
    param($path, $size)
    $xml = [xml](get-content $path)
    $vmSize = $xml.ServiceDefinition.WebRole
    $vmSize.vmsize = $size.ToString()
    $resolvedPath = resolve-path($path) 
    $xml.save($resolvedPath)
}

function set-instancecount {
    param($path, $count)
    $xml = [xml](get-content $path)
    $instances = $xml.ServiceConfiguration.Role.Instances
    $instances.count = $count.ToString()
    $resolvedPath = resolve-path($path) 
    $xml.save($resolvedPath)
}

function remove-ssl {
  param($path)
  "Removing Azure SSL Settings..."
  $xml = [xml](get-content $path)
  $bindings = $xml.ServiceDefinition.WebRole.Sites.Site.Bindings
  $sslBinding = $xml.ServiceDefinition.WebRole.Sites.Site.Bindings.Binding | where {$_.name -eq "SSLBinding"}
  $bindings.RemoveChild($sslBinding) | Out-Null
  $endpoints = $xml.ServiceDefinition.WebRole.Endpoints
  $sslEndpoint = $xml.ServiceDefinition.WebRole.Endpoints.InputEndpoint | where {$_.name -eq "SSL"}
  $endpoints.RemoveChild($sslEndpoint) | Out-Null
  $resolvedPath = resolve-path($path) 
  $xml.save($resolvedPath)
}

function remove-setting {
  param($path, $name)
  "Removing Azure Setting $name..."
  $xml = [xml](get-content $path)
  $settings = $xml.ServiceConfiguration.Role.ConfigurationSettings
  $toRemove = $settings.Setting | where {$_.name -eq $name}
  $settings.RemoveChild($toRemove) | Out-Null
  $resolvedPath = resolve-path($path) 
  $xml.save($resolvedPath)
}

function remove-certificates {
  param($path)
  "Removing Azure Certificates..."
  $xml = [xml](get-content $path)
  $settings = $xml.ServiceConfiguration.Role.Certificates
  $settings.RemoveAll() | Out-Null
  $resolvedPath = resolve-path($path) 
  $xml.save($resolvedPath)
}

function remove-azmodule {
  param($path, $name)
  "Removing Azure Module $name..."
  $xml = [xml](get-content $path)
  $modules = $xml.ServiceDefinition.WebRole.Imports
  $toRemove = $modules.Import | where {$_.moduleName -eq $name}
  $modules.RemoveChild($toRemove) | Out-Null
  $resolvedPath = resolve-path($path) 
  $xml.save($resolvedPath)
}

function remove-startuptask {
  param($path, $commandLine)
  "Removing Startup Task $commandLine..."
  $xml = [xml](get-content $path)
  $startup = $xml.ServiceDefinition.WebRole.Startup
  $task = $startup.Task | where {$_.commandLine -eq $commandLine}
  $startup.RemoveChild($task) | Out-Null
  $resolvedPath = resolve-path($path) 
  $xml.save($resolvedPath)
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

function set-cacheserviceurl {
    param($path, $value) 
    
    $settings = [xml](get-content $path)

    $settings.configuration.dataCacheClients.dataCacheClient | % {
        $_.hosts.host.name = $value
    }
    
    $resolvedPath = resolve-path($path) 
    $settings.save($resolvedPath)
}

function set-cacheserviceaccesskey {
    param($path, $value) 
    
    $settings = [xml](get-content $path)

    $settings.configuration.dataCacheClients.dataCacheClient | % {
        $_.securityProperties.messageSecurity.authorizationInfo = $value
    }
    
    $resolvedPath = resolve-path($path) 
    $settings.save($resolvedPath)
}

function enable-azureElmah {
    param($path)
    $connectionString = "";
    if(!$UseEmulator) {
        $connectionString = "DefaultEndpointsProtocol=https;AccountName=$azureDiagStorageAccountName;AccountKey=$azureDiagStorageAccessKey";
    } else {
        $connectionString = "UseDevelopmentStorage=true";
    }

    $xml = [xml](get-content $path)
    $el = $xml.configuration.elmah.errorLog
    $el.type = "NuGetGallery.TableErrorLog, NuGetGallery.Website";
    $el.SetAttribute("connectionString", $connectionString);
    $el.RemoveAttribute("connectionStringName");
    $resolvedPath = resolve-path($path) 
    $xml.Save($resolvedPath)
}

#Do Work Brah
$script:WarningPreference = "Inquire"
$scriptPath = split-path $MyInvocation.MyCommand.Path
$rootPath = resolve-path(join-path $scriptPath "..")
$websitePath = join-path $rootPath "Website"
$webConfigPath = join-path $websitePath "Web.config"
$webConfigBakPath = join-path $scriptPath "Web.config.bak"
$rolePropertiesPath = join-path $scriptPath "NuGetGallery.RoleProperties.txt"
$cspkgFolder = join-path $rootPath "_AzurePackage"
$cspkgPath = join-path $cspkgFolder "NuGetGallery.cspkg"
$csdefPath = join-path $scriptPath "NuGetGallery.csdef"
$csdefBakPath = join-path $scriptPath "NuGetGallery.csdef.bak"
$cscfgPath = join-path $scriptPath "NuGetGallery.cscfg"
$cscfgBakPath = join-path $scriptPath "NuGetGallery.cscfg.bak"
$gitPath = (get-command git)
$binPath = join-path $websitePath "bin"

Write-Host "Checking the Git repository status is clean"
if ($commitRevision -eq $null) { $gitStatus = (& "$gitPath" status --short) }
if ($gitStatus)
{
   Write-Warning "Your git repository status is not clean. If you're actually deploying, it is recommended that you 'Halt Command' now and use 'git commit', 'git reset' or update your .gitignore to get to a clean state before you continue deployment."
}

$appDataPath = (join-path $websitePath App_Data)
if (test-path $appDataPath) {
    Write-Host "Wiping out the App_Data folder contents"
    rmdir $appDataPath
    mkdir $appDataPath
}

# Startup Scripts
$startupScripts = @("EnableDynamicHttpCompression.cmd", "ConfigureIISLogging.cmd")

if ($commitSha -eq $null) {
    $commitSha = (& "$gitPath" rev-parse HEAD)
    $packageSha = (& "$gitPath" rev-parse --short HEAD)
} else {
  $packageSha = $commitSha
}

if ($commitBranch -eq $null) {
    $commitBranch = (& "$gitPath" name-rev --name-only HEAD)
}

if(Test-Path $cspkgFolder) {
  del $cspkgFolder -Force -Recurse
}
mkdir $cspkgFolder | out-null

cp $webConfigPath $webConfigBakPath
cp $csdefPath $csdefBakPath
cp $cscfgPath $cscfgBakPath

if(!$UseEmulator) {
  set-vmsize -path $csdefPath -size $vmSize
  set-configurationsetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountExpiration" -value $remoteDesktopAccountExpiration
  set-certificatethumbprint -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.PasswordEncryption" -value $remoteDesktopCertificateThumbprint
  set-configurationsetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountEncryptedPassword" -value $remoteDesktopEnctyptedPassword
  set-configurationsetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountUsername" -value $remoteDesktopUsername
  if(![String]::IsNullOrEmpty($azureDiagStorageAccountName)) {
    set-configurationsetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" -value "DefaultEndpointsProtocol=https;AccountName=$azureDiagStorageAccountName;AccountKey=$azureDiagStorageAccessKey"
  }
  set-connectionstring -path $webConfigPath -name "NuGetGallery" -value $sqlAzureConnectionString
  set-connectionstring -path $webConfigPath -name "AzureStatistics" -value $azureStatisticsConnectionString
  set-certificatethumbprint -path $cscfgPath -name "nuget.org" -value $sslCertificateThumbprint
} else {
  remove-startuptask -path $csdefPath -commandLine "EnableDynamicHttpCompression.cmd"
  remove-ssl -path $csdefPath
  remove-azmodule -path $csdefPath -name "RemoteAccess"
  remove-azmodule -path $csdefPath -name "RemoteForwarder"
  remove-setting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.Enabled"
  remove-setting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteForwarder.Enabled"
  remove-setting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountExpiration"
  remove-certificates -path $cscfgPath
  remove-setting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountEncryptedPassword"
  remove-setting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountUsername"
  set-instancecount -path $cscfgPath -count 1
}

set-releasemode $webConfigPath
set-machinekey $webConfigPath

#Release Tag stuff
print-message("Setting the release tags")
if(!$UseEmulator) {
  set-appsetting -path $webConfigPath -name "Gallery:AzureStorageAccessKey" -value $azureStorageAccessKey
  set-appsetting -path $webConfigPath -name "Gallery:AzureStorageAccountName" -value $azureStorageAccountName
  set-appsetting -path $webConfigPath -name "Gallery:AzureStorageBlobUrl" -value $azureStorageBlobUrl
  set-appsetting -path $webConfigPath -name "Gallery:AzureCdnHost" -value $azureCdnHost
  set-appsetting -path $webConfigPath -name "Gallery:GoogleAnalyticsPropertyId" -value $googleAnalyticsPropertyId
  set-appsetting -path $webConfigPath -name "Gallery:PackageStoreType" -value "AzureStorageBlob"
  set-appsetting -path $webConfigPath -name "Gallery:ReleaseBranch" -value $commitBranch
  set-appsetting -path $webConfigPath -name "Gallery:ReleaseName" -value "NuGet 1.6 'Hershey'"
  set-appsetting -path $webConfigPath -name "Gallery:ReleaseSha" -value $commitSha
  set-appsetting -path $webConfigPath -name "Gallery:ReleaseTime" -value (Get-Date -format "dd/MM/yyyy HH:mm:ss")
  set-appsetting -path $webConfigPath -name "Gallery:ReleaseTime" -value (Get-Date -format "dd/MM/yyyy HH:mm:ss")
  set-appsetting -path $webConfigPath -name "Gallery:UseAzureEmulator" -value "false"
  set-cacheserviceurl -path $webConfigPath -value $cacheServiceEndpoint
  set-cacheserviceaccesskey -path $webConfigPath -value $cacheServiceAccessKey
}

if(![String]::IsNullOrEmpty($facebookAppId)) {
  set-appsetting -path $webConfigPath -name "Gallery:FacebookAppId" -value $facebookAppId
}
enable-azureElmah -path $webConfigPath

$startupScripts | ForEach-Object {
  cp (Join-Path $scriptPath $_) (Join-Path $binPath $_)
}

$copyOnlySwitch = ""
if($UseEmulator) {
  $copyOnlySwitch = "/copyOnly"
}

& "$AzureToolsRoot\.NET SDK\2012-10\bin\cspack.exe" "$csdefPath" /out:"$cspkgPath" /role:"Website;$websitePath" /sites:"Website;Web;$websitePath" /rolePropertiesFile:"Website;$rolePropertiesPath" $copyOnlySwitch
if ($lastexitcode -ne 0) {
  throw "CSPack Failed with Exit Code: $lastexitcode"
  exit 1 
}

cp $cscfgPath $cspkgFolder

cp $webConfigBakPath $webConfigPath
cp $csdefBakPath $csdefPath
cp $cscfgBakPath $cscfgPath
$startupScripts | ForEach-Object {
  rm (Join-Path $binPath $_)
}

$packageDateTime = (Get-Date -format "MMMdd @ HHmm")
print-success("Azure $env:NUGET_GALLERY_ENV package and configuration dropped to $cspkgFolder.")
print-success("Deployment Name: $packageDateTime ($packageSha on $commitBranch)")

write-host ""

Exit 0