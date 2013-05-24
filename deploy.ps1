function Set-AppSetting($x, [string]$name, [string]$value) {
    $setting = $x.configuration.appSettings.add | where { $_.key -eq $name }
    if($setting) {
        $setting.value = $value
        "Patched $name setting."
    } else {
        "Unknown App Setting: $name."
    }
}

# Gather deployment info
$Commit = git rev-parse --short HEAD
$Branch = $env:branch
$Date = [TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow, "Pacific Standard Time")

# Load web.config
$webConfigPath = Join-Path $env:DEPLOYMENT_TEMP "web.config"
if(!(Test-Path $webConfigPath)) {
    throw "Web.config not found at $webConfigPath!"
}
$webConfig = [xml](cat $webConfigPath)
Set-AppSetting $webConfig "Gallery.ReleaseBranch" $Branch
Set-AppSetting $webConfig "Gallery.ReleaseSha" $Commit
Set-AppSetting $webConfig "Gallery.ReleaseTime" ($Date.ToString("yyyy-MM-dd hh:mm:ss tt") + " Pacific")
$webConfig.Save($webConfigPath)