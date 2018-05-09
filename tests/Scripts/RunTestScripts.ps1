[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TestCategories
)

$dividerSymbol = "~"

& "$PSScriptRoot\BuildTests.ps1" | Out-Host

Write-Host ($dividerSymbol * 20)

$failedTests = New-Object System.Collections.ArrayList

Function Output-Job {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline)]$job
    )

    Write-Host ($dividerSymbol * 20)
    $jobName = $job.Name
    Write-Host "Finished testing test category $jobName."
    Write-Host ($dividerSymbol * 10)
    Receive-Job $job | Out-Host
    Write-Host ($dividerSymbol * 10)
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
try {
    $TestCategoriesArray `
        | ForEach-Object {
            # Kill existing job
            $job = Get-Job -Name $_ -ErrorAction SilentlyContinue | Remove-Job -Force | Out-Host

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
                    throw "$script failed!"
                }
            } -ArgumentList ($_, $PSScriptRoot)
            
            Write-Host "Started testing $_"
        } | Out-Null

    do {
        $jobs = Get-Job -Name $TestCategoriesArray
        $job = Wait-Job $jobs -Any
        Output-Job $job | Out-Host
        $TestCategoriesArray = $TestCategoriesArray | Where-Object { $_ -ne $job.Name }
    } while ($TestCategoriesArray.count -gt 0)

    $finished = $true
} finally {
    if (!($finished)) {
        Write-Host "Testing failed!"
        Get-Job -Name $TestCategoriesArray `
            | ForEach-Object {
                Stop-Job $_ | Out-Host
                Output-Job $_ | Out-Host
            } | Out-Host
    }
}

Write-Host ($dividerSymbol * 20)
if ($failedTests.Count -gt 0) {
    Write-Host "Some functional tests failed!"
    $failedTests | ForEach-Object { Write-Host "Test category $_" }
    exit 1
}

Write-Host "All functional tests succeeded!"
exit 0