<#
.SYNOPSIS
Sets the active NuGet Environment

.PARAMETER Name
The name of an environment defined in Environments.xml

#>
function Set-Environment {
    param([Parameter(Mandatory=$true)][string]$Name)
    
    #Find the key
    $key = @($Environments.Keys | Where { $_ -like "$Name*" })
    if($key.Length -eq 0) {
        throw "Unknown Environment $Name"
    } elseif($key.Length -gt 1) {
        throw "Ambiguous Environment Name: $Name. Did you mean one of these?: $key"
    }

    $env = $Environments[$key]
    Write-Host "Downloading Configuration for $($env.Name) environment"

    RunInSubscription $env.Subscription {
        $Global:CurrentEnvironment = $env
        
        Write-Host "Downloading Configuration for Web Role..."
        $service = Get-AzureDeployment -ServiceName $env.Service -Slot "production"
        
        Write-Host "Downloading Configuration for Worker Role..."
        $worker = Get-AzureDeployment -ServiceName $env.Worker -Slot "production"

        $Global:CurrentDeployment = @{
            "Service" = $service;
            "Worker" = $worker;
        }
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