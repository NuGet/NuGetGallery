# Copyright (c) Andre N. Klingsheim. See https://nwebsec.codeplex.com/license for license information.

param([bool]$allowReboot = $false)

Function UpdateRegistryPath($path){

  if(test-path $path){
    return $false
  }
  write-Host "Creating registry path: $path"
  md $path
  return $true
}

Function UpdateRegistryKey($path, $propertyName, $value, $propertyType){
  $property = Get-ItemProperty -Path $path -Name $propertyName -ErrorAction SilentlyContinue

  if($property){
    if($property.$propertyName -eq $value){
      return $false
    }
  Write-Host "Updating registry key $path $propertyName $value"
    Set-ItemProperty -path $path -name $propertyName -value $value
    return $true
  }
  Write-Host "Creating registry key $path $propertyName $value"
  New-ItemProperty -path $path -name $propertyName -value $value -PropertyType $propertyType
  return $true
}

$date = Get-Date
write-output "---- NWebsec.AzureStartupTasks - TLS hardening - $date ----"
write-output "Checking for registry keys, updating as necessary"
write-output ""


$preferredCipherSuites = "TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384_P256,TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384_P384,TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256_P256,TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA_P256,TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA_P384,TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA_P256,TLS_RSA_WITH_AES_256_GCM_SHA384,TLS_RSA_WITH_AES_128_GCM_SHA256,TLS_RSA_WITH_AES_256_CBC_SHA256,TLS_RSA_WITH_AES_128_CBC_SHA256,TLS_RSA_WITH_AES_256_CBC_SHA,TLS_RSA_WITH_AES_128_CBC_SHA,TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384_P384,TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256_P256,TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384_P384,TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256_P256,TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA_P256,TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA_P384,TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA_P256,TLS_DHE_DSS_WITH_AES_256_CBC_SHA256,TLS_DHE_DSS_WITH_AES_128_CBC_SHA256,TLS_DHE_DSS_WITH_AES_256_CBC_SHA,TLS_DHE_DSS_WITH_AES_128_CBC_SHA,TLS_RSA_WITH_3DES_EDE_CBC_SHA,TLS_DHE_DSS_WITH_3DES_EDE_CBC_SHA"
$rebootRequired = $false

# Disable SSL 2.0
write-output "**** Making sure SSL 2.0 is disabled ****"
$rebootRequired = (UpdateRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 2.0\Server") -Or $rebootRequired
$rebootRequired = (UpdateRegistryKey "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 2.0\Server" "Enabled" 0 "DWord") -Or $rebootRequired

# Disable SSL 3.0
write-output "**** Making sure SSL 3.0 is disabled ****"
$rebootRequired = (UpdateRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 3.0") -Or $rebootRequired
$rebootRequired = (UpdateRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 3.0\Server") -Or $rebootRequired
$rebootRequired = (UpdateRegistryKey "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\SSL 3.0\Server" "Enabled" 0 "DWord") -Or $rebootRequired

# Enable TLS 1.1
write-output "**** Making sure TLS 1.1 is enabled ****"
$rebootRequired = (UpdateRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1") -Or $rebootRequired
$rebootRequired = (UpdateRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Server") -Or $rebootRequired
$rebootRequired = (UpdateRegistryKey "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Server" "DisabledByDefault" 0 "DWord") -Or $rebootRequired
$rebootRequired = (UpdateRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client") -Or $rebootRequired
$rebootRequired = (UpdateRegistryKey "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client" "DisabledByDefault" 0 "DWord") -Or $rebootRequired


# Enable TSL 1.2
write-output "**** Making sure TLS 1.2 is enabled ****"
$rebootRequired = (UpdateRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2") -Or $rebootRequired
$rebootRequired = (UpdateRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Server") -Or $rebootRequired
$rebootRequired = (UpdateRegistryKey "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Server" "DisabledByDefault" 0 "DWord") -Or $rebootRequired
$rebootRequired = (UpdateRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client") -Or $rebootRequired
$rebootRequired = (UpdateRegistryKey "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client" "DisabledByDefault" 0 "DWord") -Or $rebootRequired

# Protocol versions done, set preferred cipher suites
write-output "**** Making sure preferred cipher suites are set ****"
$rebootRequired = (UpdateRegistryKey "HKLM:\SOFTWARE\Policies\Microsoft\Cryptography\Configuration\SSL\00010002" "Functions" $preferredCipherSuites "String") -Or $rebootRequired

if($rebootRequired){
  if($allowReboot){
    write-output "Registry was updated, rebooting..."
    write-output "---- NWebsec.AzureStartupTasks - TLS hardening Completed - $date ----"
    shutdown /r /t 0
  }else{
    write-output "Registry was updated, reboot is required for changes to take effect."
  }
}else{
write-output "Registry keys were ok, exiting."
}
write-output "---- NWebsec.AzureStartupTasks - TLS hardening Completed - $date ----"