param(
    $promptBeforDelete  = $true,
    $subscriptionID     = $env:NUGET_GALLERY_AZURE_SUBSCRIPTION_ID,
    $serviceName        = $env:NUGET_GALLERY_AZURE_SERVICE_NAME,
    $slot               = $env:NUGET_GALLERY_AZURE_SLOT,
    $certThumbprint     = $env:NUGET_GALLERY_AZURE_CERT_THUMBPRINT,
    $storageServiceName = $env:NUGET_GALLERY_AZURE_STORAGE_SERVICE_NAME,
    $commitSha,
    $commitBranch
)

$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

# Validate Sutff
require-param -value $promptBeforDelete -paramName "promptBeforDelete"
require-param -value $subscriptionID -paramName "subscriptionID"
require-param -value $serviceName -paramName "serviceName"
require-param -value $slot -paramName "slot"
require-param -value $certThumbprint -paramName "certThumbprint"
require-param -value $storageServiceName -paramName "storageServiceName"

# Make sure the Azure Module is there
require-module -name "WAPPSCmdlets"

# Get all the stuff ready
$gitPath = join-path (programfiles-dir) "Git\bin\git.exe"
if ($commitSha -eq $null) {
    $commitSha = (& "$gitPath" rev-parse HEAD)
}
if ($commitBranch -eq $null) {
    $commitBranch = (& "$gitPath" name-rev --name-only HEAD)
}
$certificate = (get-item cert:\CurrentUser\MY\$certThumbprint)
$deploymentLabel = "AUTO: $commitSha on $commitBranch"
$deploymentName = "AUTO-$commitSha-$commitBranch"
$packageLocation = join-path (resolve-path(join-path $ScriptRoot "..")) "_AzurePackage"
$cspkgFile = join-path $packageLocation "NuGetGallery.cspkg"
$cscfgFile = join-path $packageLocation "NuGetGallery.cscfg"


# Helper Functions
function CheckForExistingDeployment()
{
    $deployment = Get-Deployment -ServiceName $serviceName -Slot $slot -Certificate $certificate -SubscriptionId $subscriptionID
	if ($deployment.Name -ne $null)
	{
        if($promptBeforDelete) {
            $title = "Delete Exisitng Deployment"
		    $message = "The selected deployment environment is in use, would you like to delete and continue?`nHosted Service: $serviceName`nDeploymentEnvironment: $slot`nDeploymentLabel: $deploymentLabel"
		    $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", "Delete and Continue"
		    $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No", "Cancel"
		    $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)
		    $result = $host.ui.PromptForChoice($title, $message, $options, 1) 

		    switch ($result)
	        {
    	       0 { DeleteDeployment }
    	       1 {
                   print-error("Deploy was cancelled by user.")
    			   exit 1
    			 }
    	    }
        } else {
            DeleteDeployment
        }
	}
}

function DeleteDeployment()
{
    # First make sure it isn't deploying and if it is pasue it
	SuspendDeployment
    # now delete the deployment
    print-message("Deleting Deployment: In Progress")
	$removeDeployment = Remove-Deployment -Slot $slot -ServiceName $serviceName -SubscriptionId $subscriptionID -Certificate $certificate
	$opstat = WaitToCompleteNoProgress($removeDeployment.operationId)
    print-message("Deleting Deployment: $opstat")
	sleep -Seconds 10
}

function SuspendDeployment()
{
    print-message("Suspending Deployment: In Progress")
	$suspendDeployment = Set-DeploymentStatus -Slot $slot -ServiceName $serviceName -SubscriptionId $subscriptionID -Certificate $certificate -Status Suspended
    $opstat = WaitToCompleteNoProgress($suspendDeployment.operationId)
    print-message("Suspending Deployment: $opstat")
}

function WaitToCompleteNoProgress($operationId)
{
	$result = Get-OperationStatus -SubscriptionId $subscriptionID -Certificate $certificate -OperationId $operationId
	while ([string]::Equals($result, "InProgress"))
	{
		sleep -Seconds 1
		$result = Get-OperationStatus -SubscriptionId $subscriptionID -Certificate $certificate -OperationId $operationId
	}
	return $result
}

function CreateNewDeployment()
{
    # Wait till the current Deployment is deleted. This can take awhile some times. 
	$deployment = Get-Deployment -ServiceName $serviceName -Slot $slot -Certificate $certificate -SubscriptionId $subscriptionID
	while ($deployment.Name -ne $null)
	{
		sleep -Seconds 1
		$deployment = Get-Deployment -ServiceName $serviceName -Slot $slot -Certificate $certificate -SubscriptionId $subscriptionID
	}

    print-message("Creating New Deployment: In Progress")
	$newdeployment = New-Deployment -Slot $slot -Package $cspkgFile -Configuration $cscfgFile -label $deploymentLabel -Name $deploymentName -ServiceName $serviceName -StorageServiceName $storageServiceName -SubscriptionId $subscriptionID -Certificate $certificate
	$opstat = WaitToCompleteNoProgress($newdeployment.operationId)
	print-message("Creating New Deployment: $opstat")

}

function StartInstances()
{
    print-message("Starting Instances: In Progress")
	$run = Set-DeploymentStatus -Slot $slot -ServiceName $serviceName -SubscriptionId $subscriptionID -Certificate $certificate -Status Running
	$deployment = Get-Deployment -ServiceName $serviceName -Slot $slot -Certificate $certificate -SubscriptionId $subscriptionID
	$oldStatusStr = @("") * $deployment.RoleInstanceList.Count
	
	while (-not(AllInstancesRunning($deployment.RoleInstanceList))) {
		$i = 1
		foreach ($roleInstance in $deployment.RoleInstanceList) {
			$instanceName = $roleInstance.InstanceName
			$instanceStatus = $roleInstance.InstanceStatus

			if ($oldStatusStr[$i - 1] -ne $roleInstance.InstanceStatus) {
				$oldStatusStr[$i - 1] = $roleInstance.InstanceStatus
                print-message("Starting Instance '$instanceName': $instanceStatus")
			}
			$i = $i + 1
		}
		sleep -Seconds 1
		$deployment = Get-Deployment -ServiceName $serviceName -Slot $slot -Certificate $certificate -SubscriptionId $subscriptionID
	}

	$i = 1
	foreach ($roleInstance in $deployment.RoleInstanceList) {
		$instanceName = $roleInstance.InstanceName
		$instanceStatus = $roleInstance.InstanceStatus

		if ($oldStatusStr[$i - 1] -ne $roleInstance.InstanceStatus) {
			$oldStatusStr[$i - 1] = $roleInstance.InstanceStatus
			print-message("Starting Instance '$instanceName': $instanceStatus")
		}
		$i = $i + 1
	}
	
	$opstat = Get-OperationStatus -SubscriptionId $subscriptionID -Certificate $certificate -operationid $run.operationId
	print-message("Starting Instances: $opstat")
    
}

function AllInstancesRunning($roleInstanceList)
{
	foreach ($roleInstance in $roleInstanceList) {
		if ($roleInstance.InstanceStatus -ne "Ready") {
			return $false
		}
	}
	return $true
}

#Do Work Brah
CheckForExistingDeployment
CreateNewDeployment
StartInstances
exit 0
