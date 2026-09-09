# 05-net10-host: Scaffold the side-by-side ASP.NET Core host

Create SDK-style `src\NuGetGallery.net10\NuGetGallery.net10.csproj` targeting `net10.0`, with an explicit `Program` class and `Main` method. Register modern configuration and validation, secret injection, the shared Data Protection and cookie components, authorization, forwarded headers, YARP services, and reserved health/readiness endpoints where consistent with repository conventions. Reference only compatible existing Gallery targets needed by this phase, using central package-management conventions.

Add the project to the Frontend area of `NuGetGallery.sln` without changing `NuGetGallery.Aspire.slnx` or the Aspire AppHost. Do not add EF Core, migrations, schema changes, or new database behavior. Verify the .NET 10 SDK/targeting pack on developer and CI build paths; retain the existing `global.json` roll-forward behavior unless validation proves the `8.0.318` floor blocks a supported build.

**Done when**: The new host restores, builds, starts independently from configuration, exposes a stub local and health/readiness endpoint, loads the shared protection/authentication services without Azure or Aspire in self-contained tests, and the legacy web project remains unchanged and deployable.

## Research

Research completed before source changes on 2026-09-08:

- The installed SDK is `10.0.400` with the .NET and ASP.NET Core `10.0.11` runtimes. The repository `global.json` floor remains `8.0.318` with `rollForward: latestMajor`, which selects the installed .NET 10 SDK successfully and does not need changing.
- `NuGetGallery.sln` has a `2. Frontend` solution folder containing the legacy `src\NuGetGallery\NuGetGallery.csproj`. The new SDK-style host will be added there; the existing legacy project, `NuGetGallery.Aspire.slnx`, and `src\NuGetGallery.AppHost` will remain unchanged.
- Repository-wide central package management is enabled in `Directory.Packages.props`. `Yarp.ReverseProxy` is not yet pinned; NuGet.org exposes `2.3.0` as the current stable version. The host only needs this explicit package plus framework-provided ASP.NET Core APIs. The existing `Microsoft.AspNetCore.TestHost` `10.0.0`, xUnit, and test SDK pins cover a self-contained host test project.
- `src\NuGetGallery.Core\NuGetGallery.Core.csproj` targets `netstandard2.1` as its compatible modern target. It contains `SharedCookieConstants`, `SharedDataProtectionConfiguration`, `FileStorageXmlRepository`, `FileSystemFileStorageService`, `CloudBlobCoreFileStorageService`, and `ConfigureSharedDataProtection`; the host can reference this project without referencing the legacy System.Web application or adding EF Core.
- Task 04 established the shared Data Protection contract: `.AspNet.LocalUser`, scheme `LocalUser`, application discriminator `NuGetGallery`, six-hour lifetime, and a storage-backed `IXmlRepository`. The new host will register that existing builder extension and cookie scheme with ASP.NET Core sliding renewal disabled, preserving the legacy host as the sole issuer/refresher until task 07.
- `NuGet.Services.Configuration` targets `netstandard2.0` and already provides `ConfigurationRootSecretReaderFactory` plus injected JSON/environment configuration sources. Those reuse `NuGet.Services.KeyVault` credential selection and `$$secret$$` injection while falling back to `EmptySecretReader` when no vault is configured, so local tests/startup require neither Azure nor Aspire.
- Forwarded headers will use the .NET 10 non-obsolete `KnownIPNetworks`/`System.Net.IPNetwork` API, enumerate only `X-Forwarded-For`, `X-Forwarded-Host`, and `X-Forwarded-Proto`, trust loopback by default, and ignore forwarded hosts unless allow-listed. `UseForwardedHeaders` will be first in the middleware pipeline. Kestrel will suppress its server header and retain a TLS 1.2/1.3 floor.
- Task 06 owns the terminal catch-all route. This task will register YARP's forwarder services only and reserve local `/_local`, `/_health`, and `/_ready` endpoints, which do not overlap the legacy `api/status` and `api/health-probe` routes.
- New self-contained tests will use ASP.NET Core TestServer with filesystem Data Protection under test output, assert endpoint responses and cookie registration, and perform a Data Protection round trip without Azure or Aspire. Validation will use targeted `dotnet test`/`dotnet build`, an independent Kestrel startup probe, and Visual Studio MSBuild for the legacy web project/solution surface.

### Review follow-up: environment-safe Azure credentials

- Review found that `Local_Development` was set to `true` in base `appsettings.json`. In a Production deployment configured for Azure Storage with system-assigned managed identity, that selected `UsingDefaultAzureCredential`, whose repository implementation is intentionally unavailable in Release builds.
- The safe repository-aligned default is `Local_Development: false` in base configuration, which selects `ManagedIdentityCredential`. `appsettings.Development.json` explicitly opts local development into `DefaultAzureCredential`; a configured user-assigned identity continues to select managed identity in every environment.
- Focused configuration tests will load the same base/environment JSON files copied with the host and verify the resulting system-assigned Azure Storage credential mode for Production and Development.
