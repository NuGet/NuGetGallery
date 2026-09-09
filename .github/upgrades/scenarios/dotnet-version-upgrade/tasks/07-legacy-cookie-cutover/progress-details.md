# 07-legacy-cookie-cutover progress

## STATUS

COMPLETED

## Implementation

- Changed only the primary `LocalUser` Katana cookie contract:
  - explicitly names `.AspNet.LocalUser` and path `/`;
  - retains active `LocalUser` authentication, HTTP-only and `RequireSSL`-driven secure behavior, `/users/account/LogOn`, six-hour expiry, sliding renewal, and the existing default sign-in type;
  - uses `AspNetTicketDataFormat`, `DataProtectorShim`, the proven ASP.NET Core cookie purpose chain, and the Interop `chunks-N` cookie manager;
  - rejects a mismatched authentication type and a missing shared provider instead of silently falling back to the previous machine-key ticket format.
- Kept Katana as the sole issuer and refresher. The ASP.NET Core host remains reader-only with sliding renewal disabled and no `SignInAsync` path. External temporary cookies, admin bearer authentication, antiforgery, machine-key consumers, controllers/views, EF6/SQL, and all other OWIN middleware were unchanged.
- Wired the legacy provider through the existing production abstractions:
  - `ConfigurationService.Initialize()` now retains the same `KeyVaultConfiguration` used by its existing `web.config` secret-reader factory;
  - the already injected/normalized `SharedDataProtectionConfiguration` is validated at startup;
  - the existing singleton `FileStorageXmlRepository` resolves its grouped/keyed `ICoreFileStorageService`;
  - `KeyVaultKeyEncryptionKeyResolver` is registered once as the official `IKeyEncryptionKeyResolver` and metadata-validator abstraction when encryption at rest is enabled;
  - the shared `IDataProtectionProvider` is a singleton configured with the task-04 repository and official Key Vault extension.
- Added production-contract tests for explicit cookie settings, secure behavior, six-hour/sliding semantics, shared-format round trips, and fail-closed behavior.
- Added proxy regression tests proving an old-enough valid shared ticket receives only the legacy response's single renewal and an invalid ticket still reaches the anonymous legacy fallback.

## Validation

- `dotnet build .\src\NuGetGallery.Services\NuGetGallery.Services.csproj --framework net472 --configuration Debug --verbosity minimal`
  - Succeeded.
- Visual Studio MSBuild 18:
  - `tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj /restore /t:Build /p:Configuration=Debug` succeeded.
  - `NuGetGallery.sln /restore /t:Build /p:Configuration=Debug` succeeded, confirming the legacy application remains independently buildable.
- `dotnet test .\tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj --no-restore --no-build --configuration Debug --filter "FullyQualifiedName~LocalUserAuthenticatorFacts|FullyQualifiedName~AuthenticatorFacts" --verbosity minimal`
  - Passed: 26; failed: 0; skipped: 0.
- `dotnet test .\tests\NuGetGallery.net10.Facts\NuGetGallery.net10.Facts.csproj --configuration Debug --no-restore --verbosity minimal`
  - Passed: 35; failed: 0; skipped: 0.
- `.\tests\NuGetGallery.CookieInteropTests\run-tests.ps1`
  - Passed all `net472`/`net10.0` legacy-issue, Core exchange, and legacy-accept compatibility stages.
- Source verification found no ASP.NET Core `SignInAsync` call or enabled sliding expiration.
- `git diff --check` passed.

## Warnings

- Builds retain the repository's existing `NU1507` multi-source Central Package Management warning and the SDK 10.0.400/newer-analyzers warning against the centrally pinned 8.0.0 analyzer package.
- Legacy builds retain unrelated existing warnings, including `CS0618` for `TelemetryClient.InstrumentationKey` and nullable warnings in `FederatedCredentialServiceFacts`.
- No Azure resources were contacted and no secrets, key XML, cookies, or connection strings were logged.
- The accepted slot-swap format cutover means cookies issued with the former machine-key format require a one-time sign-in after deployment or rollback.

## Modified files

- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/07-legacy-cookie-cutover/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/07-legacy-cookie-cutover/progress-details.md`
- `src/NuGetGallery.Services/Authentication/Providers/LocalUser/LocalUserAuthenticator.cs`
- `src/NuGetGallery.Services/Configuration/ConfigurationService.cs`
- `src/NuGetGallery.Services/Configuration/SecretReader/SecretReaderFactory.cs`
- `src/NuGetGallery.Services/NuGetGallery.Services.csproj`
- `src/NuGetGallery/App_Start/DefaultDependenciesModule.cs`
- `src/NuGetGallery/Authentication/AuthDependenciesModule.cs`
- `tests/NuGetGallery.Facts/Authentication/Providers/LocalUser/LocalUserAuthenticatorFacts.cs`
- `tests/NuGetGallery.net10.Facts/LegacyProxyFacts.cs`

Pre-existing workflow state/assessment artifacts and later-task artifacts were preserved. No commit was created.
