# 04-shared-data-protection progress

## STATUS

COMPLETED

## Implementation

- Added complete `ListFilesAsync` enumeration to `ICoreFileStorageService`.
  - Blob storage exhausts every continuation page and returns sorted container-relative names.
  - Filesystem storage recursively enumerates through `IFileSystemService`, rejects paths outside the requested folder, and returns normalized `/`-separated relative names.
  - Corrected empty Blob result segments to expose a null continuation token.
- Added the private `data-protection` folder/container with `application/xml`, no cache header, and the existing container initialization policy.
- Added `FileStorageXmlRepository` in `NuGetGallery.Core`.
  - Reads all XML files with DTDs prohibited and fails on missing or malformed listed files.
  - Writes UTF-8 XML with create-only semantics and propagates storage race failures.
  - Retries transient filesystem sharing violations while another process completes an immutable write.
  - Isolates the synchronous `IXmlRepository` boundary on worker tasks, including key loading and generation under a request synchronization context.
  - Never deletes or rewrites existing entries and emits only count/success diagnostics without names, XML, cookies, connection strings, or secrets.
- Added bindable `SharedDataProtectionConfiguration`, strict production validation, and shared `IDataProtectionBuilder` registration.
  - Validates storage type/location, the fixed `NuGetGallery` discriminator, minimum key lifetime, encryption-at-rest, a versionless HTTPS `/keys/{name}` URI, rotation shorter than key lifetime, and Key Vault version retention at least as long as ring retention.
  - Uses the official `Azure.Extensions.AspNetCore.DataProtection.Keys` extension with `IKeyEncryptionKeyResolver`.
  - Provides a production overload that constructs task 02's `KeyVaultKeyEncryptionKeyResolver` from `KeyVaultConfiguration`, preserving its local/debug, system-assigned identity, user-assigned identity, certificate, and `SendX5c` behavior.
- Closed the startup metadata-validation gap identified during review.
  - Added `IKeyEncryptionKeyMetadataValidator` to the Key Vault abstraction and implemented it on `KeyVaultKeyEncryptionKeyResolver`.
  - Production registration now requires that capability and performs a native synchronous `KeyClient.GetKey` call during registration; it does not block on an asynchronous operation.
  - Validation confirms the configured vault and key name, requires a versionless configured URI and a versioned response, and rejects disabled, not-yet-valid, expired, non-RSA/non-RSA-HSM, or non-wrap/unwrap-capable keys.
  - The resolver also applies the same metadata checks when later resolving a newly rotated current version, while continuing to resolve historical versioned identifiers directly.
  - Azure authentication and service failures are preserved, so startup fails closed rather than deferring the error to first key generation.
- Added equivalent legacy configuration properties and normalization into the shared modern configuration object. The Data Protection connection string participates in existing `$$secret$$` injection and keyed `StorageDependent` grouping.
- Registered `FileStorageXmlRepository` as a singleton storage dependent without changing production `LocalUser` cookie registration.
- Updated the cross-framework cookie harness so the .NET 10 leg reads the legacy-created ring through `FileStorageXmlRepository`, proving the shared repository/configuration path remains compatible in both directions.
- Added required .NET Framework binding redirects and pinned the secure `System.Security.Cryptography.Xml`/`System.Formats.Asn1` 10.0.11 graph. `Azure.Extensions.AspNetCore.DataProtection.Keys` 1.1.0 retains the repository's established Data Protection 2.3.x interoperability line and existing `Azure.Core` 1.50.0/`Azure.Security.KeyVault.Keys` 4.4.0 pins; no unrelated framework or Azure SDK migration was performed.

## Validation

- `dotnet test .\tests\NuGetGallery.Core.Facts\NuGetGallery.Core.Facts.csproj --configuration Debug --no-restore --verbosity minimal`
  - Passed: 1,193; failed: 0; skipped: 19 Azure integration tests that require external storage.
- Focused final Data Protection/Blob enumeration run:
  - `dotnet test .\tests\NuGetGallery.Core.Facts\NuGetGallery.Core.Facts.csproj --configuration Debug --filter "FullyQualifiedName~DataProtection|FullyQualifiedName~TheListFilesAsyncMethod" --verbosity minimal`
  - Passed: 19; failed: 0; skipped: 0.
- `dotnet test .\tests\NuGet.Services.KeyVault.Tests\NuGet.Services.KeyVault.Tests.csproj --configuration Debug --no-restore --verbosity minimal`
  - Passed: 100; failed: 0; skipped: 0.
- Focused resolver metadata-validation run:
  - `dotnet test .\tests\NuGet.Services.KeyVault.Tests\NuGet.Services.KeyVault.Tests.csproj --configuration Debug --filter "FullyQualifiedName~KeyVaultKeyEncryptionKeyResolverFacts" --verbosity minimal`
  - Passed: 25; failed: 0; skipped: 0.
