param(
    [Parameter(Mandatory=$true)][string]$OutputFile,
    [Parameter(Mandatory=$false)][string]$TargetSubscription = $null,
    [Parameter(Mandatory=$false)][string]$TargetService = $null,
    [Parameter(Mandatory=$false)][string]$TargetStorageAccount = $null,
    [Parameter(Mandatory=$false)][string]$TargetDatabaseServer = $null,
    [Parameter(Mandatory=$false)][string]$TargetDatabaseName = $null,
    [Parameter(Mandatory=$false)][string]$AzureRealm = $null
)

# Import common stuff
$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

# Little bit of special hackery to make Microsofties lives easier :P
if([String]::Equals([Environment]::UserDomainName, "REDMOND", "OrdinalIgnoreCase") -and (!$AzureRealm)) {
    $AzureRealm = "microsoft.com" # Setting this won't give non-softies access if they spoof the Domain :), they still have to authenticate
}


if(!(Get-Module -List Azure -ErrorAction SilentlyContinue)) {
    throw "You need to install Windows Azure Powershell from WebPI to use this command"
}

$subs = @(Get-AzureSubscription -ErrorAction SilentlyContinue)
if(!$subs) {
    $result = $false;
    $loop = $true
    do {
        Write-Host "You must download and import a publish profile to use this command."
        $answer = (Read-Host "Would you like do do that now? [Y/n]")
        if("Yes".StartsWith($answer, "OrdinalIgnoreCase") -or [String]::IsNullOrEmpty($answer)) {
            $loop = $false
            $result = $true;
        } elseif("No".StartsWith($answer, "OrdinalIgnoreCase")) {
            $loop = $false;
        }else {
            Write-Host "Unexpected answer..."
        }
    } while($loop)

    if(!$result) {
        throw "Can't use this command without a publish profile imported..."
    }
    Write-Host "Fetching Publish Profile. Your browser will launch and prompt you to download a file. Press ENTER when that's done."

    $url = "https://windows.azure.com/download/publishprofile.aspx"
    if($AzureRealm) {
        $url += "?whr=$AzureRealm"
    }

    Start-Process "$url"
    Read-Host
    Write-Host "Now select that file in the dialog that has appeared"

    [Reflection.Assembly]::LoadWithPartialName("System.Windows.Forms") | Out-Null
    $ofd = new-object System.Windows.Forms.OpenFileDialog
    $ofd.Title = "Select Azure Publish Settings"
    $ofd.Filter = "Azure Publish Settings (*.publishsettings)|*.publishsettings"
    if($ofd.ShowDialog() -ne "OK") {
        throw "User cancelled dialog"
    }
    $publishSettingsFile = $ofd.FileName;
    if(!(Test-Path $publishSettingsFile)) {
        throw "Publish Settings file does not exist! $publishSettingsFile"
    }
    Write-Host "Using PublishSettings from $publishSettingsFile"
    Import-AzurePublishSettingsFile -PublishSettingsFile $publishSettingsFile

    $subs = @(Get-AzureSubscription -ErrorAction SilentlyContinue)
    if(!$subs) {
        throw "Still no subscriptions!!"
    }
}

# Select the Subscription
$Subscription = SelectOrUseProvided $TargetSubscription (Get-AzureSubscription) { $true } "Subscription" { $_.SubscriptionName }
Write-Host "** Target Subscription: $($Subscription.SubscriptionName)" -ForegroundColor Black -BackgroundColor Green
Select-AzureSubscription $Subscription.SubscriptionName

# Select the Cloud Service
$Service = SelectOrUseProvided $TargetService (Get-AzureService) { !$_.ServiceName.EndsWith("ops", "OrdinalIgnoreCase") } "Service" { $_.ServiceName }
Write-Host "** Target Service: $($Service.ServiceName)" -ForegroundColor Black -BackgroundColor Green

# Gallery.AzureStorageConnectionString
# Gallery.AzureDiagnosticsConnectionString
# Select the Storage Account (TODO: Use Linked Resources?)
$Storage = SelectOrUseProvided $TargetStorageAccount (Get-AzureStorageAccount) { 
    !$_.StorageAccountName.EndsWith("diag", "OrdinalIgnoreCase") -and
    !$_.StorageAccountName.EndsWith("bak", "OrdinalIgnoreCase") -and
    !$_.StorageAccountName.EndsWith("mds", "OrdinalIgnoreCase") } "Storage Account" { $_.StorageAccountName }
Write-Host "** Target Storage Account: $($Storage.StorageAccountName)" -ForegroundColor Black -BackgroundColor Green

