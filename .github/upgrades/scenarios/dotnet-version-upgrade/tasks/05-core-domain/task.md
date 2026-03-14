# 05-core-domain: Upgrade NuGetGallery.Core

Upgrade the core domain library (NuGetGallery.Core) that consolidates business logic and depends on multiple service layers (Entities, Validation, FeatureFlags, Validation.Issues, Messaging.Email).

This project bridges infrastructure services and the web application. Has 165+ API issues and 5 incompatible packages.

**Key concerns**:
- 5 incompatible packages including Microsoft.WindowsAzure.ConfigurationManager
- 165+ API issues (System.Web dependencies, data services client APIs)
- Referenced by main web app and multiple tool projects

**Done when**:
- NuGetGallery.Core targets net10.0
- All incompatible packages addressed
- Builds without errors
- Tests pass (NuGetGallery.Core.Facts also upgraded)
- Higher tiers still build

