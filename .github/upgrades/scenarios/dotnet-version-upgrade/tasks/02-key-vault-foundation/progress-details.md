# Progress details

## Status

Completed and validated.

## Implementation

- Added `KeyVaultCredentialFactory` with independently testable selection for debug/local development, system-assigned managed identity, user-assigned managed identity, certificate authentication, and `SendX5c`.
- Added `KeyVaultClientFactory` for validated vault URI construction and reusable `SecretClient`, `KeyClient`, and `CryptographyClient` creation.
- Refactored `KeyVaultReader` to lazily use the factories while retaining `new KeyVaultReader(KeyVaultConfiguration)`, all eight `ISecretReader` methods, and the internal `SecretClient` test constructor/SendX5c seam.
- Added a factory-backed `KeyVaultDataSigner` overload without changing the existing `CryptographyClient` constructor or signing implementation.
- Added `KeyVaultKeyEncryptionKeyResolver` implementing `Azure.Core.Cryptography.IKeyEncryptionKeyResolver`.
  - Rejects non-HTTPS, foreign-vault, malformed key-path, user-info, non-default-port, query, and fragment identifiers before authenticated access.
  - Resolves versionless key IDs through native `KeyClient.GetKey`/`GetKeyAsync` calls on every request.
  - Validates Key Vault's returned versioned identifier and caches only versioned `IKeyEncryptionKey` clients.
  - Uses separate native synchronous and asynchronous paths and does not wrap Azure SDK failures.
- Added fake-backed tests for credential selection, client/vault validation, the complete secret-reader contract, resolver URI validation, key rotation, versioned caching, sync/async behavior, cancellation-token flow, and exception preservation.
- Confirmed no Data Protection registration, key creation/access, permission/configuration changes, .NET 10 wiring, or cookie/proxy/storage changes were introduced.

## Validation

- Exact narrow commands were re-run under the repository-selected SDK (`10.0.400`, because requested SDK `8.0.318` is not installed and `global.json` allows `latestMajor`):
  - `dotnet build .\src\NuGet.Services.KeyVault\NuGet.Services.KeyVault.csproj --no-restore --configuration Release --verbosity minimal`
    - Succeeded for `net472` and `netstandard2.0`; 0 errors.
    - Emitted two instances (one per TFM) of the external analyzer-tooling warning: `The .NET SDK has newer analyzers with version '10.0.400' than what version '8.0.0' of 'Microsoft.CodeAnalysis.NetAnalyzers' package provides. Update or remove this package reference. You can suppress this warning by setting the MSBuild property '_SkipUpgradeNetAnalyzersNuGetWarning' to 'true'.`
  - `dotnet test .\tests\NuGet.Services.KeyVault.Tests\NuGet.Services.KeyVault.Tests.csproj --no-restore --configuration Debug --verbosity minimal`
    - Passed: 93, failed: 0, skipped: 0.
    - Emitted the same analyzer-tooling warning for the source and test projects plus external restore warning `NU1507: There are 3 package sources defined in your configuration. When using central package management, map your package sources with package source mapping (https://aka.ms/nuget-package-source-mapping) or specify a single package source. The following sources are defined: dotnet-tools, NuGet.org, nuget-build`.
