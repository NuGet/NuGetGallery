# NuGetGallery .NET 10 Side-by-Side Proxy Upgrade Plan

## Goal and scope

Create `src\NuGetGallery.net10\NuGetGallery.net10.csproj` as a side-by-side ASP.NET Core host targeting `net10.0`. Local ASP.NET Core endpoints take precedence and a terminal YARP route proxies unmatched traffic to a configuration-driven legacy NuGetGallery origin. The existing .NET Framework application remains deployable and is the sole interactive sign-in authority and cookie refresher in this phase.

Both hosts explicitly use `.AspNet.LocalUser` and share an ASP.NET Core Data Protection cookie format. A one-time sign-in disruption at the App Service slot-swap format cutover is accepted. Existing Gallery libraries and EF6 may be reused; controller/view migration, EF Core, Azure deployment topology, Aspire changes, and retirement of the legacy project are out of scope.

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | Bottom-Up | The assessed graph contains 17 projects crossing a .NET Framework boundary, so dependency-first foundations must be validated before the new host consumes them. |
| Project Approach | Web project: Side-by-side; shared libraries: retain compatible existing targets and multi-target only where required | The 82,957-line classic web project has 2,410 API findings, including 2,135 ASP.NET Framework findings, while the approved phase keeps the legacy origin live. |
| Cross-App Cookie Authentication | Shared Cookie (Data Protection interop) | Both concurrently running hosts must understand `.AspNet.LocalUser`, and a one-time format cutover is explicitly acceptable. |
| Entity Framework | Keep EF6 | EF Core migration and new database behavior are outside the approved phase; compatible existing Gallery libraries may retain EF6 dependencies. |

### Selected Strategy
**Bottom-Up (Dependency-First)** — Upgrade from framework-neutral foundations to the root applications, validating each boundary before the next consumer is wired.
**Rationale**: The assessment covers a 17-project, multi-level dependency graph rooted at the classic `NuGetGallery.csproj`; the approved side-by-side modifier avoids retargeting that web project while preserving dependency-first validation.

## Dependency order

```text
Tier 4: NuGetGallery.net10 + legacy NuGetGallery integration
          ↓
Tier 3: Shared cookie and Data Protection integration
          ↓
Tier 2: Storage foundation | Key Vault foundation
          ↓
Tier 1: Existing framework-neutral Gallery and NuGet.Services contracts
```

Tasks execute strictly in numeric order. Task `01` and task `02` are separate foundation commits: each must be independently cherry-pickable onto unmodified `dev`, and neither may contain .NET 10, Data Protection, cookie, proxy, or migration configuration. Starting with task `03`, migration work may consume both foundations.

### 01-storage-foundation: Refactor Gallery storage without changing behavior

Create the independently deployable storage foundation across `NuGetGallery.Core`, `NuGetGallery.Services`, the legacy web project, jobs, AccountDeleter, shared solutions, and all tests and registration sites. Move `IsAvailableAsync` to `ICoreFileStorageService`; replace the MVC-returning download API with a framework-neutral `DownloadFileResult` that explicitly represents redirect, local-file, and not-found outcomes; propagate that result through package services; and map it to the unchanged MVC response only at controller boundaries. Remove `IFileStorageService` and update every implementation, mock, keyed Autofac registration, Microsoft DI registration, and consumer atomically.

Extract or move the framework-neutral portions of `FileSystemFileStorageService`, `IFileSystemService`, and `FileSystemService` into `NuGetGallery.Core`, leaving only unavoidable legacy root-resolution and `HostingEnvironment.MapPath` behavior in the web adapter. Preserve existing filesystem and Blob behavior, including redirect policy, CDN/query/version handling, conflict semantics, resource lifetimes, status codes, headers, filenames, and content types. Do not add file enumeration, Data Protection packages, key-ring storage, new storage capabilities, or any .NET 10 migration wiring.

**Done when**: A storage-only commit can be cherry-picked onto unmodified `dev`; `NuGetGallery.sln`, `NuGet.Server.Common.sln`, `NuGet.Jobs.sln`, and AccountDeleter's build surface restore and build; targeted storage, package-download, controller, registration, and availability tests pass; and observable HTTP and storage behavior is unchanged.

