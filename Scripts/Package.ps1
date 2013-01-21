param(
  [Parameter(Mandatory=$false)][string]$ReleaseSha,
  [Parameter(Mandatory=$false)][string]$ReleaseBranch,
  [Parameter(Mandatory=$false)][string]$VMSize = $null,
  [Parameter(Mandatory=$false)][string]$AzureSDKRoot = $null,
  [Parameter(Mandatory=$false)][string]$SSLCertificateName = $null,
  [Parameter(Mandatory=$false)][switch]$EnableRDP,
  [Parameter(Mandatory=$false)][switch]$ForEmulator,
  [Parameter(Mandatory=$false)][switch]$PassThru
)

# If there's a NUGET_GALLERY_VM_SIZE environment variable, use it
if(Test-Path env:\NUGET_GALLERY_VM_SIZE) {
  $VMSize = $env:NUGET_GALLERY_VM_SIZE
} elseif(!$VMSize) {
  $VMSize = "Small"
}
Write-Host "Using VMSize: $VMSize"

# Import common stuff
$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

# Gather environment information
$rootPath = resolve-path(join-path $ScriptRoot "..")
$websitePath = join-path $rootPath "Website"
$webConfigPath = join-path $websitePath "Web.config"
$webConfigBakPath = join-path $ScriptRoot "Web.config.bak"
$csdefPath = Join-Path $ScriptRoot "NuGetGallery.csdef"
$csdefBakPath = Join-Path $ScriptRoot "NuGetGallery.csdef.bak"
$rolePropertiesPath = join-path $ScriptRoot "NuGetGallery.RoleProperties.txt"

$cspkgFolder = join-path $rootPath "_AzurePackage"
$gitPath = (get-command git)
$binPath = join-path $websitePath "bin"

# Make a backup of the web.config so we can avoid polluting Git history
# All we put in web.config is the Release SHA, Branch and Time, so it's not _essential_ that this be restored.
# Similarly, we only put VMSize in the csdef.
cp $webConfigPath $webConfigBakPath
cp $csdefPath $csdefBakPath

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
$cspkgPath = join-path $cspkgFolder "NuGetGallery_$commitSha.cspkg"

if(Test-Path $cspkgFolder) {
  del $cspkgFolder -Force -Recurse
}
mkdir $cspkgFolder | out-null

#Release Tag stuff
Write-Host "Setting the release tags"

function set-vmsize {
    param($path, $size)

    $settings = [xml](get-content $path)
    $settings.ServiceDefinition.WebRole.vmsize = $size;
    $resolvedPath = resolve-path($path)
    $settings.save($resolvedPath)
}

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

function Enable-SSL {
    param($path, $certName)

    $settings = [xml](get-content $path)
    $epsNode = $settings.ServiceDefinition.WebRole.Endpoints
    $newNode = $settings.CreateElement("InputEndpoint");
    $newNode.name = "SSL"
    $newNode.protocol ="https"
    $newNode.port = "443"
    $newNode.certificate = $certName
    $epsNode.AppendChild($newNode)

    $bindingsNode = $settings.ServiceDefinition.WebRole.Sites.Site.Bindings
    $newNode = $settings.CreateElement("Binding")
    $newNode.name = "SSLBinding"
    $newNode.endpointName = "SSL";
    $bindingsNode.AppendChild($newNode)

    $resolvedPath = resolve-path($path) 
    $settings.Save($resolvedPath);
}

function Enable-RDP {
    param($path)

    $settings = [xml](get-content $path)
    $importsNode = $settings.ServiceDefinition.WebRole.Imports
    
    $raNode = $settings.CreateElement("Import");
    $raNode.moduleName = "RemoteAccess"
    $importsNode.AppendChild($raNode)
    
    $rfNode = $settings.CreateElement("Import");
    $rfNode.moduleName = "RemoteForwarder"
    $importsNode.AppendChild($rfNode)

    $resolvedPath = resolve-path($path) 
    $settings.Save($resolvedPath);
}

if($SSLCertificateName) {
  Enable-SSL -path $csdefPath -certName $SSLCertificateName
}
if($EnableRDP) {
  Enable-RDP -path $csdefPath
}

set-vmsize -path $csdefPath -size $VMSize
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
$azureSdkPath = Get-AzureSdkPath $azureSdkPath

& "$azureSdkPath\bin\cspack.exe" "$csdefPath" /out:"$cspkgPath" /role:"Website;$websitePath" /sites:"Website;Web;$websitePath" /rolePropertiesFile:"Website;$rolePropertiesPath" $copyOnlySwitch
if ($lastexitcode -ne 0) {
  throw "CSPack Failed with Exit Code: $lastexitcode"
  exit 1 
}

cp $webConfigBakPath $webConfigPath
cp $csdefBakPath $csdefPath
del $webConfigBakPath
del $csdefBakPath

$startupScripts | ForEach-Object {
  rm (Join-Path $binPath $_)
}

$packageDateTime = (Get-Date -format "MMMdd @ HHmm")
print-success("Azure $env:NUGET_GALLERY_ENV package and configuration dropped to $cspkgFolder.")
print-success("Deployment Name: $packageDateTime ($packageSha on $commitBranch)")

write-host ""

if($PassThru) {
  # Write out the package file
  Get-Item $cspkgPath
}

Exit 0