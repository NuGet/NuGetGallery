# 04-business-services: Upgrade Tier 3-4 Business Services

Upgrade 4 business service libraries spanning two tiers: Tier 3 (NuGet.Services.Messaging, NuGet.Services.Validation) and Tier 4 (NuGet.Services.CatalogValidation, NuGet.Services.Messaging.Email).

These provide validation, messaging, and catalog services. Tier 3 projects depend on Tiers 1-2. Tier 4 depends on Tiers 1-3.

**Key concerns**:
- NuGet.Services.Messaging.Email: incompatible package (NuGet.StrongName.AnglicanGeek.MarkdownMailer) — find alternative
- API compatibility issues across validation and messaging layers

**Done when**:
- All 4 projects target net10.0
- Incompatible packages replaced
- Projects build without errors
- Tests pass
- Tiers 5-9 still build

