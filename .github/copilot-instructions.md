# Copilot Instructions for NuGetGallery

This repository powers nuget.org. It combines the ASP.NET Gallery, shared server libraries, background jobs and V3 services, functional-test infrastructure, and a small Python log parser. Work from the repository root unless a command says otherwise.

## Build and validation

### Prerequisites

- Use Windows with Visual Studio 2022 and the **ASP.NET and web development** and **Azure development** workloads. The build locates Visual Studio MSBuild and several projects target .NET Framework 4.7.2.
- Gallery browser functional tests also require Visual Studio's **Web performance and load testing tools** component.
- `global.json` starts SDK selection at 8.0.318 with `rollForward: latestMajor`. Install a .NET 10-capable SDK for the Aspire host and functional tests, which target .NET 10.
- Run `.\tools\Setup-DevEnvironment.ps1` as administrator for first-time Gallery setup. It configures IIS Express/HTTPS and applies the EF6 database migrations.

### C# build

Use the repository PowerShell build, not `dotnet build`, for the main solutions:

```powershell
# Restore and build Common, Gallery, Jobs, Jobs functional tests, and artifacts
.\build.ps1

# Build one subsystem without packaging/signing
.\build.ps1 -SkipCommon -SkipJobs -SkipArtifacts
.\build.ps1 -SkipGallery -SkipJobs -SkipArtifacts
.\build.ps1 -SkipCommon -SkipGallery -SkipArtifacts

# Repeat a Gallery build after a successful restore
.\build.ps1 -SkipCommon -SkipJobs -SkipArtifacts -SkipRestore
```

The build also validates project/solution membership, generates assembly version files, and runs the configured Roslyn/NuGet analyzers. There is no separate repository-wide C# lint command. Gallery builds treat warnings as errors, and Release builds compile Razor views.

### C# tests

Build before testing because the test commands use `--no-build --no-restore`.

```powershell
# All Common, Gallery, and Jobs unit tests
.\test.ps1

# One subsystem
.\test.ps1 -SkipCommon -SkipJobs

# One project
dotnet test tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj --no-restore --no-build --configuration debug

# One test or test class
dotnet test tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj --no-restore --no-build --configuration debug --filter "FullyQualifiedName~TheMethodName"
```

Tests use xUnit and Moq. Most unit-test projects target .NET Framework 4.7.2 even though `dotnet test` is the runner.

### Aspire and functional tests

`src\NuGetGallery.AppHost` orchestrates the Gallery and local V3 pipeline. For the CI-style minimal profile:

Always stop the host in a `finally` block when scripting this flow. `Start-AspireHost.ps1` defaults `APPHOST_PROFILE` to `ci-gallery`; the AppHost itself defaults to `full`. The full profile adds the Azure Search-backed resources.

`BuildGalleryFunctionalTests.ps1` restores/builds the functional-test solution and installs its Playwright browsers. `-UnsafeAdminApiAuthBypassForTesting` compiles an authentication bypass; use it only in the AppHost build for Admin API functional tests, never in artifact, signing, or deployment builds.

For agent-owned browser validation, use the in-test Aspire harness:

```powershell
.\tests\Scripts\RunGalleryPlaywrightTests.ps1 -Configuration Release
```

This starts the `ci-gallery` AppHost, seeds test data, runs the supported Playwright tests, and tears down once per suite. It is Windows-only and uses fixed Gallery ports 80/443, so do not run it concurrently with `Start-AspireHost.ps1`. Pass `-AppHostProfile full` only when local Azure Search prerequisites are available. Statistics-service and read-only-mode browser tests remain on their separately configured paths.

### Frontend assets

Change Gallery styles in `src\Bootstrap\less`, not generated CSS. Then run:

```powershell
.\tools\Build-Bootstrap.ps1
```

Commit the LESS source and copied/minified outputs under `src\NuGetGallery`. CI rebuilds Bootstrap and fails if the generated output differs. The script installs `src\Bootstrap` npm dependencies when needed and invokes Grunt.

### StatsLogParser

The independent Python package under `python\StatsLogParser` requires Python 3.10+, pipx, and Poetry:

```powershell
Set-Location python\StatsLogParser
poetry install
poetry run pytest tests\
poetry run pytest tests\test_file.py -k test_name
poetry build
```

When dependencies change, update both `poetry.lock` and the exported `requirements.txt` used by Spark.

## Architecture

### Solutions and ownership

- `NuGetGallery.sln` contains the classic ASP.NET Gallery and its unit tests.
- `NuGet.Server.Common.sln` contains reusable configuration, logging, storage, messaging, and other server libraries used across NuGet services.
- `NuGet.Jobs.sln` contains background jobs and V3 pipeline components.
- `NuGetGallery.FunctionalTests.sln` and `NuGet.Jobs.FunctionalTests.sln` isolate functional-test projects.
- `NuGetGallery.Aspire.slnx` contains the local distributed application and its dependencies.

Source projects intentionally overlap between solutions; test projects must not. `build.shared.ps1` requires every `.csproj` to belong to a solution and uses `CommonPackageVersion`, `GalleryPackageVersion`, or `JobsPackageVersion` to classify shared source projects. When adding or moving a project, update the appropriate solution(s) and package-version property.

### Gallery request and service layers

`src\NuGetGallery` is a .NET Framework 4.7.2 ASP.NET MVC application with Web API, OWIN, Razor, and IIS Express. Controllers and views depend on:

