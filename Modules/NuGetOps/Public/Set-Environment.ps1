<#
.SYNOPSIS
Sets the active NuGet Environment

.PARAMETER Name
The name of an environment defined in Environments.xml

#>
function Set-Environment {
    param([Parameter(Mandatory=$true)][string]$Name)
    
    $env = $Environments[$Name]
    if(!$env) {
        throw "Unknown Environment $Name"
    }
    Write-Host "Downloading Configuration for $Name environment"

    RunInSubscription $env.Subscription {
        $Global:CurrentDeployment = Get-AzureDeployment -ServiceName $env.Service -Slot "production"
        $Global:CurrentEnvironment = $env
    }

    if(_IsProduction) {
        $Global:OldBgColor = $Host.UI.RawUI.BackgroundColor
        $Host.UI.RawUI.BackgroundColor = "DarkRed"
        _RefreshGitColors
    } else {
        if($Global:OldBgColor) {
            $Host.UI.RawUI.BackgroundColor = $Global:OldBgColor
            del variable:\OldBgColor
        }
        _RefreshGitColors
    }
}