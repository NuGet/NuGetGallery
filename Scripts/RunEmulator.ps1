$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

$rootPath = resolve-path(join-path $ScriptRoot "..")
if(!(Test-Path "$rootPath\_AzurePackage\NuGetGallery.csx")) {
    throw "Run 'Package.ps1 -ForEmulator' before running this script"
}
if(!(Test-Path "$ScriptRoot\NuGetGallery.emulator.cscfg")) {
    throw "Cannot find NuGetGallery.emulator.cscfg in $ScriptRoot"
}

& "$AzureToolsRoot\Emulator\csrun.exe" /run:"$rootPath\_AzurePackage\NuGetGallery.csx;$ScriptRoot\NuGetGallery.emulator.cscfg" /launchBrowser /useiisexpress