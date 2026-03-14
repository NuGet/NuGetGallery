# 02-foundation-libraries: Upgrade Tier 1 Foundation Libraries

Upgrade the 7 foundation libraries with zero internal project dependencies: NuGet.Services.Contracts, NuGet.Services.Entities, NuGet.Services.FeatureFlags, NuGet.Services.KeyVault, NuGet.Services.Licenses, NuGet.Services.Owin, NuGet.Services.Storage.

These projects form the base of the dependency graph. Most target net472 or multi-target net472;netstandard2.0/2.1. Changes are primarily TFM updates and package version bumps. NuGet.Services.Storage has the most API compatibility issues (185) in this tier.

**Key concerns**:
- NuGet.Services.Storage: 185+ API issues (behavioral changes in Azure SDK)
- NuGet.Services.Owin: 1 incompatible package (Microsoft.Owin) — assess alternatives or keep for compatibility adapter
- Package updates: 15 packages need version updates across tier

**Done when**:
- All 7 projects target net10.0 (single TFM or multi-target including net10.0)
- All packages updated to compatible versions
- Projects build without errors
- Unit tests for these projects pass
- Higher tiers (still on old framework) still build successfully

