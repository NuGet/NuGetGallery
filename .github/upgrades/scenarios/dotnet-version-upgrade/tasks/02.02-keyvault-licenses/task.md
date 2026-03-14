# 02.02-keyvault-licenses: Upgrade NuGet.Services.KeyVault, Licenses

# 02.02: Upgrade KeyVault, Licenses

## Objective
Upgrade 2 Azure-dependent libraries to net10.0.

## Projects
- src/NuGet.Services.KeyVault/NuGet.Services.KeyVault.csproj (net472;netstandard2.0)
- src/NuGet.Services.Licenses/NuGet.Services.Licenses.csproj (net472;netstandard2.0)

## Changes
- Add net10.0 to TargetFrameworks
- Update any Microsoft.Extensions.* packages if flagged

## Done When
- Both projects multi-target including net10.0
- Projects build without errors
