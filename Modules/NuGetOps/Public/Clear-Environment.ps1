<#
.SYNOPSIS
Clears the active NuGet Environment
#>
function Clear-Environment {
    if($Global:OldBgColor) {
        $Host.UI.RawUI.BackgroundColor = $Global:OldBgColor
        del variable:\OldBgColor
    }
    _RefreshGitColors
    del variable:\CurrentEnvironment
    del variable:\CurrentDeployment
}