### 02-key-vault-foundation: Refactor Key Vault client creation and add key resolution

Create a second, independent foundation in `NuGet.Services.KeyVault`. Extract credential selection and Azure client construction from `KeyVaultReader` into reusable factories while preserving local/debug, system-assigned identity, user-assigned identity, certificate, and `SendX5c` behavior. Preserve `new KeyVaultReader(KeyVaultConfiguration)`, the complete `ISecretReader` sync/async contract, and the internal `SecretClient` test seam; reuse the factory in `KeyVaultDataSigner` only where signing behavior remains identical.

Add `KeyVaultKeyEncryptionKeyResolver` behind the official Azure `IKeyEncryptionKeyResolver` abstraction. It must validate expected HTTPS vault and key URIs before authenticated access, resolve versionless identifiers afresh, cache only versioned clients, and support native synchronous and asynchronous paths while preserving Azure SDK failures. This task must not depend on task `01`, register Data Protection in an application, create or access a wrapping key, request permissions, add migration settings, or contain any .NET 10 wiring.

**Done when**: A Key-Vault-only commit can be cherry-picked onto unmodified `dev`; existing secret-reader and signing behavior tests still pass; fake-backed credential-selection, URI-validation, cache, sync, and async resolver tests pass without Azure access; and all affected existing solutions restore and build.

### 03-cookie-interoperability: Prove bidirectional shared-cookie compatibility

Build a focused executable harness or integration suite before production startup is changed. Establish shared constants in a `netstandard2.1`-visible part of `NuGetGallery.Core` for `.AspNet.LocalUser`, authentication scheme/type, Data Protection application discriminator and purpose chain, path, and relevant claims. Exercise Katana `AspNetTicketDataFormat`/`DataProtectorShim` and ASP.NET Core cookie authentication with compatible package versions and chunking behavior.

Prove legacy-issued cookies authenticate under ASP.NET Core and ASP.NET Core-issued or refreshed cookies authenticate under Katana with equivalent username, name identifier, roles, authentication-method claim, and NuGet custom claims. Cover expiration, tampering, wrong key/purpose, oversized/chunked cookies, and the expected inability of the previous machine-key format to interoperate. Preserve the six-hour lifetime and sliding-expiration semantics while recording the accepted one-time reauthentication boundary.

**Done when**: Self-contained cross-framework tests pass on `net472` and `net10.0`, package compatibility is demonstrated rather than assumed, all shared cookie invariants are explicit, and no production host registration or request-path behavior has changed.

### 04-shared-data-protection: Add shared key-ring storage and Key Vault protection

Extend the storage foundation only now with paginated `ListFilesAsync` support for Blob and equivalent relative-name enumeration for the shared filesystem implementation. Add `FileStorageXmlRepository` in `NuGetGallery.Core`, a dedicated private Data Protection folder/container, create-only XML writes, complete enumeration and strict parsing, concurrency behavior, and the existing keyed `StorageDependent` registration model. Follow established Gallery connection-string, `$$secret$$`, managed-identity, user-assigned identity, local/debug credential, certificate, and container-initialization patterns.

Use the task `02` resolver with the official `Azure.Extensions.AspNetCore.DataProtection.Keys` integration and a versionless RSA/RSA-HSM Key Vault key URI; do not introduce a custom XML encryptor or wrapping format. Define equivalent legacy and modern configuration contracts, validate storage mode, location, discriminator, key lifetime, encryption-at-rest, and rotation/retention relationships, and keep production startup fail-closed. Preserve old ring entries and their referenced Key Vault versions, avoid automatic deletion or rewrapping, isolate the synchronous `IXmlRepository` boundary, and add safe telemetry without key XML, cookies, connection strings, or secrets.

**Done when**: Filesystem and Blob repository tests cover multi-page reads, malformed XML, create-only races, request-context rotation, restore, and missing old versions; fake-resolver integration tests prove official versioned key wrapping and multi-version rotation; both hosts can consume one configuration-equivalent ring; and production configuration cannot start with plaintext or incomplete protection.

