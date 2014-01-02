<#
.SYNOPSIS
Gets the config settings for the specified service

.PARAMETER Service
The service to get configuration for
#>
function Get-NuGetServiceConfiguration {
    param(
        [Parameter(Mandatory=$true, Position=0)]$Service)

    $dep = $null;
    $type = $null;
    $Service = EnsureService $Service

    if($Service.AppSettings) {
        # Website deployment
        $type = "Website"
        $dep = $Service
    } elseif($Service.Configuration) {
        # Cloud Service deployment
        $type = "CloudService"
        $dep = $Service
    } elseif(!$Service.ID -or !$Service.Environment) {
        throw "Unknown service object"
    } else {
        Write-Host "Downloading config for $($Service.ID)..."
        $type = $Service.Type
        $dep = GetDeployment $Service
    }

    
    if($type -eq "Website") {
        $settings = $dep.AppSettings
        $dep.ConnectionStrings | foreach {
            $settings[$_.Name] = $_.ConnectionString
        }
        $settings
    } else {
        $x = [xml]($dep.Configuration);
        $table = @{};
        $role = $x.ServiceConfiguration.Role | select -first 1
        Write-Host "Using config for role '$($role.name)'"
        $role.ConfigurationSettings.Setting | foreach {
            $table.Add($_.name, $_.value)
        }
        $table
    }
}