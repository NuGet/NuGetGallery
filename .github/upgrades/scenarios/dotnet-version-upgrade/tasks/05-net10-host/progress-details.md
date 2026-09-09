# 05-net10-host progress

## STATUS

COMPLETED

## Implementation

- Added `src\NuGetGallery.net10\NuGetGallery.net10.csproj`, an SDK-style ASP.NET Core host targeting `net10.0` with an explicit `Program.Main`.
- Added modern configuration bootstrapping that:
  - Uses the repository's `ConfigurationRootSecretReaderFactory`, cached secret reader, and injected JSON/environment providers.
  - Supports the existing Key Vault certificate, system-assigned identity, user-assigned identity, local-development credential, and `SendX5c` settings.
  - Binds and validates host paths, forwarded-header trust, and the task-04 shared Data Protection settings at startup.
  - Defaults `Local_Development` to `false` in base configuration so Production Azure Storage with system-assigned identity selects `ManagedIdentityCredential`; only Development configuration opts into `DefaultAzureCredential`.
- Registered the task-04 shared Data Protection repository and builder integration for filesystem or Azure Blob storage.
  - Development defaults to a local filesystem ring and requires no Azure or Aspire process.
  - Production continues to fail closed unless keys are encrypted at rest with the validated Key Vault resolver.
- Registered the shared `LocalUser` / `.AspNet.LocalUser` cookie reader, authorization, health checks, and YARP HTTP-forwarder services.
  - ASP.NET Core sliding renewal is disabled so this host does not become a cookie issuer/refresher before task 07.
  - No YARP route or legacy destination was added; the catch-all remains task 06.
- Added fail-closed forwarded-header handling using .NET 10's `KnownIPNetworks`, loopback trust defaults, and an opt-in forwarded-host allow-list.
- Added Kestrel server-header suppression, configurable TLS/request-size policy, and response fingerprint-header removal.
- Reserved local `/_local`, liveness `/_health`, and readiness `/_ready` endpoints without overlapping the legacy Gallery health routes.
- Added the host to `2. Frontend` and its focused tests to `3. Tests` in `NuGetGallery.sln`.
- Kept `global.json` unchanged because its `8.0.318` floor plus `latestMajor` selects the installed `10.0.400` SDK.
- Did not modify `src\NuGetGallery`, `NuGetGallery.Aspire.slnx`, or `src\NuGetGallery.AppHost`; no EF Core/database work was added.

## Validation

- `dotnet --info`
  - Selected SDK `10.0.400`; .NET/ASP.NET Core 10 runtimes are installed.
- `dotnet build .\src\NuGetGallery.net10\NuGetGallery.net10.csproj --configuration Debug --verbosity minimal`
  - Passed; output produced under `bin\Debug\net10.0`.
- `dotnet build .\src\NuGetGallery.net10\NuGetGallery.net10.csproj --configuration Release --verbosity minimal`
  - Passed; output produced under `bin\Release\net10.0`.
- `dotnet test .\tests\NuGetGallery.net10.Facts\NuGetGallery.net10.Facts.csproj --configuration Debug --verbosity minimal`
  - Passed: 4; failed: 0; skipped: 0.
  - TestServer verified all reserved endpoints, a shared Data Protection round trip, the shared cookie scheme/name with renewal disabled, and YARP forwarder service registration without Azure or Aspire.
  - Configuration tests verified Production's system-assigned Azure Storage path selects managed identity and Development explicitly selects local `DefaultAzureCredential`.
- `dotnet test .\tests\NuGetGallery.net10.Facts\NuGetGallery.net10.Facts.csproj --configuration Release --verbosity minimal`
  - Passed: 4; failed: 0; skipped: 0.
  - This specifically guards the reviewed Release failure path while retaining the same credential-selection assertions.
- Independent Kestrel smoke test:
  - Started the Release host with its Development launch profile on `http://127.0.0.1:5197`.
  - `/_local` returned `200 NuGetGallery.net10`.
  - `/_health` and `/_ready` each returned `200 Healthy`.
- Visual Studio MSBuild 18.12:
  - `NuGetGallery.sln /restore /t:Build /p:Configuration=Debug` passed after the host and test project were added.
- Parsed `appsettings.json` and `Properties\launchSettings.json` successfully.
- `dotnet sln .\NuGetGallery.sln list` includes both new projects in their requested solution areas.
- `git -c core.whitespace=cr-at-eol diff --check` passed.
- `dotnet list .\src\NuGetGallery.net10\NuGetGallery.net10.csproj package --vulnerable --include-transitive` found no vulnerability introduced by YARP.

## Warnings

- Builds retain the repository's existing `NU1507` multi-source Central Package Management warning and analyzer-version warning (`Microsoft.CodeAnalysis.NetAnalyzers` 8.0.0 versus SDK 10.0.400).
- The vulnerability scan reports the repository's pre-existing transitive `Microsoft.Build.Tasks.Git` 8.0.0 moderate advisory (`GHSA-23fw-v26w-5fgq`); YARP 2.3.0 was not reported.
- Production startup intentionally requires encrypted-at-rest Data Protection configuration and valid Key Vault access. The checked-in filesystem/plaintext settings are local-development defaults selected by the launch profile.
- `DefaultAzureCredential` remains intentionally restricted to explicit local Development configuration; base/Production configuration no longer enables it.
- No catch-all proxy route, legacy cookie cutover, EF Core behavior, database behavior, Aspire solution entry, or AppHost resource was added.

## Modified files

- `.github/upgrades/scenarios/dotnet-version-upgrade/scenario-instructions.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/05-net10-host/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/05-net10-host/progress-details.md`
- `Directory.Packages.props`
- `NuGetGallery.sln`
- `src/NuGetGallery.net10/NuGetGallery.net10.csproj`
- `src/NuGetGallery.net10/GalleryForwardedHeadersOptions.cs`
- `src/NuGetGallery.net10/GalleryHostOptions.cs`
- `src/NuGetGallery.net10/Program.cs`
- `src/NuGetGallery.net10/Properties/launchSettings.json`
- `src/NuGetGallery.net10/SharedDataProtectionServiceCollectionExtensions.cs`
- `src/NuGetGallery.net10/appsettings.Development.json`
- `src/NuGetGallery.net10/appsettings.json`
- `tests/NuGetGallery.net10.Facts/NuGetGallery.net10.Facts.csproj`
- `tests/NuGetGallery.net10.Facts/ProgramFacts.cs`

Pre-existing workflow assessment/state artifacts were preserved. No commit was created.