### 05-net10-host: Scaffold the side-by-side ASP.NET Core host

Create SDK-style `src\NuGetGallery.net10\NuGetGallery.net10.csproj` targeting `net10.0`, with an explicit `Program` class and `Main` method. Register modern configuration and validation, secret injection, the shared Data Protection and cookie components, authorization, forwarded headers, YARP services, and reserved health/readiness endpoints where consistent with repository conventions. Reference only compatible existing Gallery targets needed by this phase, using central package-management conventions.

Add the project to the Frontend area of `NuGetGallery.sln` without changing `NuGetGallery.Aspire.slnx` or the Aspire AppHost. Do not add EF Core, migrations, schema changes, or new database behavior. Verify the .NET 10 SDK/targeting pack on developer and CI build paths; retain the existing `global.json` roll-forward behavior unless validation proves the `8.0.318` floor blocks a supported build.

**Done when**: The new host restores, builds, starts independently from configuration, exposes a stub local and health/readiness endpoint, loads the shared protection/authentication services without Azure or Aspire in self-contained tests, and the legacy web project remains unchanged and deployable.

### 06-yarp-fallback: Configure migration-first YARP routing

Make local ASP.NET Core endpoints authoritative and add a terminal anonymous catch-all proxy route to a validated legacy origin. Preserve method, raw/escaped path, query, body, response status/body, authentication cookie, `Set-Cookie`, redirects, cache, content-disposition, range, and conditional headers. Define trusted Host, scheme, client-IP, `Forwarded`, and `X-Forwarded-*` behavior so public URLs never leak the internal origin, and reject self-referential destinations before serving traffic.

Match or deliberately exceed legacy request-size and timeout limits, stream large package uploads/downloads without buffering, propagate cancellation, avoid unsafe retries for non-idempotent requests, and ensure local health/readiness paths do not accidentally shadow legacy routes. Keep route ownership granular so later controller migrations can replace proxy coverage piecemeal; no controller migration is part of this task.

**Done when**: In-process proxy tests cover local-route precedence, GET/HEAD/POST, multipart and near-limit bodies, streaming, ranges/conditionals, encoded paths, redirects, errors, cookies, cancellation, timeout, forwarded headers, and loop rejection, with every unmatched valid request reaching the fake legacy origin.

### 07-legacy-cookie-cutover: Apply the minimal legacy LocalUser cookie change

Change only the LocalUser Katana cookie registration and supporting legacy configuration. Explicitly set `.AspNet.LocalUser` and the proven interoperability values, construct the shared Data Protection provider from settings resolved through the existing `web.config` wrapper and credential factories, and resolve the keyed `ICoreFileStorageService`/official Key Vault integration as singletons. The format switch occurs at the slot swap, with one-time session loss at swap or rollback accepted.

Keep the legacy host as the sole issuer and refresher during this phase: disable competing ASP.NET Core sliding renewal and do not call `SignInAsync` for proxied requests. Preserve login paths, secure-cookie behavior, six-hour expiry, sliding expiration, claims, external-login flow, temporary external cookie, admin bearer authentication, anti-forgery protection, machine-key consumers, controllers, views, EF6, SQL, storage selection, and the rest of the OWIN pipeline.

**Done when**: Targeted legacy authentication tests and the task `03` compatibility suite pass against production-equivalent registration, a proxied authenticated request receives at most the legacy host's single renewal, invalid cookies still permit anonymous proxy fallback, and the legacy application remains independently deployable.

### 08-cross-host-regression-tests: Complete focused .NET 10 and cross-framework coverage

Add or extend `.Facts` projects according to repository conventions to make the approved behavior a permanent regression gate. Consolidate coverage for routing precedence, representative proxy traffic, request limits and streaming, encoded paths, redirects, cancellation/timeouts, role/claim authorization, cookie interoperability and single-renewal ownership, filesystem and Blob key repositories, concurrent immutable writes, multi-page enumeration, malformed XML, Data Protection and Key Vault rotation, restoration, and missing-old-version failure.

