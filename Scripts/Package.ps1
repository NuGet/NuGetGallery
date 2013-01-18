param(
  [Parameter(Mandatory=$false)][string]$ReleaseSha,
  [Parameter(Mandatory=$false)][string]$ReleaseBranch,
  [Parameter(Mandatory=$false)][string]$AzureSDKRoot = $null,
  [Parameter(Mandatory=$false)][switch]$ForEmulator
)

# Import common stuff
$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

# Gather environment information
$rootPath = resolve-path(join-path $ScriptRoot "..")
$websitePath = join-path $rootPath "Website"
$webConfigPath = join-path $websitePath "Web.config"
$webConfigBakPath = join-path $ScriptRoot "Web.config.bak"
$csdefPath = Join-Path $ScriptRoot "NuGetGallery.csdef"
$rolePropertiesPath = join-path $ScriptRoot "NuGetGallery.RoleProperties.txt"

$cspkgFolder = join-path $rootPath "_AzurePackage"
$cspkgPath = join-path $cspkgFolder "NuGetGallery.cspkg"
$gitPath = (get-command git)
$binPath = join-path $websitePath "bin"

# Make a backup of the web.config so we can avoid polluting Git history
# All we put in web.config is the Release SHA, Branch and Time, so it's not _essential_ that this be restored.
cp $webConfigPath $webConfigBakPath

# Startup Scripts
$startupScripts = @("Startup.cmd", "Startup.ps1", "ConfigureIISLogging.cmd")

if ($commitSha -eq $null) {
    $commitSha = (& "$gitPath" rev-parse HEAD)
    $packageSha = (& "$gitPath" rev-parse --short HEAD)
} else {
  $packageSha = $commitSha
}

if ($commitBranch -eq $null) {
    $commitBranch = (& "$gitPath" name-rev --name-only HEAD)
}

if(Test-Path $cspkgFolder) {
  del $cspkgFolder -Force -Recurse
}
mkdir $cspkgFolder | out-null

#Release Tag stuff
Write-Host "Setting the release tags"

function set-appsetting {
    param($path, $name, $value)

    $settings = [xml](get-content $path)
    $setting = $settings.configuration.appSettings.selectsinglenode("add[@key='" + $name + "']")

    if ($value -ne $null) {
      $setting.value = $value.toString()
    } else {
      $setting.value = ""
    }
    $resolvedPath = resolve-path($path) 
    $settings.save($resolvedPath)
}

function disable-debug {
    param($path)

    $settings = [xml](get-content $path)
    $compilNode = $settings.configuration."system.web".compilation;
    $compilNode.debug = "false";
    $resolvedPath = resolve-path($path) 
    $settings.Save($resolvedPath);
}

set-appsetting -path $webConfigPath -name "Gallery.ReleaseBranch" -value $ReleaseBranch
set-appsetting -path $webConfigPath -name "Gallery.ReleaseSha" -value $ReleaseSha
set-appsetting -path $webConfigPath -name "Gallery.ReleaseTime" -value (Get-Date -format "dd/MM/yyyy HH:mm:ss")
disable-debug -path $webConfigPath

$startupScripts | ForEach-Object {
  cp (Join-Path $ScriptRoot $_) (Join-Path $binPath $_)
}

$copyOnlySwitch = ""
if($ForEmulator) {
  $copyOnlySwitch = "/copyOnly"
  $cspkgPath = [IO.Path]::ChangeExtension($cspkgPath, "csx")
}

# Find the most recent SDK version
if(!$azureSdkPath) {
  $azureSdkPath = (dir "$AzureToolsRoot\.NET SDK" | sort Name -desc | select -first 1).FullName
}

& "$azureSdkPath\bin\cspack.exe" "$csdefPath" /useCtpPackageFormat /out:"$cspkgPath" /role:"Website;$websitePath" /sites:"Website;Web;$websitePath" /rolePropertiesFile:"Website;$rolePropertiesPath" $copyOnlySwitch
if ($lastexitcode -ne 0) {
  throw "CSPack Failed with Exit Code: $lastexitcode"
  exit 1 
}

cp $webConfigBakPath $webConfigPath
del $webConfigBakPath

$startupScripts | ForEach-Object {
  rm (Join-Path $binPath $_)
}

$packageDateTime = (Get-Date -format "MMMdd @ HHmm")
print-success("Azure $env:NUGET_GALLERY_ENV package and configuration dropped to $cspkgFolder.")
print-success("Deployment Name: $packageDateTime ($packageSha on $commitBranch)")

write-host ""

Exit 0