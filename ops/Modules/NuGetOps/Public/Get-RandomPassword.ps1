<#
.SYNOPSIS
Returns a random, timestamped, password
#>
function Get-RandomPassword {
    # Base64-encode the Guid to add some additional characters
    [DateTime]::Now.ToString("MMMddyy") + "!" + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([Guid]::NewGuid().ToString()))
}