Update storage-download tests for framework-neutral redirect, local-file, and not-found outcomes plus unchanged MVC controller mapping. Validate startup rejection for invalid proxy, cookie, storage, lifetime, and Key Vault settings. Use in-process fake legacy origins and fake Azure abstractions; tests must not require Azure resources, LocalDB, EF migrations, production secrets, or Aspire.

**Done when**: The targeted legacy, new-host, storage, Key Vault, Data Protection, proxy, and cross-framework authentication suites pass deterministically and collectively cover every completion criterion without external infrastructure.

### 09-build-integration: Validate repository build boundaries and release readiness

Update only the build surfaces needed for the new SDK-style projects while preserving the Visual Studio/MSBuild path for the legacy solution. Validate every solution affected by the two foundations, explicitly including `NuGetGallery.sln`, `NuGet.Server.Common.sln`, `NuGet.Jobs.sln`, and AccountDeleter's containing build surface. Do not add the new host to unrelated solutions or alter frameworks/packages in Gallery Core, Gallery Services, jobs, validation, or functional-test projects except for narrowly required compatible cookie and foundation dependencies.

Perform the final dependency-order validation: foundations remain independently cherry-pickable and behavior-preserving; the new host starts without Aspire; local endpoints beat the proxy; unmatched requests reach the configured legacy origin; bidirectional shared cookies preserve relevant identity; renewal is legacy-owned; and invalid cookies do not block anonymous fallback. Document as post-upgrade work—rather than execute—production resource provisioning, topology/DNS/cutover, controller migration, EF Core, and eventual legacy-project removal.

**Done when**: All affected solutions restore and build through their established toolchains without new warnings, all targeted and applicable repository tests pass, no EF Core/schema/Aspire/deployment changes are present, and the side-by-side host satisfies the approved completion criteria.

### 10-auth-context-diagnostic: Add a development-only authentication diagnostic controller

Add a test controller/action to the .NET 10 Gallery that allows a developer signed in through the legacy Gallery to inspect the shared authentication context. Inspect existing identity, claims, local routing, and test patterns before implementation. Make the diagnostic endpoint available only in Development, prevent response caching, expose only the identity details needed to compare authentication in the two hosts, and never return cookies, tokens, or secrets. Register controller services and routes so the diagnostic action takes precedence over the YARP fallback without migrating existing controllers or changing the legacy application. Document exact local validation URLs and steps using existing legacy authentication surfaces. Cover anonymous and authenticated requests, local-route precedence, and non-Development unavailability with focused tests. Preserve legacy-only sign-in and renewal ownership.

**Done when**: The new controller can be exercised locally after legacy sign-in, the response reports the expected shared identity, targeted host tests pass, and the endpoint does not expose diagnostic information outside Development.

### 11-local-shared-key-ring: Align local launch configuration with the legacy authentication key ring

Configure the .NET 10 Development launch profile to use the existing legacy Gallery filesystem storage base instead of its independent `.data` directory. Preserve certificate selection and production configuration, use a portable relative path, and do not append the repository-managed `data-protection` subfolder. Verify launch working-directory semantics, shared repository key discovery, cookie authentication with equivalent legacy configuration, and update directly related local-development documentation. Do not copy or delete existing keys or change cookie issuance or renewal.

**Done when**: The standard local launch configuration resolves both hosts to the same key ring, focused authentication tests pass, and the user has precise restart and sign-in verification instructions.

## Execution constraints

- Execute tasks strictly in numeric order and commit after each task.
- Tasks `01` and `02` are independently cherry-pickable foundations; neither contains or depends on migration wiring.
- Validate each completed foundation against all existing higher-level consumers before beginning task `03`.
- Keep the old Framework project live and deployable throughout the side-by-side phase; never delete it in this plan.
- The legacy host is the only interactive sign-in authority and cookie refresher in this phase.
- Preserve the existing application database and EF6 behavior; no task may introduce EF Core or a schema migration.
- Use official Data Protection and Azure Key Vault extension points, existing Gallery Azure access patterns, and fail-closed production configuration.
- Keep controller migration, Azure resource provisioning/deployment topology, and Aspire changes as explicit follow-up work.
