param(
    [string]$TestCategories
)

$dividerSymbol = "~"

$failedTests = New-Object System.Collections.ArrayList

$TestCategories.Split(';') | ForEach-Object {
    Write-Host ($dividerSymbol * 20)
    Write-Host "Testing $_."
    Write-Host ($dividerSymbol * 10)
    
    & cmd /c "$PSScriptRoot\Run$_.bat"
    
    Write-Host ($dividerSymbol * 10)
    
    Write-Host "Finished testing $_."
    if ($LastExitCode) {
        Write-Host "$_ failed!"
        $failedTests.Add($_) | Out-Null
    } else {
        Write-Host "$_ succeeded!"
    }
}

Write-Host ($dividerSymbol * 20)
if ($failedTests.Count -gt 0) {
    Write-Host "Some functional tests failed!"
    
    $failedTestsStrings = $failedTests | ForEach-Object { $_ }
    $failedTestsString = [string]::Join(", ", $failedTestsStrings)
    Write-Host "$failedTestsString failed!"
    
    exit 1
}

Write-Host "All functional tests succeeded!"
exit 0