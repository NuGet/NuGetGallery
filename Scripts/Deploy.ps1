param(
    $subscriptionId        = $env:NUGET_GALLERY_AZURE_SUBSCRIPTION_ID,
    $serviceName           = $env:NUGET_GALLERY_AZURE_SERVICE_NAME,
	$storageServiceName    = $env:NUGET_GALLERY_AZURE_STORAGE_ACCOUNT_NAME,
    $certificateThumbprint = $env:NUGET_GALLERY_AZURE_MANAGEMENT_CERTIFICATE_THUMBPRINT,
    $commitRevision,
    $commitBranch
)

$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

require-param -value $subscriptionId -paramName "subscriptionId"
require-param -value $serviceName -paramName "serviceName"
require-param -value $storageServiceName -paramName "storageServiceName"
require-param -value $certificateThumbprint -paramName "certificateThumbprint"

require-module -name "WAPPSCmdlets"

function await-operation($operationId)
{
	$status = Get-OperationStatus -SubscriptionId $subscriptionId -Certificate $certificate -OperationId $operationId
	while ([string]::Equals($status, "InProgress"))
	{
		sleep -Seconds 1
		$status = Get-OperationStatus -SubscriptionId $subscriptionId -Certificate $certificate -OperationId $operationId
	}
	return $status
}

function await-status($status)
{
	$deployment = Get-Deployment -ServiceName $serviceName -Slot Staging -Certificate $certificate -SubscriptionId $subscriptionId
	while (-not([string]::Equals($deployment.status, $status)))
	{
		sleep -Seconds 1
		$deployment = Get-Deployment -ServiceName $serviceName -Slot Staging -Certificate $certificate -SubscriptionId $subscriptionId
	}
}

function await-start()
{
	$deployment = Get-Deployment -ServiceName $serviceName -Slot Staging -Certificate $certificate -SubscriptionId $subscriptionId
	$roleInstances = Get-RoleInstanceStatus -ServiceName $serviceName -RoleInstanceList $deployment.RoleInstanceList -Certificate $certificate -SubscriptionId $subscriptionId
	$roleInstancesThatAreNotReady = $roleInstances.RoleInstances | where-object { $_.InstanceStatus -ne "Ready" }
	while ($roleInstancesThatAreNotReady.Count -gt 0)
	{
		sleep -Seconds 1
		$deployment = Get-Deployment -ServiceName $serviceName -Slot Staging -Certificate $certificate -SubscriptionId $subscriptionId
		$roleInstances = Get-RoleInstanceStatus -ServiceName $serviceName -RoleInstanceList $deployment.RoleInstanceList -Certificate $certificate -SubscriptionId $subscriptionId
		$roleInstancesThatAreNotReady = $roleInstances.RoleInstances | where-object { $_.InstanceStatus -ne "Ready" }
	}
	
	$deployment = Get-Deployment -ServiceName $serviceName -Slot Staging -Certificate $certificate -SubscriptionId $subscriptionId
	while (-not([string]::Equals($deployment.status, "Running")))
	{
		sleep -Seconds 1
		$deployment = Get-Deployment -ServiceName $serviceName -Slot Staging -Certificate $certificate -SubscriptionId $subscriptionId
	}
}

"Locating Git"
$gitCommand = get-command git;
if ($gitCommand -eq $null) { write-error "Cound not locate path to Git with 'Get-Command git'"; exit 1 }
$gitPath = $gitCommand.Definition

"Getting commit revision and branch (via '$gitPath')"
if ($commitRevision -eq $null) { $commitRevision = (& "$gitPath" rev-parse --short HEAD) }
if ($commitBranch -eq $null) { $commitBranch = (& "$gitPath" name-rev --name-only HEAD) }

"Getting Azure management certificate $certificateThumbprint"
$certificate = (get-item cert:\CurrentUser\MY\$certificateThumbprint)

"Building deployment name from date and revision"
$deploymentLabel = "$((get-date).ToString("MMM dd @ HHmm")) ($commitRevision on $commitBranch; auto-deployed)"
$deploymentName = "$((get-date).ToString("yyyyMMddHHmmss"))-$commitRevision-$commitBranch-auto"

"Locating Azure package and configuration"
$packageLocation = join-path (resolve-path(join-path $ScriptRoot "..")) "_AzurePackage"
$cspkgFile = join-path $packageLocation "NuGetGallery.cspkg"
$cscfgFile = join-path $packageLocation "NuGetGallery.cscfg"

"Checking for existing staging deployment on $serviceName"
$deployment = Get-Deployment -ServiceName $serviceName -Slot Staging -Certificate $certificate -SubscriptionId $subscriptionId
if ($deployment -ne $null -and $deployment.Name -ne $null) {
  "Deleting existing staging deployment $($deployment.Name) on $serviceName"
  $operationId = (remove-deployment -subscriptionId $subscriptionId -certificate $certificate -slot Staging -serviceName $serviceName ).operationId
  await-operation($operationId)
}

"Creating new staging deployment $deploymentName on $serviceName"
$operationId = (new-deployment -subscriptionId $subscriptionId -certificate $certificate -ServiceName $serviceName -storageServiceName $storageServiceName -slot Staging -Package $cspkgFile -Configuration $cscfgFile -Name $deploymentName -Label $deploymentLabel).operationId
await-operation($operationId)
await-status("Suspended")

"Starting staging deployment $deploymentName on $serviceName"
$operationId = (set-deploymentstatus -subscriptionId $subscriptionId -serviceName $serviceName -slot Staging -status Running -certificate $certificate).operationId
await-operation($operationId)
await-start

"Moving staging deployment $deploymentName to productionon on $serviceName"
move-deployment -subscriptionId $subscriptionId -serviceName $serviceName -certificate $certificate -name $deploymentName

