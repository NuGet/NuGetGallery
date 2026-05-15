# Copilot Instructions for NuGetGallery

This repository powers [nuget.org](https://www.nuget.org). It contains the gallery web application, background jobs, shared libraries, and validation services.

## Target Frameworks

This repository targets multiple C# frameworks: .NET Framework 4.7.2, .NET Standard 2.1, and .NET Standard 2.0.

The repo uses `<LangVersion>latest</LangVersion>`. Use the latest C# syntax for newly generated code.

## Build

The repo uses MSBuild via Visual Studio (not `dotnet build`). Build with PowerShell:

```powershell
# Full build (restore + common + gallery + jobs + artifacts)
.\build.ps1

# Gallery-only build (most common during development)
.\build.ps1 -SkipCommon -SkipJobs -SkipArtifacts

# Incremental (skip restore after first build)
.\build.ps1 -SkipCommon -SkipJobs -SkipArtifacts -SkipRestore
```

## Test

Tests use **xUnit** and **Moq**. Run all tests:

```powershell
.\test.ps1
```

Run a single test project:

```powershell
dotnet test tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj --no-restore --no-build --configuration debug
```

Run a single test by name:

```powershell
dotnet test tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj --no-restore --no-build --configuration debug --filter "FullyQualifiedName~TheMethodName"
```

## Architecture

### Solutions

- **NuGetGallery.sln** — Main gallery web app and its tests
- **NuGet.Server.Common.sln** — Shared libraries (configuration, logging, storage, etc.)
- **NuGet.Jobs.sln** — Background jobs (catalog indexing, stats, validation, Azure Search)
- **NuGetGallery.Aspire.slnx** — Aspire orchestrator for local development

### Key projects

- `src/NuGetGallery` — ASP.NET MVC web app (.NET Framework 4.7.2). Uses OWIN, Web API, and Razor views.
- `src/NuGetGallery.Core` — Shared core library (multi-targets net472 and netstandard2.1). Contains EF entities and core services.
- `src/NuGetGallery.Services` — Business logic layer: authentication, package management, permissions, telemetry.
- `src/NuGet.Services.Entities` — Entity models shared across gallery and jobs.
- `src/NuGetGallery.AppHost` — Aspire AppHost for local orchestration (Azurite, IIS Express, DB migrations, seeding).

### Dependency injection

Autofac is the DI container. Registration is module-based:

- `src/NuGetGallery/App_Start/AutofacConfig.cs` — Container setup
- `src/NuGetGallery/App_Start/DefaultDependenciesModule.cs` — Main service registrations
- `src/NuGetGallery/Authentication/AuthDependenciesModule.cs` — Auth registrations

### Database

Entity Framework 6 with code-first migrations. Key files:

- `src/NuGetGallery.Core/Entities/EntitiesContext.cs` — Main DbContext
- `src/NuGetGallery/Migrations/` — Gallery database migrations

Additional contexts exist for validation (`ValidationEntitiesContext`) and support requests (`SupportRequestDbContext`).

### Search

The search pipeline flows: `ExternalSearchService` → `GallerySearchClient` → `ResilientSearchHttpClient` → `HttpClientWrapper` (with Polly retry policies). Configured conditionally based on `SearchServiceUriPrimary`/`SearchServiceUriSecondary` settings.

## Conventions

### File header

Source files should include this copyright header:

```
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
```

### Nullable reference types

Only opt code into nullable reference types when explicitly asked. When doing so:

- Add `#nullable enable` at the top of the file.
- Never use `!` to suppress null warnings.
- Declare variables non-nullable and check for `null` at entry points.
- Use `throw new ArgumentNullException(nameof(parameter))` in .NET Framework and .NET Standard projects.

### C# style

- Allman-style braces
- Always specify visibility (`private string _foo`, not `string _foo`)
- Private fields: `_camelCase` with `readonly` where possible
- Avoid `this.` unless necessary
- Avoid abbreviations: `Service` not `Svc`
- Namespaces match folder structure
- 4 spaces indentation
- System usings first, then alphabetical

### Asynchronous programming

- Use async/await consistently. Name async methods with an `Async` suffix.
- Never use `.Result` or `.Wait()` on Tasks.
- Pass `CancellationToken` parameters when appropriate.
- Use `ConfigureAwait(false)` in library code (shared projects targeting netstandard).

### Telemetry

Use `ITelemetryService` for application telemetry, not raw `ILogger`.

### Database migrations

- Use Entity Framework 6 migration patterns with proper `Up()` and `Down()` implementations.
- Never modify existing migrations after they've been deployed. Create new migrations for schema changes.

### URL handling

- Convert HTTP URLs to HTTPS where appropriate.
- Use `PackageHelper.TryPrepareUrlForRendering()` for sanitizing URLs for display.

### Anti-forgery tokens

All POST controller actions must have `[ValidateAntiForgeryToken]`. API-style POST actions that intentionally skip it must be added as exceptions in `tests/NuGetGallery.Facts/Controllers/ControllerTests.cs` (`AllActionsHaveAntiForgeryTokenIfNotGet`).

### Test organization

Test projects are named with `.Facts` suffix (e.g., `NuGetGallery.Facts`, `NuGetGallery.Core.Facts`).

Tests use nested classes to group by member under test:

```csharp
public class PackageServiceFacts
{
    public class TheCreatePackageMethod : TestContainer
    {
        [Fact]
        public void ReturnsCreatedPackage()
        {
            // Arrange
            // ...

            // Act
            // ...

            // Assert
            // ...
        }
    }
}
```

`TestContainer` (in `tests/NuGetGallery.Facts/Framework/TestContainer.cs`) is the base class for tests that need DI. It provides `GetController<T>()`, `GetMock<T>()`, `GetService<T>()`, and `GetFakeContext()`.

### csproj compile patterns

`NuGetGallery.Core.csproj` excludes certain files from `netstandard2.1` that depend on `System.Web`. If porting code that uses `System.Web.HttpServerUtility`, check the compile exclusions.

### Branch workflow

All feature branches should be created from and merged back to `dev`. Branch names follow the format `users/[username]/[feature-name]` for new features and `users/[username]/[issue-number]` for bug fixes.

### JavaScript and frontend

- JavaScript uses ES5 syntax for compatibility.
- Use `'use strict'` directive.
- Follow accessibility best practices (WCAG) in UI components.
- The gallery uses a customized Bootstrap 3.4.1 fork. LESS sources are in `src/Bootstrap/less/`. After modifying them, rebuild with `.\tools\Build-Bootstrap.ps1` and commit both the LESS changes and the updated output files.
- Several pages use **Knockout.js** for client-side data binding.

### Views and layout

The master layout is `Views/Shared/Gallery/Layout.cshtml` (not the standard `_Layout.cshtml` location). It's selected via `_ViewStart.cshtml` with a branding override fallback. The layout defines optional Razor sections that pages can render into: `TopStyles`, `TopScripts`, `BottomScripts`, `Meta`, and `SocialMeta`.

Views use strongly-typed ViewModels (e.g., `DisplayPackageViewModel`). ViewModels live in `src/NuGetGallery/ViewModels/`.

Icons use the **Microsoft Fabric** icon set: `<i class="ms-Icon ms-Icon--{Name}" aria-hidden="true"></i>`.

### CSS and JS bundles

Page styles are written in **LESS** in `src/Bootstrap/less/theme/`. Each page has a corresponding file (e.g., `page-display-package.less`, `page-home.less`). These are imported via `all.less`, compiled by Grunt, and output as `bootstrap.min.css`. This is the primary stylesheet loaded by the layout.

When adding styles for a new page:
1. Create `src/Bootstrap/less/theme/page-{name}.less`
2. Add `@import "page-{name}.less";` to `src/Bootstrap/less/theme/all.less`
3. Run `.\tools\Build-Bootstrap.ps1` to compile
4. Commit both the LESS source and the compiled output in `Content/gallery/css/bootstrap.min.css`

For shared/reusable component styles, use the `common-*.less` naming pattern.

CSS variables (e.g., `var(--neutralForeground1Rest)`) are used for dark/light theme support. Use them instead of hardcoded colors where available.

JS bundles are registered in code in `App_Start/AppActivator.cs` (not a separate `BundleConfig.cs`). Each page has its own JS file following the naming convention `Scripts/gallery/page-{name}.js`, with a corresponding `ScriptBundle` registered in `AppActivator.cs`.

When adding a new page with JavaScript:
1. Create `Scripts/gallery/page-{name}.js`
2. Register a new `ScriptBundle` in `AppActivator.cs`
3. Reference the bundle in the view's `BottomScripts` section
