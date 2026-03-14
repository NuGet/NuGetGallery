# .NET 10.0 Upgrade Plan

## Overview

**Target**: Upgrade NuGet Gallery solution from .NET Framework 4.7.2 / .NET Standard 2.0-2.1 to .NET 10.0 (LTS)

**Scope**: 39 projects across 9 dependency tiers, ~276k LOC, 10,499 compatibility issues identified

### Selected Strategy
**Bottom-Up (Dependency-First)** — Upgrade from leaf nodes to root applications, tier by tier.

**Rationale**: Deep dependency graph (9 levels) with high-risk top layer. Main web application has significant breaking changes (ASP.NET Framework → ASP.NET Core, OWIN migration). Foundation libraries have low complexity. Tier-by-tier approach validates each layer before proceeding to dependent projects.

### Dependency Graph Structure

```
Tier 9: [Top-level Tools & Test Projects] (6 projects)
         ↓
Tier 8: [GitHubVulnerabilities Tools, AccountDeleter] (6 projects)
         ↓
Tier 7: [NuGetGallery Web App, DatabaseMigrationTools, Validation.Common.Job, VerifyMicrosoftPackage] (4 projects)
         ↓
Tier 6: [NuGet.Jobs.Common, NuGetGallery.Services, NuGetGallery.Core.Facts] (3 projects)
         ↓
Tier 5: [NuGetGallery.Core] (1 project)
         ↓
Tier 4: [NuGet.Services.CatalogValidation, NuGet.Services.Messaging.Email] (2 projects)
         ↓
Tier 3: [NuGet.Services.Messaging, NuGet.Services.Validation] (2 projects)
         ↓
Tier 2: [Configuration, Cursor, Logging, ServiceBus, Sql, Validation.Issues, Entities.Tests] (7 projects)
         ↓
Tier 1: [Contracts, Entities, FeatureFlags, KeyVault, Licenses, Owin, Storage] (7 foundation projects)
```

## Tasks

### 01-prerequisites: Validate .NET 10 SDK and Global Configuration

Ensure .NET 10 SDK is installed and accessible. Check for global.json files that might pin SDK versions and validate they're compatible with .NET 10 upgrade. Update if necessary.

**Done when**: 
- .NET 10 SDK validated as installed
- No global.json SDK version conflicts
- Solution can target .NET 10

---

### 02-foundation-libraries: Upgrade Tier 1 Foundation Libraries

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

---

### 03-core-services: Upgrade Tier 2 Core Services

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

---

### 04-business-services: Upgrade Tier 3-4 Business Services

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

---

### 05-core-domain: Upgrade NuGetGallery.Core

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

---

### 06-service-layer: Upgrade Tier 6 Service Layer

Upgrade 3 projects in Tier 6: NuGet.Jobs.Common (shared job infrastructure), NuGetGallery.Services (web services layer), NuGetGallery.Core.Facts (test library).

These projects depend on NuGetGallery.Core and provide job infrastructure and web service implementations.

**Key concerns**:
- NuGetGallery.Services: 472+ API issues, 12 incompatible packages (OWIN, ASP.NET MVC)
- Heavy System.Web dependencies in NuGetGallery.Services
- NuGet.Jobs.Common: consolidates multiple service dependencies

**Done when**:
- All 3 projects target net10.0
- OWIN/ASP.NET packages addressed (compatibility adapters or alternatives)
- Projects build without errors
- Tests pass
- Main web app (Tier 7) still builds on old framework

---

### 07-web-application: Upgrade Main NuGetGallery Web Application

**This is the highest-risk task.** Upgrade src/NuGetGallery/NuGetGallery.csproj — the main ASP.NET Framework web application to ASP.NET Core on .NET 10.

**Critical scope**:
- 8,046+ LOC affected by API changes
- 8,880 issues related to ASP.NET Framework (System.Web) → ASP.NET Core
- Project uses non-SDK-style format — requires conversion to SDK-style
- 51 incompatible packages (Autofac.Mvc5, OWIN packages, ASP.NET MVC/WebAPI, Lucene.Net)
- Feature migrations required:
  - System.Web.Optimization bundling → ASP.NET Core bundling/minification
  - Classic Entity Framework initialization → EF6 on .NET Core setup (keep EF6 per user preference)
  - OWIN middleware → ASP.NET Core native middleware
  - WCF services → CoreWCF
- Heavy System.Web dependencies (HttpContext, MVC controllers, routing, authentication)

**Approach**:
- Convert project to SDK-style format first
- Update TFM to net10.0
- Migrate ASP.NET Framework packages to ASP.NET Core equivalents
- Convert OWIN middleware to ASP.NET Core middleware pipeline
- Replace bundling/minification with ASP.NET Core approach
- Update authentication/authorization to ASP.NET Core Identity
- Keep Entity Framework 6 (EF6 supports .NET Core/10)
- Address WCF service hosting (CoreWCF or alternative)

**Done when**:
- Project converted to SDK-style
- Targets net10.0
- ASP.NET Core packages configured
- Middleware pipeline converted from OWIN to ASP.NET Core
- Bundling/minification replaced
- Authentication working
- Entity Framework 6 configured for .NET Core
- WCF services addressed
- Project builds without errors
- Application runs and serves requests
- Critical functionality validated (user auth, package search, package pages)

---

### 08-dependent-applications: Upgrade Tier 7-9 Applications and Tools

Upgrade remaining 16 projects spanning Tiers 7-9: database migration tools, validation jobs, GitHub vulnerability tools, account management, verification tools, and all test projects.

These are top-level applications and tools that depend on the upgraded libraries and web app.

**Projects**:
- Tier 7: DatabaseMigrationTools, Validation.Common.Job, VerifyMicrosoftPackage
- Tier 8: AccountDeleter, GitHubVulnerabilities2Db, GitHubVulnerabilities2V3, GalleryTools, NuGetGallery.Facts, VerifyGitHubVulnerabilities
- Tier 9: AccountDeleter.Facts, GitHubVulnerabilities2Db.Facts, GitHubVulnerabilities2v3.Facts, NuGet.Services.DatabaseMigration.Facts, VerifyMicrosoftPackage.Facts, GalleryTools (others at this level)

**Key concerns**:
- NuGetGallery.Facts: 1040+ API issues (largest test project, depends on web app)
- Deprecated packages in multiple tools (Microsoft.Extensions.CommandLineUtils)
- Console application hosts may need host builder updates

**Done when**:
- All 16 projects target net10.0
- All packages updated
- Projects build without errors
- All tests pass

---

### 09-final-validation: Full Solution Validation

Run comprehensive validation across the entire upgraded solution to ensure stability and compatibility.

**Validation steps**:
- Full solution clean build (all 39 projects)
- Run complete test suite (all xUnit tests across all test projects)
- Verify no package conflicts or dependency issues
- Check for deprecated API warnings
- Run static analysis (if configured)
- Smoke test main web application (basic functionality)

**Done when**:
- Solution builds successfully in Release mode
- All tests pass (or documented failures are acceptable)
- No package conflicts
- Web application starts and responds to requests
- No critical warnings

