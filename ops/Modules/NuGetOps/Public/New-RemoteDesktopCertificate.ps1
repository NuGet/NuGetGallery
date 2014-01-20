<#
.SYNOPSIS
Creates a new remote desktop certificate for the current environment

.DESCRIPTION
#>
function New-RemoteDesktopCertificate {
    if(!$CurrentEnvironment) {
        throw "This command requires an environment"
    }

    if(!$AzureSDKRoot) {
        throw "This command requires the Azure .NET SDK"
    }

    $CertificateName = "nuget-$($CurrentEnvironment.Name)"

    $existingCert = Get-RemoteDesktopCertificate
    if($existingCert) {
        throw "There is already a certificate with the friendly name $CertificateName. Please delete it first."
    }

    $Thumbprint = & "$AzureSDKRoot\bin\csencrypt.exe" new-passwordencryptioncertificate -FriendlyName $CertificateName | 
        where { $_ -match "Thumbprint\s+:\s(.*)" } | 
        foreach { $matches[1] }

    Write-Host "Created Remote Desktop Certificate $CertificateName with thumbprint $Thumbprint"
}