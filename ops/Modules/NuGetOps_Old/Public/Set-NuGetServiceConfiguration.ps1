<#
.SYNOPSIS
Sets the config settings for the specified service

.PARAMETER Service
The service to get configuration for
#>
function Set-NuGetServiceConfiguration {
    param(
        [Parameter(Mandatory=$true, Position=0)]$Service,
        [Parameter(Mandatory=$true, Position=1)][hashtable]$Settings)

    $dep = $null;
    if($Service -is [string]) {
        if(!$CurrentEnvironment) {
            throw "This command requires an environment"
        }
        $Service = Get-NuGetService $Service -ForceSingle
    }

    if(!$Service.ID -or !$Service.Environment) {
        throw "Invalid Service object provided"
    }

    if(!$Service.Environment.Subscription) {
        throw "No Subscription is available for this environment. Do you have access?"
    }

    if($Service.Type -eq "Website") {
        Write-Host "Saving settings for $($Service.ID)..."

        RunInSubscription $Service.Environment.Subscription.Name {
            # HACK: Gallery.SqlServer is a connection string!
            $cstr = $Settings["Gallery.SqlServer"]
            if($cstr) {
                $Settings.Remove("Gallery.SqlServer")
                $cs = New-Object Microsoft.WindowsAzure.Commands.Utilities.Websites.Services.WebEntities.ConnStringInfo
                $cs.Name = "Gallery.SqlServer"
                $cs.ConnectionString = $cstr
                $cs.Type = "SQLAzure"
                Set-AzureWebsite -Name $Service.ID -AppSettings $Settings -ConnectionStrings $cs
            } else {
                Set-AzureWebsite -Name $Service.ID -AppSettings $Settings
            }
        }
    } else {
        throw "Cannot write settings to a Cloud Service. Apply the settings to a CSCFG file and upload that instead."
    }
}