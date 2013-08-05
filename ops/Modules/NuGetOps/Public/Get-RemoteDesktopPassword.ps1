<#
.SYNOPSIS
Returns a random, timestamped, password AND an encrypted password suitable for use as a Remote Desktop password

.PARAMETER Thumbprint
The Thumbprint of the certificate to use to encrypt. MUST BE INSTALLED.
#>
function Get-RemoteDesktopPassword {
    param(
        [Parameter(Mandatory=$true)][string]$Thumbprint)
	if(!$AzureSDKRoot) {
        throw "This command requires the Azure .NET SDK"
    }
    $plainText = Get-RandomPassword
    $cipherText = [String]::Concat(
        (echo $plainText | 
         & "$AzureSDKRoot\bin\csencrypt.exe" Encrypt-Password -Thumbprint $Thumbprint |
         select -skip 5))
    return @{
        "PlainText" = $plainText;
        "CipherText" = $cipherText;
    }
}