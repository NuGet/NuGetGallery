$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'NuGetGallery.CookieInteropTests.csproj'
$artifacts = Join-Path $PSScriptRoot '.artifacts\cookie-interop'
$legacyExecutable = Join-Path $PSScriptRoot 'bin\Debug\net472\NuGetGallery.CookieInteropTests.exe'
$coreAssembly = Join-Path $PSScriptRoot 'bin\Debug\net10.0\NuGetGallery.CookieInteropTests.dll'

try {
    Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue

    & dotnet build $project --configuration Debug
    if ($LASTEXITCODE -ne 0) { throw "Harness build failed with exit code $LASTEXITCODE." }

    & $legacyExecutable legacy-issue $artifacts
    if ($LASTEXITCODE -ne 0) { throw "Legacy issue leg failed with exit code $LASTEXITCODE." }

    & dotnet $coreAssembly core-exchange $artifacts
    if ($LASTEXITCODE -ne 0) { throw "ASP.NET Core exchange leg failed with exit code $LASTEXITCODE." }

    & $legacyExecutable legacy-accept $artifacts
    if ($LASTEXITCODE -ne 0) { throw "Legacy accept leg failed with exit code $LASTEXITCODE." }

    Write-Host 'Cookie interoperability harness passed on net472 and net10.0.'
}
finally {
    Remove-Item (Join-Path $PSScriptRoot '.artifacts') -Recurse -Force -ErrorAction SilentlyContinue
}
