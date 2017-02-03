# Delete leftover tests
Get-ChildItem $env:Build_SourcesDirectory | Where-Object {$_.Extension -eq '.trx' -Or $_.Name -match 'functionaltests.*.xml'} | ForEach-Object {
    Remove-Item $_
}

# Determine the url of the gallery we are testing.
function GetUrl()
{
    param([string] $xmlString)
    $stagingsectionindex = $xmlString.IndexOf("<DeploymentSlot>Staging</DeploymentSlot>")
    $startindex =  $xmlString.IndexOf("<Url>",$stagingsectionindex)
    $endindex = $xmlString.IndexOf("</Url>",$startindex)
    $stagingUrl =  $xmlString.Substring($startindex+5, $endindex-($startindex +5))
    return $stagingUrl
}

if ($env:Slot -eq "Production")
{
    $GalleryUrl = $env:SERVICEROOT
}
else
{
    # Use the Azure management certificate to find the url of the desired slot
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