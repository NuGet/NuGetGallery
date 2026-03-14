# 02.01-contracts-entities-featureflags: Upgrade NuGet.Services.Contracts, Entities, FeatureFlags

# 02.01: Upgrade Contracts, Entities, FeatureFlags

## Objective
Upgrade 3 simple foundation libraries to net10.0 by adding net10.0 to their multi-target configuration.

## Projects
- src/NuGet.Services.Contracts/NuGet.Services.Contracts.csproj (net472;netstandard2.0)
- src/NuGet.Services.Entities/NuGet.Services.Entities.csproj (net472;netstandard2.1)
- src/NuGet.Services.FeatureFlags/NuGet.Services.FeatureFlags.csproj (net472;netstandard2.0)

## Changes
- Add net10.0 to TargetFrameworks
- Update Newtonsoft.Json to 13.0.4 in Entities (if needed)
- Remove System.ComponentModel.Annotations from Entities (included in framework)

## Done When
- All 3 projects multi-target including net10.0
- Projects build without errors
- Unit tests pass
