## Octopus Azure deployment script, version 1.0
## --------------------------------------------------------------------------------------
##
## This script is used to control how we deploy packages to Windows Azure. 
##
## When the script is run, the correct Azure subscription will ALREADY be selected,
## and we'll have loaded the neccessary management certificates. The Azure PowerShell module
## will also be loaded.  
##
## If you want to customize the Azure deployment process, simply copy this script into
## your NuGet package as DeployToAzure.ps1. Octopus will invoke it instead of the default 
## script. 
## 
## The script will be passed the following parameters in addition to the normal Octopus 
## variables passed to any PowerShell script. 
## 
##   $OctopusAzureSubscriptionId           // The subscription ID GUID
##   $OctopusAzureSubscriptionName         // The random name of the temporary Azure subscription record
##   $OctopusAzureServiceName              // The name of your cloud service
##   $OctopusAzureStorageAccountName       // The name of your storage account
##   $OctopusAzureSlot                     // The name of the slot to deploy to (Staging or Production)
##   $OctopusAzurePackageUri               // URI to the .cspkg file in Azure Blob Storage to deploy 
##   $OctopusAzureConfigurationFile        // The name of the Azure cloud service configuration file to use
##   $OctopusAzureDeploymentLabel          // The label to use for deployment
##   $OctopusAzureSwapIfPossible           // "True" if we should attempt to "swap" deployments rather than a new deployment

function CreateOrUpdate() 
{
    $releaseNumber = $OctopusParameters["Octopus.Release.Number"]
    $OctopusAzureDeploymentLabel = $releaseNumber + " (" + ([DateTime]::Now.ToString("dd MMM yyyy @ HHmm")) + ")"
    Write-Host "Deploying `"$OctopusAzureDeploymentLabel`""

    # Parse out the environment name
    if($OctopusAzureServiceName -notmatch "nuget-(?<env>[A-Za-z]+)-\d+-[A-Z0-9a-z]+") 
    {
        throw "Azure Service Name is invalid: $OctopusAzureServiceName"
    }
    $environment = $matches["env"]

    # Locate the config file
    $config = Join-Path $env:NuDeployCode "Deployment\Config\$environment\$OctopusAzureServiceName.cscfg"
    if(!(Test-Path $config)) 
    {
        throw "Missing Deployment Config File! Expected it at: $config. Check the NuDeployCodeRoot environment variable on your Tentacle!"
    }

    # Copy it over the current one
    Write-Host "Copying $config to $OctopusAzureConfigurationFile"
    Copy-Item $config $OctopusAzureConfigurationFile -Force

    # Get the Current Deployment
    $deployment = Get-AzureDeployment -ServiceName $OctopusAzureServiceName -Slot $OctopusAzureSlot -ErrorVariable a -ErrorAction silentlycontinue
 
    if (($a[0] -ne $null) -or ($deployment.Name -eq $null)) 
    {
        CreateNewDeployment
        return
    }

    if (($OctopusAzureSwapIfPossible -eq $true) -and ($OctopusAzureSlot -eq "Production")) 
    {
        Write-Host "Checking whether a swap is possible"
        $staging = Get-AzureDeployment -ServiceName $OctopusAzureServiceName -Slot "Staging" -ErrorVariable a -ErrorAction silentlycontinue
        if (($a[0] -ne $null) -or ($staging.Name -eq $null)) 
        {
            Write-Host "Nothing is deployed in staging"
        }
        else 
        {
            Write-Host ("Current staging deployment: " + $staging.Label)

            # Parse the release number out
            $splat = $staging.Label.Split();
            if(($splat.Length -gt 0) -and ($staging.Label.Split()[0] -eq $releaseNumber))
            {
                # We can swap! The existing deployment label matches this release!
                SwapDeployment
                return
            }
        }
    }
    
    # Get the current number of instances and poke it in to the config if we're updating an existing deployment
    #  (Octopus can do this automatically but we are already messing with CSCFG :))
    Write-Host "Reading existing Instance Count for $($deployment.ServiceName)"
    $xml = [xml](cat $OctopusAzureConfigurationFile);
    $deployment.RolesConfiguration.Keys | ForEach {
        # Find the role node affected
        $roleName = $_
        $roleXml = $xml.ServiceConfiguration.Role | where {$_.name -eq $roleName} | select -first 1

        # Put the current value in the xml
        $instanceCount = $deployment.RolesConfiguration[$roleName].InstanceCount;
        Write-Host " Setting $roleName instance count to $instanceCount"
        $roleXml.Instances.count = $instanceCount.ToString()
    }
    Write-Host "Saving config file..."
    $xml.Save($OctopusAzureConfigurationFile)
    UpdateDeployment 
}
 
function SwapDeployment()
{
    Write-Host "Swapping the staging environment to production"
    Move-AzureDeployment -ServiceName $OctopusAzureServiceName
}
 
function UpdateDeployment($deployment)
{
    Write-Host "A deployment already exists in $OctopusAzureServiceName for slot $OctopusAzureSlot. Upgrading deployment..."
    Set-AzureDeployment -Upgrade -ServiceName $OctopusAzureServiceName -Package $OctopusAzurePackageUri -Configuration $OctopusAzureConfigurationFile -Slot $OctopusAzureSlot -Mode Auto -label $OctopusAzureDeploymentLabel -Force
}
 
function CreateNewDeployment()
{
    Write-Host "Creating a new deployment..."
    New-AzureDeployment -Slot $OctopusAzureSlot -Package $OctopusAzurePackageUri -Configuration $OctopusAzureConfigurationFile -label $OctopusAzureDeploymentLabel -ServiceName $OctopusAzureServiceName
}

function WaitForComplete() 
{
    $dep = Get-AzureDeployment -ServiceName $OctopusAzureServiceName -Slot $OctopusAzureSlot

    $ready = $false
    while(!$ready) {
        Write-Host "Checking if deployment is ready yet"
        $ready = $true
        $dep.RoleInstanceList | ForEach-Object {
            Write-Host " $($_.InstanceName) = $($_.InstanceStatus)"
            if($_.InstanceStatus -ne "ReadyRole") {
                $ready = $false
            }
        }
        if(!$ready) {
            Write-Host "Sleeping for 10 seconds..."
            Start-Sleep -Seconds 10
            $dep = Get-AzureDeployment -ServiceName $OctopusAzureServiceName -Slot $OctopusAzureSlot
        }
    }

    $completeDeploymentID = $dep.DeploymentId
    Write-Host "Deployment complete; Deployment ID: $completeDeploymentID"
}

CreateOrUpdate
WaitForComplete