- `src\NuGetGallery.Services` for Gallery business logic such as authentication, package management, permissions, storage, and telemetry.
- `src\NuGetGallery.Core` for EF entities, repositories, auditing, and core services.
- `src\NuGet.Services.Entities` for models shared with jobs and services.

Autofac is the composition root. `App_Start\AutofacConfig.cs` discovers MVC/Web API controllers and assembly modules; `App_Start\DefaultDependenciesModule.cs` contains most runtime registrations; authentication has its own module. Follow the existing lifetime and keyed-registration patterns rather than constructing services in controllers.

The main Gallery database uses EF6 `EntitiesContext`. Gallery migrations are in `src\NuGetGallery\Migrations`; support-request migrations are under `Areas\Admin\Migrations`; validation and catalog-validation use separate contexts. Create a new migration for schema changes and implement both `Up()` and `Down()`; never edit a migration that may already be deployed.

### Search and V3 pipeline

Gallery search is selected in `DefaultDependenciesModule` based on search endpoint configuration. The external path is:

`ExternalSearchService` -> `GallerySearchClient` -> `ResilientSearchHttpClient` -> one or more `HttpClientWrapper` instances.

The Jobs solution contains the V3 production pipeline. `Ng` subcommands and standalone `JsonConfigurationJob` executables move Gallery DB/package data through catalog, flat-container, registration, search, and auxiliary-storage outputs. Standalone jobs generally accept `-Configuration <json>`; check the job's `Program.cs` and adjacent README before changing invocation or configuration.

### Local distributed application

`src\NuGetGallery.AppHost\Program.cs` models local infrastructure and ordering: Azurite, EF6 migrations, IIS Express Gallery, seed tools, catalog/V3 jobs, and optional Azure Search. Resources use `WaitFor`/`WaitForCompletion`; preserve these dependencies when changing the graph. The AppHost generates local configuration files for Gallery and tools, so do not hard-code those generated values in checked-in application configuration.

### Frontend composition

- The default layout is `src\NuGetGallery\Views\Shared\Gallery\Layout.cshtml`, selected by `_ViewStart.cshtml` with a branding override. It exposes optional `TopStyles`, `TopScripts`, `BottomScripts`, `Meta`, and `SocialMeta` sections.
- Strongly typed view models live under `ViewModels`.
- Page styles use `src\Bootstrap\less\theme\page-*.less` and must be imported by `src\Bootstrap\less\theme\all.less`; reusable styles use `common-*.less`.
- Page scripts use `Scripts\gallery\page-*.js`. Register their `ScriptBundle` in `App_Start\AppActivator.cs` and render the bundle from the view's `BottomScripts` section.
- The frontend uses the customized Bootstrap 3.4.1 fork, jQuery, and Knockout. Existing Gallery JavaScript is ES5 and begins with `'use strict'`.
- Prefer existing CSS custom properties for light/dark theme colors and Microsoft Fabric icons (`ms-Icon--*`). Preserve WCAG behavior and accessible labels.

## Repository-specific conventions

### C# and project files

- Use the two-line .NET Foundation/Apache 2.0 header from `.editorconfig`.
- Use Allman braces, four spaces, explicit visibility, `_camelCase` private fields, `readonly` where possible, and no unnecessary `this.`.
- Keep `using` directives outside the namespace, with `System` directives first and all groups alphabetized without blank separators.
- Use block-scoped namespaces. Async methods end in `Async`; do not block with `.Result` or `.Wait()`.
- The repository multi-targets .NET Framework 4.7.2, .NET Standard 2.0/2.1, and newer .NET for selected services/tools. Several multi-target projects conditionally remove files that depend on `System.Web` or other framework-only APIs. Check conditional `Compile Remove`, references, and project references before moving code across target frameworks.
- SDK-style `<PackageReference>` versions are centrally managed in `Directory.Packages.props`; omit versions from ordinary `<PackageReference>` items. The main build also restores legacy `packages.config` dependencies, so follow the dependency style of the project being changed rather than converting it incidentally.

### Gallery behavior

- Use `ITelemetryService` for Gallery application telemetry and follow nearby event/metric patterns. The shared service libraries also expose `Microsoft.Extensions.Logging`; do not replace Gallery telemetry with ad hoc logging.
- Sanitize user-controlled URLs for display with `PackageHelper.TryPrepareUrlForRendering()` and prefer HTTPS where the existing flow supports it.
- MVC POST actions require `[ValidateAntiForgeryToken]`. Intentional API exceptions must be added to `ControllerTests.AllActionsHaveAntiForgeryTokenIfNotGet` so the architectural test remains explicit.
- Register new services through the appropriate Autofac module and add configuration through the established configuration models/sections.

### Tests

- Unit-test projects use `.Facts` or `.Tests` according to their subsystem's existing convention.
- Gallery facts commonly group tests in nested classes named for the member under test and use Arrange/Act/Assert.
- Tests requiring the Gallery dependency graph derive from `tests\NuGetGallery.Facts\Framework\TestContainer.cs`; use `GetController<T>()`, `GetMock<T>()`, `GetService<T>()`, and `GetFakeContext()` rather than rebuilding the container.
- Keep test projects in only one main solution; `build.shared.ps1` rejects shared non-source projects.

### Contributions

Changes target the `dev` branch and should be linked to an issue. Repository documentation contains multiple historical branch-name examples, so follow the current maintainer/issue convention rather than assuming one global branch-name template.
