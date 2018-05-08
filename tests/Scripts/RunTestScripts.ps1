param(
    [string]$TestCategories
)

$dividerSymbol = "~"

$failedTests = New-Object System.Collections.ArrayList

ForEach -Parallel ($TestCategory in $TestCategories.Split(';')) {
    Write-Output ($dividerSymbol * 20)
    Write-Output "Testing $TestCategory."
    Write-Output ($dividerSymbol * 10)
    
    & $env:COMSPEC /c "$PSScriptRoot\Run$TestCategory.bat"
    
    Write-Output ($dividerSymbol * 10)
    
    Write-Output "Finished testing $TestCategory."
    if ($LastExitCode) {
        Write-Output "$TestCategory failed!"
        $failedTests.Add($TestCategory) | Out-Null
    } else {
        Write-Output "$TestCategory succeeded!"
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