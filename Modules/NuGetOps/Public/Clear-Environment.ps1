<#
.SYNOPSIS
Clears the active NuGet Environment
#>
function Clear-Environment {
    if($Global:OldBgColor) {
        $Host.UI.RawUI.BackgroundColor = $Global:OldBgColor
        del variable:\OldBgColor
    }
    $prod = _IsProduction
    _RefreshGitColors
    if($prod) {
        Clear-Host
    }
    del variable:\CurrentEnvironment
    del variable:\CurrentDeployment
}