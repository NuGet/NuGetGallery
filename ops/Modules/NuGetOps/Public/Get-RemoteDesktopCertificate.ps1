<#
.SYNOPSIS
Gets the remote desktop certificate for the current environment if one is installed

.DESCRIPTION
#>
function Get-RemoteDesktopCertificate {
    if(!$CurrentEnvironment) {
        throw "This command requires an environment"
    }

    $CertificateName = "nuget-$($CurrentEnvironment.Name)"

    dir cert:\CurrentUser\My | where { $_.FriendlyName -eq $CertificateName } | select -first 1
}