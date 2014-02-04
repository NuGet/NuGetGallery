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

    Write-Host "Setting Current Environment to $($CurrentEnvironment.Name)"

    # Check for the subscription
    $subName = $CurrentEnvironment.Subscription
    if($subName -isnot [string]) {
        $subName = $subName.Name;
    }

    Select-AzureSubscription $subName;

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

    del env:\NUCMD_*

    # Load environment variables
    $env:NUCMD_ENVIRONMENT = $CurrentEnvironment.Name;
    $env:NUCMD_SUBSCRIPTION_ID = $CurrentEnvironment.Subscription.Id
    $env:NUCMD_SUBSCRIPTION_NAME = $CurrentEnvironment.Subscription.Name

    # Search for a certificate
    $cert = Get-AzureManagementCertificate $CurrentEnvironment.Subscription.Name
    if($cert) {
        $env:NUCMD_SUBSCRIPTION_THUMBPRINT = $cert.Thumbprint
    }

    # Build a service map
    $str = "";
    $CurrentEnvironment.Services.Values | where { $_.Datacenter -eq 0 } | foreach {
        $str += "$($_.Kind)|$($_.Uri);"
    }
    $env:NUCMD_SERVICE_MAP = $str
}
