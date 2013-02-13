param(
  $environment                        = $env:NUGET_GALLERY_ENV,
  $sqlConnectionString                = $env:NUGET_GALLERY_MAIN_CONNECTION_STRING,
  $warehouseConnectionString          = $env:NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING,
  $mainStorage                        = $env:NUGET_GALLERY_MAIN_STORAGE,
  $diagnosticsStorage                 = $env:NUGET_GALLERY_DIAGNOSTICS_STORAGE,
  $backupSourceSqlConnectionString    = $env:NUGET_GALLERY_BACKUP_SOURCE_CONNECTION_STRING,
  $backupSourceStorage                = $env:NUGET_GALLERY_BACKUP_SOURCE_STORAGE,
  $reportsStorage                     = $env:NUGET_WAREHOUSE_REPORTS_STORAGE
)

#Import Common Stuff
function require-param {
  param($value, $paramNames)
  
  if ($value -eq $null) {
    write-error "The parameter -$paramName is required."
    exit 1
  }
}

#Validate Sutff
require-param -value $environment -paramName "environment"
require-param -value $sqlConnectionString -paramName "sqlConnectionString"
require-param -value $mainStorage -paramName "mainStorage"
require-param -value $diagnosticsStorage -paramName "diagnosticsStorage"

#Helper Functions
function set-connectionstring {
  param($path, $name, $value)
  $settings = [xml](get-content $path)
  $setting = $settings.configuration.connectionStrings.add | where { $_.name -eq $name }
  $setting.connectionString = "$value"
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

function set-rolesetting {
    param($path, $name, $value)
    $settings = [xml](get-content $path)
    $setting = $settings.ServiceConfiguration.Role.ConfigurationSettings.Setting | Where { $_.name -eq $name }
    $setting.value = $value
    $resolvedPath = resolve-path($path)
    $settings.save($resolvedPath)
}

function set-rolecert {
    param($path, $name, $value)
    $settings = [xml](get-content $path)
    $setting = $settings.ServiceConfiguration.Role.Certificates.Certificate | Where { $_.name -eq $name }
    $setting.thumbprint = $value
    $resolvedPath = resolve-path($path)
    $settings.save($resolvedPath)
}

$scriptPath = split-path $MyInvocation.MyCommand.Path
$rootPath = resolve-path (join-path $scriptPath "..")
$cloudServicePath = Join-Path $rootPath "Source\WorkerCloudService"
$workerPath = Join-Path $rootPath "Source\NuGetGallery.Operations.Worker"

$buildOutput = Join-Path $cloudServicePath "bin\Release\app.publish"
$cspkgFolder = Join-Path $rootPath "_AzurePackage";
if(Test-Path $cspkgFolder) {
  del -Recurse -Force $cspkgFolder
}

$buildPath = Join-Path $cloudServicePath "WorkerCloudService.ccproj"
$buildParams = "/p:Configuration=Release"
$buildTarget = "/t:Publish"

$configPath = join-path $workerPath "App.config"
$configBakPath = join-path $scriptPath "App.config.bak"

$cscfgPath = join-path $cloudServicePath "ServiceConfiguration.Cloud.cscfg"
$cscfgBakPath = join-path $scriptPath "ServiceConfiguration.Cloud.cscfg.bak"

cp $configPath $configBakPath
cp $cscfgPath $cscfgBakPath

set-appsetting -path $configPath -name "NUGET_GALLERY_ENV" -value $environment
set-appsetting -path $configPath -name "NUGET_GALLERY_MAIN_STORAGE" -value $mainStorage
if ("$backupSourceStorage" -ne "") { set-appsetting -path $configPath -name "NUGET_GALLERY_BACKUP_SOURCE_STORAGE" -value $backupSourceStorage }
set-appsetting -path $configPath -name "NUGET_GALLERY_MAIN_CONNECTION_STRING" -value $sqlConnectionString
set-appsetting -path $configPath -name "NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING" -value $warehouseConnectionString
set-appsetting -path $configPath -name "NUGET_WAREHOUSE_REPORTS_STORAGE" -value $reportsStorage

if ("$backupSourceSqlConnectionString" -ne "") { set-appsetting -path $configPath -name "NUGET_GALLERY_BACKUP_SOURCE_CONNECTION_STRING" -value $backupSourceSqlConnectionString }
set-rolesetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" -value $diagnosticsStorage

if(![String]::IsNullOrEmpty($env:NUGET_GALLERY_REMOTE_DESKTOP_ACCOUNT_EXPIRATION)) {
  set-rolesetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountUsername" -value $env:NUGET_GALLERY_REMOTE_DESKTOP_USERNAME
  set-rolesetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountEncryptedPassword" -value $env:NUGET_GALLERY_REMOTE_DESKTOP_ENCRYPTED_PASSWORD
  set-rolesetting -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.AccountExpiration" -value $env:NUGET_GALLERY_REMOTE_DESKTOP_ACCOUNT_EXPIRATION
  set-rolecert -path $cscfgPath -name "Microsoft.WindowsAzure.Plugins.RemoteAccess.PasswordEncryption" -value $env:NUGET_GALLERY_REMOTE_DESKTOP_CERTIFICATE_THUMBPRINT
}

& "$(get-content env:windir)\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" $buildFile $buildParams $buildTarget

cp $configBakPath $configPath
cp $cscfgBakPath $cscfgPath

mkdir $cspkgFolder | out-null
cp $buildOutput\* $cspkgFolder

write-host "Azure package and configuration dropped to $cspkgFolder."
write-host ""

Exit 0