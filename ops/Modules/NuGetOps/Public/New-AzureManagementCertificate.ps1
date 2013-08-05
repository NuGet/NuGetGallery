<#
.SYNOPSIS
Creates an Azure Management Certificate

.DESCRIPTION
This command creates a certificate which can be uploaded to the Azure Portal for use as a management certificate

.PARAMETER Name
The name of the certificate to create (if not specified, a default will be used and that default is good, so only specify this if you really need a custom name)

.PARAMETER Force
If a cert already exists with the specified name, delete it first
#>

function New-AzureManagementCertificate {
    param([Parameter(Mandatory=$false)][string]$Name, [switch]$Force)

    if(!$Name) {
        $Name = "Azure-$([Environment]::UserName)-on-$([Environment]::MachineName)-at-$([DateTime]::UtcNow.ToString("yyyy-MM-dd"))-utc"
    }

    Write-Host "Generating Certificate..."
    $FileName = Join-Path (Convert-Path .) "$Name.cer"
    $PfxFileName = Join-Path (Convert-Path .) "$Name.pfx"
    if(Test-Path $FileName) {
        if($Force) {
            del $FileName
        } else {
            throw "There is already a cert at $FileName. Delete it or move it before running this command, or specify the -Force argument to have this script replace it."
        }
    }
    if(Test-Path $PfxFileName) {
        if($Force) {
            del $PfxFileName
        } else {
            throw "There is already a cert at $PfxFileName. Delete it or move it before running this command, or specify the -Force argument to have this script replace it."
        }
    }
    makecert -sky exchange -r -n "CN=$Name" -pe -a sha1 -len 2048 -ss My $FileName

    # Get the Thumbprint and find the private key in the store
    $FileName = (Convert-Path $FileName)
    Write-Host "Certificate created. Public Key is at $FileName"
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $FileName
    $CertificateThumbprint = $cert.Thumbprint

    $cert = get-item "cert:\CurrentUser\My\$CertificateThumbprint"
    $CertData = $cert.Export("Pkcs12", [String]::Empty);
    [IO.File]::WriteAllBytes($PfxFileName, $CertData)
}