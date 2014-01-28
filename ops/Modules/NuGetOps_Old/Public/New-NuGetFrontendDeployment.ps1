<#
.SYNOPSIS
Creates a new NuGet Frontend Deployment

.PARAMETER Service
The service to deploy the frontend to
#>
function New-NuGetFrontendDeployment {
    param(
        [Parameter(Mandatory=$true, Position=0)]$Service,
        [Parameter(Mandatory=$true)][string]$Package)

    $Service = EnsureService $Service

    if(!$Service.ID -or !$Service.Environment) {
        throw "Invalid Service object provided"
    }

    if(!$Service.Environment.Subscription) {
        throw "No Subscription is available for this environment. Do you have access?"
    }

    if(!(Test-Path $Package)) {
        throw "Could not find package $Package"
    }
    $Package = Convert-Path $Package

    $ext = [IO.Path]::GetExtension($Package)

    if($Service.Type -eq "CloudService") {
        if($ext -ne ".cspkg") {
            throw "Expected a CSPKG package!"
        }
        throw "Not yet implemented!"
    } elseif($Service.Type -eq "Website") {
        if($ext -ne ".zip") {
            throw "Expected a ZIP package!"
        }
        DeployWebsite $Service $Package
    }
}