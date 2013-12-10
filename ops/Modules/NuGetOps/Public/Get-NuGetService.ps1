<#
.SYNOPSIS
Gets the service with the specified name/ID or ones that match that substring

.PARAMETER Thumbprint
The Thumbprint of the certificate to use to encrypt. MUST BE INSTALLED.
#>
function Get-NuGetService {
    param(
        [Parameter(Mandatory=$false, Position=0)][string]$Name,
        [Parameter(Mandatory=$false)][switch]$ForceSingle)

    if(!$CurrentEnvironment) {
        throw "This command requires an environment"
    }

    $candidates = @($CurrentEnvironment.Services)
    if($Name) {
        $candidates = @($candidates | where { ($_.Name -like "*$Name*") -or ($_.ID -eq "*$Name*") })
    }

    $exactMatch = @($candidates | where { ($_.Name -eq $Name) -or ($_.ID -eq $Name) })
    if($exactMatch.Length -eq 1) {
        $exactMatch[0]
    }
    else {
        if($ForceSingle) {
            if($candidates.Length -eq 1) {
                $candidates[0]
            } elseif($candidates.Length -gt 1) {
                throw "Multiple matches for $Name found: $candidates"
            }
        } else {
            $candidates
        }
    }
}
Set-Alias -Name svc -Value Get-NuGetService
Export-ModuleMember -Alias svc