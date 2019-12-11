[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TestCategories
)

$dividerSymbol = "~"
$fullDivider = ($dividerSymbol * 20)
$halfDivider = ($dividerSymbol * 10)

& "$PSScriptRoot\BuildTests.ps1" | Out-Host

Write-Host $fullDivider

$failedTests = New-Object System.Collections.ArrayList

Function Wait-ForServiceStart($MaxWaitSeconds) {
    $configurationFile = $env:ConfigurationFilePath
    if ($null -eq $configurationFile) {
        Write-Error "Configuration file path environment variable is not specified"
        return $false;
    }
    if (-not (Test-Path $configurationFile)) {
        Write-Error "Missing configuration file: $configurationFile"
        return $false
    }
    $configuration = Get-Content $configurationFile | ConvertFrom-Json;
    if ($null -eq $configuration.Slot) {
        Write-Error "`"Slot`" property was not found in the test configuration object: $configurationFile"
        return $false
    }
    $baseUrlPropertyName = if ($configuration.Slot -eq "staging") { "StagingBaseUrl" } else { "ProductionBaseUrl" }
    if ($null -eq $configuration.$baseUrlPropertyName) {
        Write-Error "`"$($baseUrlPropertyName)`" property was not found in the test configuration object: $configurationFile"
        return $false
    }

    $galleryUrl = $configuration.$baseUrlPropertyName
    $response = $null
    Write-Host "$(Get-Date -Format s) Sleeping before querying the service";
    Start-Sleep -Seconds 120
    Write-Host "$(Get-Date -Format s) Waiting until service ($galleryUrl) responds with non-502"
    $start = Get-Date
    $maxWait = New-TimeSpan -Seconds $MaxWaitSeconds
    do
    {
        if ($null -ne $response) {
            Start-Sleep -Seconds 5
        }
        $response = try { Invoke-WebRequest -Uri $galleryUrl -Method Get -UseBasicParsing } catch [System.Net.WebException] {$_.Exception.Response}
        if ($response.StatusCode -eq 502) {
            $elapsed = (Get-Date) - $start
            if ($elapsed -ge $maxWait) {
                Write-Error "$(Get-Date -Format s) Service start timeout expired"
                return $false
            } else {
                Write-Host "$(Get-Date -Format s) Still waiting for the service to stop responding with 502 after $elapsed"
            }
        } else {
            Write-Host "$(Get-Date -Format s) Got a $($response.StatusCode) response";
        }
    } while($response.StatusCode -eq 502)

    return $true;
}

Function Output-Job {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline)]$job
    )

    Write-Host $fullDivider
    $jobName = $job.Name
    Write-Host "Finished testing test category $jobName."
    Write-Host $halfDivider
    Receive-Job $job | Out-Host
    Write-Host $halfDivider
    $jobState = $job.State
    if ($jobState -eq "Completed") {
        Write-Host "Test category $jobName succeeded!"
    } else {
        $failedTests.Add($jobName) | Out-Null
        if ($jobState -eq "Failed") {
            Write-Host "Test category $jobName failed!"
        } elseif ($jobState -eq "Stopped") {
            Write-Host "Test category $jobName was stopped!"
        } else {
            Write-Host "Test category $jobName had unexpected state of $jobState!"
        }
    }
    Remove-Job $job | Out-Host
}

$serviceAvailable = Wait-ForServiceStart -MaxWaitSeconds 300
if (-not $serviceAvailable) {
    exit 1;
}

$finished = $false
$TestCategoriesArray = $TestCategories.Split(';')
Write-Host "Testing $($TestCategoriesArray -join ", ")"
Write-Host $fullDivider
try {
    $TestCategoriesArray `
        | ForEach-Object {
            # Kill existing job
            $job = Get-Job -Name $_ -ErrorAction Ignore | Remove-Job -Force | Out-Host

            # Start new job
            Start-Job -Name $_ -ScriptBlock {
                param(
                    [string]$testCategory,
                    [string]$scriptRoot
                )

                Set-Location -Path $scriptRoot | Out-Host
                $script = "$scriptRoot\RunTests.ps1"
                Write-Host "Running $script with test category $testCategory"
                & $script -TestCategory $testCategory | Out-Host
                if ($LastExitCode) {
                    throw "$script with test category $testCategory failed!"
                }
            } -ArgumentList ($_, $PSScriptRoot)

            Write-Host "Started testing $_"
        } | Out-Null
    
    Write-Host $fullDivider

    do {
        Write-Host "Waiting for $($TestCategoriesArray -join ", ")"
        $jobs = Get-Job -Name $TestCategoriesArray
        $job = Wait-Job $jobs -Any
        Output-Job $job | Out-Host
        $TestCategoriesArray = @($TestCategoriesArray | Where-Object { $_ -ne $job.Name })
        Write-Host $fullDivider
    } while ($TestCategoriesArray.count -gt 0)

    Write-Host "All functional tests finished"

    $finished = $true
} finally {
    if (!($finished)) {
        Write-Host $fullDivider
        Write-Host "Testing failed or was cancelled!"
        Get-Job -Name $TestCategoriesArray `
            | ForEach-Object {
                Stop-Job $_ | Out-Host
                Output-Job $_ | Out-Host
            } | Out-Host
    }
}

Write-Host $fullDivider
if ($failedTests.Count -gt 0) {
    Write-Host "Some functional tests failed!"
    $failedTests | ForEach-Object { Write-Host "Test category $_ failed" }
    exit 1
}

Write-Host "All functional tests succeeded!"
exit 0