$AuthCredentials = "{0}:{1}" -f $OctopusParameters['Vsts.UserName'], $OctopusParameters['Vsts.Password']
$Base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($AuthCredentials))
$Headers = @{Authorization = ("Basic {0}" -f $Base64AuthInfo)}

$QueueParameters = @{}
$QueueParameters.Add('ConfigurationName', $OctopusParameters['Vsts.EndToEnd.Jobs.ConfigurationName'])

$QueueBody = @{
    definition = @{
        id = $OctopusParameters['Vsts.EndToEnd.BuildDefinitionId']
    };
    parameters = (ConvertTo-Json $QueueParameters)
}

$QueuedBuild = Invoke-RestMethod `
    -Method Post `
    -ContentType 'application/json' `
    -Uri "https://nuget.visualstudio.com/DefaultCollection/NuGetBuild/_apis/build/builds?api-version=2.0" `
    -Headers $Headers `
    -Body (ConvertTo-Json $QueueBody)

$BuildId = $QueuedBuild.id
Write-Host "Started build $BuildId to run end-to-end tests."

do {
    Start-Sleep -s 20
    
    $BuildResponse = Invoke-WebRequest `
        -UseBasicParsing `
        -Method Get `
        -Uri "https://nuget.visualstudio.com/DefaultCollection/NuGetBuild/_apis/build/builds/$($BuildId)?api-version=2.0" `
        -Headers $Headers

    $Build = ConvertFrom-Json $BuildResponse
    
    Write-Host "Test run state:"  $Build.status
    Write-Host "******************************************"
} until ($Build.status -eq 'completed')

Write-Host "Test Run Completion Status:"  $Build.result
Write-Host "******************************************"
$BuildUri = $Build.uri
Write-Host "For more details checkout:"  "https://nuget.visualstudio.com/NuGetBuild/_build/index?buildId=$BuildUri"

if ($Build.result -ne 'succeeded') {
    throw 'Test run failed'
}