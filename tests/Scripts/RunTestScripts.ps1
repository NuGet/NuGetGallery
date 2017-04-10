param(
    [string]$TestCategories
)

$dividerSymbol = "~"

$failedTests = New-Object System.Collections.ArrayList

$TestCategories.Split(';') | ForEach-Object {
    Write-Output ($dividerSymbol * 20)
    Write-Output "Testing $_."
    Write-Output ($dividerSymbol * 10)
    
    & $env:COMSPEC /c "$PSScriptRoot\Run$_.bat"
    
    Write-Output ($dividerSymbol * 10)
    
    Write-Output "Finished testing $_."
    if ($LastExitCode) {
        Write-Output "$_ failed!"
        $failedTests.Add($_) | Out-Null
    } else {
        Write-Output "$_ succeeded!"
    }
}

Write-Output ($dividerSymbol * 20)
if ($failedTests.Count -gt 0) {
    Write-Output "Some functional tests failed!"
    $failedTests | ForEach-Object { Write-Output $_ }
    exit 1
}

Write-Output "All functional tests succeeded!"
exit 0