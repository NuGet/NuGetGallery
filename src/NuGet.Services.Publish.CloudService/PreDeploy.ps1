<#
.SYNOPSIS
   Updates certificate thumbprints in ServiceConfiguration.Cloud.cscfg based on Octopus configuration values.
.DESCRIPTION
   Add Azure.Thumbprint.xyz to the Octopus variables, where xyz is the name of the certificate.
#>

$configFile = gi "ServiceConfiguration.Cloud.cscfg"
Write-host "Updating config file named" $configFile "with instance and certificate values"
Write-host ""

$configXml = [xml](Get-Content $configFile.FullName)
foreach ($node in @($configXml.ServiceConfiguration.Role.Certificates.Certificate)) { `
	if ($OctopusParameters['Azure.Thumbprint.' + $node.name] -ne $Null) { `
		$node.thumbprint = $OctopusParameters['Azure.Thumbprint.' + $node.name]
		Write-Host "Updated thumbprint for" $node.name "to" $node.thumbprint`
	} `
} 

$configXml.Save($configFile.FullName)