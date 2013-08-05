# Run package restore manually. One of our packages provides a targets file, so we have to run restore first or we can't even load the project.
$MyPath = Split-Path $MyInvocation.MyCommand.Path
dir -Recurse -Filter packages.config | ForEach-Object {
    & "$MyPath\..\.nuget\nuget.exe" install -OutputDirectory "$MyPath\..\packages" $_.FullName
}