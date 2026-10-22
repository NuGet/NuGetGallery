# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

dotnet dev-certs https --check --trust | Out-Host
if ($LASTEXITCODE -eq 0)
{
    Write-Host "A trusted dev certificate is already available."
    return
}

$certificateBaseName = "aspire-dev-cert-$([Guid]::NewGuid().ToString('N'))"
$certificatePath = Join-Path $env:TEMP "$certificateBaseName.crt"
$keyPath = Join-Path $env:TEMP "$certificateBaseName.key"
try
{
    dotnet dev-certs https -ep $certificatePath --format Pem --no-password | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "Failed to export dev cert."
    }

    $importedCertificate = Import-Certificate `
        -FilePath $certificatePath `
        -CertStoreLocation Cert:\LocalMachine\Root `
        -ErrorAction Stop
    $importedCertificate | Out-Host
    if (-not $importedCertificate)
    {
        throw "Failed to import dev cert."
    }

    Write-Host "Dev certificate trusted successfully."
}
finally
{
    Remove-Item $certificatePath, $keyPath -Force -ErrorAction SilentlyContinue
}
