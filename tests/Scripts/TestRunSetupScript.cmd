@echo off

set RunFunctionalTests=true
set TestAccountName=testnuget@gmail.com
set TestAccountPassword=<password>
set TestEmailServerHost=imap.gmail.com

Echo Clear previously defined GalleryUrl and branch.
set galleryUrl=
set branch=
Echo.

Echo Set the gallery Url...
set galleryUrl=%1
if "%galleryUrl%" == "" (
Echo Setting galleryUrl to the default - int.nugettest.org
set galleryUrl=https://int.nugettest.org/
)
Echo The gallery Url was set to %galleryUrl%
Echo.

Echo Set branch for NuGet.exe...
set branch=%2
if "%branch%" == "" (
Echo Setting branch to the default - master branch
set branch=master
)
Echo The NuGet.exe branch was set to %branch%
Echo.

Echo copy the latest nuget.exe from %branch% branch...
copy /y \\nuget-ci\drops\%branch%\latest-successful\nuget.exe .\..\.nuget\ 
Echo.

Echo Set APIKey for the gallery...
.\..\.nuget\nuget.exe setAPIKey <api key> -Source %galleryUrl%api/v2/package/
Echo Done.

exit /b 0
