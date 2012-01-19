$scriptPath = split-path $MyInvocation.MyCommand.Path
$RootPath = resolve-path(join-path $scriptPath "..")
$WebsitePath = join-path $rootPath "Website"
$NuGetPath = Join-Path $rootPath ".nuget\NuGet.exe"
$SqlCmdPath = 'C:\Program Files\Microsoft SQL Server\100\Tools\Binn\SQLCMD.EXE'
$IISExpressPath     = 'C:\Program Files (x86)\IIS Express\iisexpress.exe'


function TruncateDatabase()
{
    &$SqlCmdPath -S .\SqlExpress -Q "Drop database NuGetGallery"
}

function AddUser()
{
    $guid = [Guid]::NewGuid()
    &$SqlCmdPath -S .\SqlExpress -d NuGetGallery -Q "Insert into Users (ApiKey, EmailAllowed) values ('$guid', 1)" > $nul
    
    Write-Host "Inserted $guid api key into database"
    return $guid
}

function RunSite()
{
    $port = 55881
    start-process $IISExpressPath "/path:$WebsitePath /port:$port /systray:false" -Window Hidden
    
    $url = "http://localhost:$port"
    Write-Host "Running site on $url"
    return $url
}

function PushPackage($version)
{
    &$NuGetPath pack Test.nuspec -Version $version
    &$NuGetPath push -Api $apiKey -Source $siteUrl Test.$version.nupkg
}

function VerifyPackage($version)
{
    $content = (&$NuGetPath list -Pre -Source "$siteUrl/api/v2") -Split ' '
    Assert-Equal "Test" $content[0]
    Assert-Equal $version $content[1]
}

function Assert-Equal($expected, $actual)
{
    if ($expected -ne $actual) {
        Write-Error "Assert failed. Expected $expected, Actual $actual"
    }
}

Push-Location $scriptPath

TruncateDatabase
.\Update-Database.ps1
$apiKey = AddUser
$siteUrl = RunSite

PushPackage('1.0.0')
VerifyPackage('1.0.0')
PushPackage('1.0.1')
VerifyPackage('1.0.1')
PushPackage('1.0.2-alpha')
VerifyPackage('1.0.2-alpha')

Get-Process iisexpress | Stop-Process

Pop-Location
