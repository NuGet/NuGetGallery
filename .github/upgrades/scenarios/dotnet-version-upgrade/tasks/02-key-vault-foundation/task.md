# 02-key-vault-foundation: Refactor Key Vault client creation and add key resolution

Create a second, independent foundation in `NuGet.Services.KeyVault`. Extract credential selection and Azure client construction from `KeyVaultReader` into reusable factories while preserving local/debug, system-assigned identity, user-assigned identity, certificate, and `SendX5c` behavior. Preserve `new KeyVaultReader(KeyVaultConfiguration)`, the complete `ISecretReader` sync/async contract, and the internal `SecretClient` test seam; reuse the factory in `KeyVaultDataSigner` only where signing behavior remains identical.

Add `KeyVaultKeyEncryptionKeyResolver` behind the official Azure `IKeyEncryptionKeyResolver` abstraction. It must validate expected HTTPS vault and key URIs before authenticated access, resolve versionless identifiers afresh, cache only versioned clients, and support native synchronous and asynchronous paths while preserving Azure SDK failures. This task must not depend on task `01`, register Data Protection in an application, create or access a wrapping key, request permissions, add migration settings, or contain any .NET 10 wiring.

**Done when**: A Key-Vault-only commit can be cherry-picked onto unmodified `dev`; existing secret-reader and signing behavior tests still pass; fake-backed credential-selection, URI-validation, cache, sync, and async resolver tests pass without Azure access; and all affected existing solutions restore and build.

## Repository research

Research completed before source changes on 2026-09-08:

- `src/NuGet.Services.KeyVault/NuGet.Services.KeyVault.csproj` is SDK-style and targets `net472;netstandard2.0`. It already references `Azure.Core` 1.50.0, `Azure.Identity` 1.17.1, `Azure.Security.KeyVault.Keys` 4.4.0, and `Azure.Security.KeyVault.Secrets` 4.4.0 through `Directory.Packages.props`; no Data Protection package is needed because the official `Azure.Core.Cryptography.IKeyEncryptionKeyResolver` and `IKeyEncryptionKey` contracts are already in Azure.Core.
- `KeyVaultReader` currently owns credential selection and lazy `SecretClient` creation. Its public `KeyVaultReader(KeyVaultConfiguration)` constructor and all eight `ISecretReader` methods must remain unchanged. Tests use the internal `(SecretClient, KeyVaultConfiguration, bool)` constructor and `_isUsingSendx5c` seam.
- Managed-identity configuration supports a null client ID (system-assigned), a client ID (user-assigned), and `LocalDevelopment`; debug builds currently select `DefaultAzureCredential`. Certificate configuration selects `ClientCertificateCredential` and enables `ClientCertificateCredentialOptions.SendCertificateChain` for `SendX5c`.
- `KeyVaultDataSigner` currently accepts an already-created `CryptographyClient`; that constructor and signing implementation must stay unchanged. A factory-backed construction path can be added without changing existing callers.
- The production project has access to mockable Azure SDK clients. `NuGet.Services.KeyVault.Tests` targets `net472`, uses xUnit/Moq, and has `InternalsVisibleTo`, so internal delegates/factory seams can exercise credential selection and resolver behavior without Azure access.
- `KeyVaultKeyIdentifier` from `Azure.Security.KeyVault.Keys` parses key name/version but explicitly does not establish host trust. The resolver therefore needs separate checks for an absolute HTTPS URI, the configured vault host, the `/keys/{name}[/{version}]` shape, and absence of user-info/query/fragment/non-default ports before creating or calling an authenticated client.
- Versionless resolution will call `KeyClient.GetKey` or `GetKeyAsync` on every request, validate the returned versioned ID, and cache only the resulting versioned `IKeyEncryptionKey`. Direct versioned identifiers use the same cache. Azure SDK exceptions will be allowed to propagate unchanged.

## Planned affected files

- Refactor `src/NuGet.Services.KeyVault/KeyVaultReader.cs`.
- Add reusable credential/client factories and the official resolver under `src/NuGet.Services.KeyVault/`.
- Add only a factory-backed overload to `src/NuGet.Services.KeyVault/KeyVaultDataSigner.cs`.
- Expand `tests/NuGet.Services.KeyVault.Tests/KeyVaultReaderFacts.cs` and add focused factory/resolver facts.
- No application registration, wrapping-key access, permission/configuration changes, .NET 10 wiring, or task-01/storage changes.

## Validation plan

- Run the focused `NuGet.Services.KeyVault.Tests` project.
- Build `NuGet.Services.KeyVault.csproj` for both `net472` and `netstandard2.0`.
- Build `NuGet.Server.Common.sln` with Visual Studio MSBuild because it contains the project and its tests.
