param(
    [string]$TestCategories
)

$dividerSymbol = "~"

$exitCode = 0

$TestCategories.Split(';') | ForEach-Object {
    Write-Host ($dividerSymbol * 20)
    Write-Host "Testing $_."
    Write-Host ($dividerSymbol * 10)
    
    & cmd /c "$PSScriptRoot\Run$_.bat"
    
    Write-Host ($dividerSymbol * 10)
    Write-Host "Finished testing $_. Result: $LastExitCode"
    
    if ($LastExitCode) {
        $exitCode = 1
    }
}

Write-Host ($dividerSymbol * 20)
if ($exitCode) {
    Write-Host "Some functional tests failed!"
} else {
    Write-Host "All functional tests succeeded!"
}

exit $exitCode