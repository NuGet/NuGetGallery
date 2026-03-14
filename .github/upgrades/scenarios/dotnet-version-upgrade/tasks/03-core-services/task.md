# 03-core-services: Upgrade Tier 2 Core Services

Upgrade 7 core service libraries that depend only on Tier 1: NuGet.Services.Configuration, NuGet.Services.Cursor, NuGet.Services.Entities.Tests, NuGet.Services.Logging, NuGet.Services.ServiceBus, NuGet.Services.Sql, NuGet.Services.Validation.Issues.

These projects provide configuration, logging, data access, and messaging infrastructure. Changes include TFM updates, package updates, and addressing API compatibility issues.

**Key concerns**:
- NuGet.Services.Sql: 35+ API issues (database/connection handling)
- NuGet.Services.Logging: deprecated packages (Microsoft.ApplicationInsights) — replace with supported alternatives
- NuGet.Services.Configuration: 6 Microsoft.Extensions.* packages need version bumps

**Done when**:
- All 7 projects target net10.0
- Deprecated packages replaced
- All packages updated
- Projects build without errors
- Tests pass
- Tiers 3-9 (still on old framework) still build

