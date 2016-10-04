@echo off

Echo Clear previously defined SearchServiceUrl and IndexBaseUrl.
set SearchServiceUrl=
set IndexBaseUrl=
Echo.

Echo Set the SearchService Url...
set SearchServiceUrl=%1
if "%SearchServiceUrl%" == "" (
Echo Setting Search service base url to the default - http://nuget-int-0-v2v3search.cloudapp.net/
set SearchServiceUrl=http://nuget-int-0-v2v3search.cloudapp.net/
)
Echo The search service base Url was set to %SearchServiceUrl%
Echo.

Echo Set IndexBase Url...
set IndexBaseUrl=%2
if "%IndexBaseUrl%" == "" (
Echo Setting IndexBaseUrl to the default - http://api.int.nugettest.org/v3-index/index.json
set branch=http://api.int.nugettest.org/v3-index/index.json
)
Echo The search service base Url was set to %IndexBaseUrl%
Echo.

exit /b 0