- Exact Visual Studio MSBuild 18 validation was re-run:
  - `MSBuild.exe .\NuGet.Server.Common.sln /restore /t:Build /p:Configuration=Debug /v:minimal /nologo`
  - Restore and build succeeded with exit code 0.
  - Captured 120 warning lines: 81 analyzer-tooling warnings described above, 34 `NU1507` external source-mapping warnings, four unsupported-TFM package warnings from unmodified `src/Microsoft.PackageManagement.Search.Web/Microsoft.PackageManagement.Search.Web.csproj`, and one `CS0618` warning from unmodified `src/NuGetGallery.Services/Telemetry/TelemetryClientWrapper.cs(30,17)`.
  - The four exact package warning messages were:
    - `System.Collections.Immutable 9.0.7 doesn't support net6.0 and has not been tested with it. Consider upgrading your TargetFramework to net8.0 or later. You may also set <SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings> in the project file to ignore this warning and attempt to run in this unsupported configuration at your own risk.`
    - `System.Diagnostics.DiagnosticSource 9.0.7 doesn't support net6.0 and has not been tested with it. Consider upgrading your TargetFramework to net8.0 or later. You may also set <SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings> in the project file to ignore this warning and attempt to run in this unsupported configuration at your own risk.`
    - `System.Drawing.Common 9.0.0 doesn't support net6.0 and has not been tested with it. Consider upgrading your TargetFramework to net8.0 or later. You may also set <SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings> in the project file to ignore this warning and attempt to run in this unsupported configuration at your own risk.`
    - `System.IO.Hashing 9.0.7 doesn't support net6.0 and has not been tested with it. Consider upgrading your TargetFramework to net8.0 or later. You may also set <SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings> in the project file to ignore this warning and attempt to run in this unsupported configuration at your own risk.`
  - The exact unrelated compiler warning was `CS0618: 'TelemetryClient.InstrumentationKey.set' is obsolete: 'InstrumentationKey based global ingestion is being deprecated. Recommended to set TelemetryConfiguration.ConnectionString. See https://github.com/microsoft/ApplicationInsights-dotnet/issues/2560 for more details.'`
- Warning-free proof for both modified projects:
  - Temporarily selected installed SDK `8.0.425`, which matches the repository's requested SDK major and the centrally pinned `Microsoft.CodeAnalysis.NetAnalyzers` 8.0.0, and used an ephemeral one-source NuGet configuration to isolate compilation from the repository's three-source restore infrastructure. Neither warning suppression nor source changes were used; `global.json` was restored byte-for-byte and the temporary NuGet configuration was deleted.
  - Isolated restore: 0 warnings, 0 errors.
  - `NuGet.Services.KeyVault.csproj` Release build (`net472;netstandard2.0`): 0 warnings, 0 errors.
  - `NuGet.Services.KeyVault.Tests.csproj` Debug build: 0 warnings, 0 errors.
  - Test execution with `--no-build --no-restore`: 93 passed, 0 failed, 0 skipped, 0 warnings.
- Warning disposition:
  - No compiler or analyzer diagnostic originates in task-02 source or tests.
  - The analyzer mismatch is caused by machine SDK roll-forward plus repository-wide analyzer pinning; `NU1507` is caused by repository-level external feed configuration. Changing either would affect the entire repository and is outside this independently cherry-pickable Key Vault foundation.
  - The package compatibility and `CS0618` warnings originate only in unrelated, unmodified projects. No `NoWarn`, pragma, or warning suppression was added.
- `git diff --check`
  - Passed.

## Modified files

- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/02-key-vault-foundation/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/02-key-vault-foundation/progress-details.md`
- `src/NuGet.Services.KeyVault/KeyVaultClientFactory.cs`
- `src/NuGet.Services.KeyVault/KeyVaultCredentialFactory.cs`
- `src/NuGet.Services.KeyVault/KeyVaultDataSigner.cs`
- `src/NuGet.Services.KeyVault/KeyVaultKeyEncryptionKeyResolver.cs`
- `src/NuGet.Services.KeyVault/KeyVaultReader.cs`
- `src/NuGet.Services.KeyVault/KeyVaultUriValidator.cs`
- `tests/NuGet.Services.KeyVault.Tests/KeyVaultClientFactoryFacts.cs`
- `tests/NuGet.Services.KeyVault.Tests/KeyVaultCredentialFactoryFacts.cs`
- `tests/NuGet.Services.KeyVault.Tests/KeyVaultKeyEncryptionKeyResolverFacts.cs`
- `tests/NuGet.Services.KeyVault.Tests/KeyVaultReaderFacts.cs`

Unrelated pre-existing workflow artifacts and task-01 changes were left untouched. No commit was created.
