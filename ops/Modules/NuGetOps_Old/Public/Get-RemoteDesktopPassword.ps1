<#
.SYNOPSIS
Returns a random, timestamped, password AND an encrypted password suitable for use as a Remote Desktop password

.PARAMETER Thumbprint
The Thumbprint of the certificate to use to encrypt. If not specified, the default one for this environment will be used, if present. Certificate MUST BE INSTALLED.
#>
function Get-RemoteDesktopPassword {
    param(
        [Parameter(Mandatory=$false)][string]$Thumbprint)

	if(!$AzureSDKRoot) {
        throw "This command requires the Azure .NET SDK"
    }

    if(!$Thumbprint) {
        $CertificateName = "nuget-$($CurrentEnvironment.Name)"
        $cert = Get-RemoteDesktopCertificate
        if(!$cert) {
            throw "Environment RDP certificate is not installed. Download the '$CertificateName' certificate from the secret store and install it"
        }
        $Thumbprint = $cert.Thumbprint
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