<#
.SYNOPSIS
Sets the active NuGet Environment

.DESCRIPTION
This command has two different behaviors.

If given the "-Name" argument, it searches the Environments.xml file located
at $EnvironmentsList (or the NUGET_OPS_ENVIRONMENTS environment variable) for an environment with the specified
name, and loads that configuration data. Note that the contents of this file are cached, so if you change it, you will
need to use Exit-NuGetOps and Enter-NuGetOps to reload the operations console.

If given the "-ServiceName", "-WorkerName" and "-Subscription" arguments, it creates an ad-hoc environment based on the services you entered.
The name given to that service is the value given as the ServiceName paramter. Setting the "-NonProduction" switch disables extra checks which are
normally put in place for production environments. This version of the command should only be used in the rare occasions you are unable to connect
to the file share or location where the Environments.xml file is stored.

.PARAMETER Name
The name of an environment defined in Environments.xml

.PARAMETER Frontend
The name of an Azure Web Site which is present in one of the subscriptions you have already registered on this machine and contains the NuGetGallery frontend.

.PARAMETER Backend
The name of an Azure Cloud Service which is present in one of the subscriptions you have already registered on this machine and contains the NuGetGallery backend.

.PARAMETER Subscription
The name of the Azure Subscription containing the services named in Frontend/Backend.

.PARAMETER NonProduction
Add this flag to disable extra checks relating to production environments

#>
function Set-Environment {
    param(
        [Parameter(Mandatory=$true, ParameterSetName="FromList")][string]$Name,
        [Parameter(Mandatory=$true, ParameterSetName="AdHoc")][string]$Frontend,
        [Parameter(Mandatory=$true, ParameterSetName="AdHoc")][string]$Backend,
        [Parameter(Mandatory=$true, ParameterSetName="AdHoc")][string]$Subscription,
        [Parameter(Mandatory=$false, ParameterSetName="AdHoc")][switch]$NonProduction
    )
    
    if($PsCmdlet.ParameterSetName -eq "FromList") {
        # Find the key
        $key = @($Environments.Keys | Where { $_ -like "$Name*" })
        if($key.Length -eq 0) {
            throw "Unknown Environment $Name"
        } elseif($key.Length -gt 1) {
            throw "Ambiguous Environment Name: $Name. Did you mean one of these?: $key"
        }
        $Global:CurrentEnvironment = $Environments[$key]
    } elseif($PsCmdlet.ParameterSetName -eq "AdHoc") {
        # Build an environment object
        $Global:CurrentEnvironment = New-Object PSCustomObject
        Add-Member -NotePropertyMembers @{
            Version = 0.2;
            Name = $ServiceName;
            Protected = !$NonProduction;
            Frontend = $Frontend;
            Backend = $Backend;
            Subscription = $Subscription
        } -InputObject $Global:CurrentEnvironment
    } else {
        throw "Unknown Parameter Set: $($PsCmdlet.ParameterSetName)"
    }

    Write-Host "Downloading Configuration for $($CurrentEnvironment.Name) environment"

    # Check for the subscription
    $subName = $CurrentEnvironment.Subscription
    if($subName -isnot [string]) {
        $subName = $subName.Name;
    }

    try {
        Get-AzureSubscription $subName | Out-Null
    } catch {
        throw "You need to register the subscription: $subName. Use New-PublishSettingsFile to generate a publish settings file, or Import-PublishSettingsFile if you already have one for this subscription"
    }

    RunInSubscription $CurrentEnvironment.Subscription.Name {
        
        Write-Host "Downloading Configuration for Frontend..."
        $frontend = $null;
        if($CurrentEnvironment.Type -eq "website") {
            $frontend = Get-AzureWebsite -Name $CurrentEnvironment.Frontend
        } elseif($CurrentEnvironment.Type -eq "webrole") {
            $frontend = Get-AzureDeployment -ServiceName $CurrentEnvironment.Frontend -Slot "production"
        } else {
            Write-Warning "Unknown Service Type: $($CurrentEnvironment.Type)"
        }
        
        Write-Host "Downloading Configuration for Backend..."
        $backend = Get-AzureDeployment -ServiceName $CurrentEnvironment.Backend -Slot "production"

        $Global:CurrentDeployment = @{
            "Frontend" = $frontend;
            "Backend" = $backend;
        }

        if(_IsProduction) {
            $Global:OldBgColor = $Host.UI.RawUI.BackgroundColor
            $Host.UI.RawUI.BackgroundColor = "DarkRed"
            _RefreshGitColors
            Write-Warning "You are attached to the PRODUCTION Environment. Use caution!"
        } else {
            if($Global:OldBgColor) {
                $Host.UI.RawUI.BackgroundColor = $Global:OldBgColor
                del variable:\OldBgColor
            }
            _RefreshGitColors
        }
    }

}