<#
.SYNOPSIS
Creates an Azure Management Certificate
#>
function New-AzureManagementCertificate {
    param([switch]$Force)
    if(!$Force) {
        throw "No longer needed! Use Add-AzureAccount to configure your Azure account. If you know you need to run this command, use -Force"
    }

    if(!$CurrentEnvironment -or !$CurrentEnvironment.Subscription) {
        throw "Requires an active environment"
    }

    $subName = $CurrentEnvironment.Subscription.Name.Replace(" ", "")
    $NamePrefix = "Azure-$subName-$([Environment]::UserName)-on-$([Environment]::MachineName)-"
    if(@(dir cert:\CurrentUser\My | where { $_.Subject -like "CN=$NamePrefix*, O=Azure, OU=$($CurrentEnvironment.Subscription.Id)" }).Length -gt 0) {
        throw "A cert is already registered in the store for this (subscription, user, machine) triple"
    }

    $CommonName = "$($NamePrefix)at-$([DateTime]::UtcNow.ToString("yyyy-MM-dd"))-utc"
    $Name = "CN=$CommonName,O=Azure,OU=$($CurrentEnvironment.Subscription.Id)"
    
    Write-Host "Generating Certificate..."
    $FileName = Join-Path (Convert-Path .) "$CommonName.cer"
    $PfxFileName = Join-Path (Convert-Path .) "$CommonName.pfx"
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
    makecert -sky exchange -r -n "$Name" -pe -a sha1 -len 2048 -ss My $FileName

    # Get the Thumbprint and find the private key in the store
    $FileName = (Convert-Path $FileName)
    Write-Host "Certificate created. Public Key is at $FileName"
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $FileName
    $CertificateThumbprint = $cert.Thumbprint

    $cert = get-item "cert:\CurrentUser\My\$CertificateThumbprint"
    $CertData = $cert.Export("Pkcs12", [String]::Empty);
    [IO.File]::WriteAllBytes($PfxFileName, $CertData)
}