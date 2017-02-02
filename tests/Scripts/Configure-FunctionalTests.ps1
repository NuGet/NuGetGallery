function GetUrl()
{
	param([string] $xmlString)
	$stagingsectionindex = $xmlString.IndexOf("<DeploymentSlot>Staging</DeploymentSlot>")
	$startindex =  $xmlString.IndexOf("<Url>",$stagingsectionindex)
	$endindex = $xmlString.IndexOf("</Url>",$startindex)
	$stagingUrl =  $xmlString.Substring($startindex+5, $endindex-($startindex +5))
	return $stagingUrl
}

if($env:Slot -eq "Production")
{
    $GalleryUrl = $env:SERVICEROOT
}
else
{
	$dict = New-Object "System.Collections.Generic.Dictionary``2[System.String,System.String]"
	$dict.Add('x-ms-version', ' 2014-02-01')
	$uri =  "https://management.core.windows.net/$env:SubscriptionId/services/hostedservices/$env:CloudServiceName" + "?embed-detail=true"
	$cert = dir "cert:\LocalMachine\My\$env:AzureCertificateThumbprint"
    try
    {
	    $response = Invoke-WebRequest -Uri $uri -Certificate $cert -Headers $dict -Method GET -UseBasicParsing
	    $url = GetUrl "$response.Content"
	    $url = $url.Replace("http","https")
        $GalleryUrl = $url
    }
    catch
    {
        Write-Host "Failed to retrieve URL for testing"
        Exit 1
    }
}

Write-Host "Using the following GalleryURL: " + $GalleryUrl
Write-Host "##vso[task.setvariable variable=GalleryUrl;]$GalleryUrl"