- Legacy focused tests:
  - `dotnet test .\tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj --no-build --no-restore --configuration Debug --filter "FullyQualifiedName~TheListFilesAsyncMethod|FullyQualifiedName~StorageDependentFacts|FullyQualifiedName~SharedDataProtectionConfigurationExtensionsFacts" --verbosity minimal`
  - Passed: 9; failed: 0; skipped: 0.
  - `dotnet test .\tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj --no-build --no-restore --configuration Debug --filter "FullyQualifiedName~ConfigurationServiceFacts" --verbosity minimal`
  - Passed: 20; failed: 0; skipped: 0.
- `.\tests\NuGetGallery.CookieInteropTests\run-tests.ps1`
  - `legacy-issue`, `.NET 10 core-exchange`, and `legacy-accept` passed using the shared repository-backed ring.
- `dotnet build .\src\NuGetGallery.Core\NuGetGallery.Core.csproj --configuration Debug --verbosity minimal`
  - Passed for `net472` and `netstandard2.1`.
- Visual Studio MSBuild 18:
  - `NuGetGallery.sln /restore /t:Build /p:Configuration=Debug` passed.
  - `NuGet.Server.Common.sln /restore /t:Build /p:Configuration=Debug` passed.
- `dotnet list .\src\NuGetGallery.Core\NuGetGallery.Core.csproj package --vulnerable --include-transitive`
  - No vulnerability was reported for the new Data Protection or cryptography packages.
  - The only result was the repository's pre-existing transitive `Microsoft.Build.Tasks.Git` 8.0.0 advisory.
- `git diff --check` passed.

## Warnings

- Builds retain the repository's existing `NU1507` multi-source Central Package Management warning and the analyzer mismatch warning caused by SDK 10.0.400 with centrally pinned analyzers 8.0.0.
- The complete Gallery solution retains unrelated existing warnings, including `CS0618` in `TelemetryClientWrapper`.
- The 19 skipped Core tests are existing Azure Storage integration tests requiring external configuration.
- No Azure resources were contacted and no secrets, key XML, cookies, or connection strings were logged.

## Modified files

- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/04-shared-data-protection/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/04-shared-data-protection/progress-details.md`
- `Directory.Packages.props`
- `src/AccountDeleter/Configuration/GalleryConfiguration.cs`
- `src/NuGet.Services.KeyVault/IKeyEncryptionKeyMetadataValidator.cs`
- `src/NuGet.Services.KeyVault/KeyVaultKeyEncryptionKeyResolver.cs`
- `src/NuGetGallery.Core/CoreConstants.cs`
- `src/NuGetGallery.Core/NuGetGallery.Core.csproj`
- `src/NuGetGallery.Core/DataProtection/DataProtectionStorageType.cs`
- `src/NuGetGallery.Core/DataProtection/FileStorageXmlRepository.cs`
- `src/NuGetGallery.Core/DataProtection/SharedDataProtectionBuilderExtensions.cs`
- `src/NuGetGallery.Core/DataProtection/SharedDataProtectionConfiguration.cs`
- `src/NuGetGallery.Core/Services/BlobResultSegmentWrapper.cs`
- `src/NuGetGallery.Core/Services/CloudBlobCoreFileStorageService.cs`
- `src/NuGetGallery.Core/Services/FileSystemFileStorageService.cs`
- `src/NuGetGallery.Core/Services/FileSystemService.cs`
- `src/NuGetGallery.Core/Services/GalleryCloudBlobContainerInformationProvider.cs`
- `src/NuGetGallery.Core/Services/ICoreFileStorageService.cs`
- `src/NuGetGallery.Core/Services/IFileSystemService.cs`
- `src/NuGetGallery.Services/Configuration/AppConfiguration.cs`
- `src/NuGetGallery.Services/Configuration/IAppConfiguration.cs`
- `src/NuGetGallery.Services/Configuration/SharedDataProtectionConfigurationExtensions.cs`
- `src/NuGetGallery/App_Start/StorageDependent.cs`
- `src/NuGetGallery/Web.config`
- `tests/NuGetGallery.CookieInteropTests/CoreCookieRunner.cs`
- `tests/NuGetGallery.CookieInteropTests/LegacyCookieRunner.cs`
- `tests/NuGet.Services.KeyVault.Tests/KeyVaultKeyEncryptionKeyResolverFacts.cs`
- `tests/NuGetGallery.Core.Facts/DataProtection/FileStorageXmlRepositoryFacts.cs`
- `tests/NuGetGallery.Core.Facts/DataProtection/SharedDataProtectionConfigurationFacts.cs`
- `tests/NuGetGallery.Core.Facts/DataProtection/SharedDataProtectionIntegrationFacts.cs`
- `tests/NuGetGallery.Core.Facts/Services/CloudBlobCoreFileStorageServiceFacts.cs`
- `tests/NuGetGallery.Core.Facts/Services/FolderNamesDataAttribute.cs`
- `tests/NuGetGallery.Facts/App_Start/StorageDependentFacts.cs`
- `tests/NuGetGallery.Facts/Services/FileSystemFileStorageServiceFacts.cs`
- `tests/NuGetGallery.Facts/Services/SharedDataProtectionConfigurationExtensionsFacts.cs`

Pre-existing workflow state/assessment artifacts and unrelated untracked helper scripts were preserved. No commit was created.