Write-Host "** NOTE: Azure CDN can't be configured by this script. Set `"Gallery.AzureCdnHost`" in the resulting CSCFG manually if you want to use CDN" -ForegroundColor Black -BackgroundColor Yellow

# Gallery.GoogleAnalyticsPropertyId
# Gallery.PackageStoreType
$GoogleAnalyticsPropertyId = Read-Host "Google Analytics Property ID (leave empty if none)"
$PackageStoreType = "AzureStorageBlob"

# Gallery.SiteRoot
$SiteRoot = "$($Service.ServiceName).cloudapp.net"
$ChangeSiteRoot = Read-Host "Enter Site Root (leave empty to use default: $SiteRoot)"
if(![String]::IsNullOrWhitespace($ChangeSiteRoot)) {
    $SiteRoot = $ChangeSiteRoot
}
Write-Host "** Using Site Root $SiteRoot" -ForegroundColor Black -BackgroundColor Green

# Gallery.Sql.NuGetGallery
$SqlServer = SelectOrUseProvided $TargetDatabaseServer (Get-AzureSqlDatabaseServer) { $true } "SQL Server" { $_.ServerName }
Write-Host "** Target SQL Azure Server: $($SqlServer.ServerName)" -ForegroundColor Black -BackgroundColor Green
$Database = $TargetDatabaseName
if([String]::IsNullOrWhitespace($TargetDatabaseName)) {
    $Database = Read-Host "Enter SQL Database Name (leave empty to use default 'NuGetGallery')"
    if([String]::IsNullOrWhitespace($Database)) {
        $Database = "NuGetGallery"
    }
}
Write-Host "** Using Database $Database" -ForegroundColor Black -BackgroundColor Green
Write-Host "Enter Credentials for this database."
$DatabaseCredentials = Get-Credential -UserName $SqlServer.AdministratorLogin -Message "Enter SQL Azure Credentials"

# Gallery.AzureCacheEndpoint
Write-Host "** NOTE: Azure Caching can't be configured by this script. Set `"Gallery.AzureCacheEndpoint`" in the resulting CSCFG manually if you want to use Caching" -ForegroundColor Black -BackgroundColor Yellow

# Gallery.ValidationKey
# Gallery.DecryptionKey
Write-Host "** Generating unique Validation and Decryption keys. Set them manually if you are updating an existing service and want to preserve cookies"
$ValidationKey = [BitConverter]::ToString((New-Object System.Security.Cryptography.HMACSHA256).Key).Replace("-", "").ToLowerInvariant()
$aes = New-Object System.Security.Cryptography.AesManaged
$aes.GenerateKey()
$DecryptionKey = [BitConverter]::ToString($aes.Key).Replace("-", "").ToLowerInvariant()

# Write the file!
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$template = (Join-Path $ScriptRoot "NuGetGallery.base.cscfg")
if(!(Test-Path $template)) {
    throw "Could not find template file: $template"
}

function Set-Setting {
    param($settings, $name, [string]$value)
    $node = $settings.Setting | Where-Object { $_.name -eq $name }
    $node.value = $value;
}

$outXml = [xml](cat $template)
$settings = $outXml.ServiceConfiguration.Role.ConfigurationSettings

# Set Storage Connection Strings
Write-Host "Loading Storage Account Key..."
$StorageConnectionString = Get-StorageAccountConnectionString $Storage.StorageAccountName
Set-Setting $settings "Gallery.AzureStorageConnectionString" $StorageConnectionString
Set-Setting $settings "Gallery.AzureDiagnosticsConnectionString" $StorageConnectionString

# Set simple settings
Set-Setting $settings "Gallery.GoogleAnalyticsPropertyId" $GoogleAnalyticsPropertyId
Set-Setting $settings "Gallery.PackageStoreType" $PackageStoreType
Set-Setting $settings "Gallery.SiteRoot" $SiteRoot
Set-Setting $settings "Gallery.ValidationKey" $ValidationKey
Set-Setting $settings "Gallery.DecryptionKey" $DecryptionKey

# Set Connection String
$UserName = $DatabaseCredentials.UserName
$Password = "";
try {
    $unmanagedString = [System.Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($DatabaseCredentials.Password)
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringUni($unmanagedString)
} finally {
    [System.Runtime.InteropServices.Marshal]::ZeroFreeGlobalAllocUnicode($unmanagedString);
}
$ConnectionString = "Server=tcp:$($SqlServer.ServerName).database.windows.net;Database=$Database;User ID=$($UserName)@$($SqlServer.ServerName);Password=$Password;Trusted_Connection=False;Encrypt=True"
Set-Setting $settings "Gallery.Sql.NuGetGallery" $ConnectionString

# Resolve the path to the output file. Can't just use Resolve-Path because that will fail if it does not exist.
# So find the root that does exist and resolve that, then join the rest.
$ToResolve = $OutputFile
$ToJoin = ""
while(![String]::IsNullOrWhitespace($ToResolve) -and !(Test-Path $ToResolve)) {
    $ToJoin = Join-Path (Split-Path -Leaf $ToResolve) $ToJoin
    $ToResolve = Split-Path -Parent $ToResolve
}
if([String]::IsNullOrWhitespace($ToResolve)) {
    $ToResolve = $ScriptRoot
}
$OutputFile = (Join-Path (Resolve-Path $ToResolve) $ToJoin).TrimEnd("\")

# Save the file
Write-Host "** Saving to $OutputFile" -ForegroundColor Black -BackgroundColor Green
$outXml.Save($OutputFile)
Write-Host "** DONE! **" -ForegroundColor Black -BackgroundColor Green