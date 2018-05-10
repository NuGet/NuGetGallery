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