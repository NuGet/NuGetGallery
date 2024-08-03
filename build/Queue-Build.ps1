[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [string]$Instance,
    [string]$Project,
    [string]$User,
    [string]$Password,
    [int]$DefinitionId,
    [string]$Branch = 'master',
    [string]$SubmoduleBranch
)

$RefsHeads = 'refs\/heads\/'
$SubmoduleBranch = $SubmoduleBranch -replace $RefsHeads, ''

$Base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f $User, $Password)))
$Headers = @{Authorization=("Basic {0}" -f $Base64AuthInfo)}

# Check if there is already a build queued, so we don't queue the same build multiple times.
$ListResponse = Invoke-WebRequest -UseBasicParsing -Method Get -Uri "https://$Instance.visualstudio.com/DefaultCollection/$Project/_apis/build/builds?api-version=2.0&definitions=$DefinitionId&statusFilter=notStarted" -Headers $Headers
$QueuedBuilds = ConvertFrom-Json $ListResponse
Foreach ($QueuedBuild in $QueuedBuilds.value) {
    $QueuedBranch = $QueuedBuild.sourceBranch -replace $RefsHeads, ''
    $QueuedParameters = ConvertFrom-Json $QueuedBuild.parameters
    if ($QueuedBranch -eq $Branch -and $QueuedParameters.SubmoduleBranch -eq $SubmoduleBranch) {
        Write-Host "There is already a build queued!"
        exit 0
    }
}

# Queue the next build
$QueueParameters = @{SubmoduleBranch=$SubmoduleBranch}
$QueueBody = @{definition=@{id=$DefinitionId};sourceBranch=$Branch;parameters=(ConvertTo-Json $QueueParameters)}

$QueueResponse = Invoke-RestMethod -UseBasicParsing -Method Post -ContentType application/json -Uri "https://$Instance.visualstudio.com/DefaultCollection/$Project/_apis/build/builds?api-version=2.0" -Headers $Headers -Body (ConvertTo-Json $QueueBody)
Write-Host $QueueResponse