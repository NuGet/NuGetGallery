# Enable Dynamic HTTP Compression for application/* mime types.
& "$env:windir\system32\inetsrv\appcmd.exe" set config /section:urlCompression /doDynamicCompression:True /commit:apphost
& "$env:windir\system32\inetsrv\appcmd.exe" set config /section:system.webServer/httpCompression /+"dynamicTypes.[mimeType='application/*',enabled='True']" /commit:apphost

# Load Azure Service Runtime Assembly
[Reflection.Assembly]::LoadWithPartialName("Microsoft.WindowsAzure.ServiceRuntime")

# Load machine keys from service config
$validationKey = [Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment]::GetConfigurationSettingValue("Gallery.ValidationKey");
$decryptionKey = [Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment]::GetConfigurationSettingValue("Gallery.DecryptionKey");

# Push the keys in to the web.config
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
        $resolvedPath = (resolve-path $path)
        $xml.save($resolvedPath)
    }
}
set-machinekey (Convert-Path "..\Web.Config")