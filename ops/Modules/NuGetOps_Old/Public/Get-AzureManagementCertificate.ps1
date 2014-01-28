<#
.SYNOPSIS
Gets the remote desktop certificate for the current environment if one is installed

.DESCRIPTION
#>
function Get-AzureManagementCertificate {
    param([Parameter(Mandatory=$false, Position=0)][string]$SubscriptionName)
    
    if(!$SubscriptionName) {
        if(!$CurrentEnvironment) {
            throw "This command requires an environment or a subscription name"
        }
        $SubscriptionName = $CurrentEnvironment.Subscription.Name
    }

    $subName = $SubscriptionName.Replace(" ", "")
    $CertPrefix = "CN=Azure-$subName-*"

    dir "cert:\CurrentUser\My" | where { $_.Subject -like $CertPrefix } | select -first 1
}