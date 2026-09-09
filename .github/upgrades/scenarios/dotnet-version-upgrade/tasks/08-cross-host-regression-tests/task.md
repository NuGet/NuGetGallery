# 08-cross-host-regression-tests: Complete focused .NET 10 and cross-framework coverage

Add or extend `.Facts` projects according to repository conventions to make the approved behavior a permanent regression gate. Consolidate coverage for routing precedence, representative proxy traffic, request limits and streaming, encoded paths, redirects, cancellation/timeouts, role/claim authorization, cookie interoperability and single-renewal ownership, filesystem and Blob key repositories, concurrent immutable writes, multi-page enumeration, malformed XML, Data Protection and Key Vault rotation, restoration, and missing-old-version failure.

Update storage-download tests for framework-neutral redirect, local-file, and not-found outcomes plus unchanged MVC controller mapping. Validate startup rejection for invalid proxy, cookie, storage, lifetime, and Key Vault settings. Use in-process fake legacy origins and fake Azure abstractions; tests must not require Azure resources, LocalDB, EF migrations, production secrets, or Aspire.

**Done when**: The targeted legacy, new-host, storage, Key Vault, Data Protection, proxy, and cross-framework authentication suites pass deterministically and collectively cover every completion criterion without external infrastructure.

## Coverage audit

Existing Task 01-07 tests already cover:

- `tests/NuGetGallery.net10.Facts/LegacyProxyFacts.cs`: local-route precedence, unmatched fallback, GET/HEAD/POST and multipart traffic, request/response streaming, range and conditional headers, raw paths, redirects, errors, cookies, forwarded headers, cancellation, timeout, no POST retry, and loop/invalid-origin rejection.
- `tests/NuGetGallery.CookieInteropTests`: bidirectional `net472`/`net10.0` ticket exchange, identity/role/custom-claim parity, six-hour expiry, sliding refresh, tampering, wrong key/purpose, machine-key rejection, and chunked tickets.
- `tests/NuGetGallery.Facts/Authentication/Providers/LocalUser/LocalUserAuthenticatorFacts.cs` and the proxy renewal facts: production-equivalent legacy cookie registration, fail-closed provider creation, and legacy-only renewal ownership.
- `tests/NuGetGallery.Core.Facts/DataProtection/*`, `tests/NuGetGallery.Core.Facts/Services/CloudBlobCoreFileStorageServiceFacts.cs`, and `tests/NuGetGallery.Facts/Services/FileSystemFileStorageServiceFacts.cs`: filesystem/Blob names, Blob pagination, strict XML parsing, immutable/create-race behavior, synchronization-context isolation, official Key Vault wrapping, rotation, restore, and missing-old-version failure.
- `tests/NuGetGallery.Facts/Services/*FileStorageServiceFacts.cs`, `PackageFileServiceFacts.cs`, and `Controllers/ApiControllerFacts.cs`: neutral redirect/local-file/not-found outcomes and unchanged MVC redirect/file/not-found mapping.
- `tests/NuGet.Services.KeyVault.Tests/KeyVaultKeyEncryptionKeyResolverFacts.cs`: URI validation, native sync/async resolution, versioned-client caching, fresh versionless resolution, rotation, and Azure failure propagation.

Deterministic regression gaps identified and closed:

1. Exercise ASP.NET Core role and custom-claim authorization with a ticket protected by the production shared-cookie options, including unauthenticated challenge and authenticated forbid outcomes.
2. Assert the new host's reader-only cookie contract completely: six-hour lifetime, secure/HTTP-only/path values, and no sliding renewal.
3. Exercise the production Kestrel pipeline with a declared streaming body exactly at
   the configured limit and one byte over it. The boundary request reaches and can be
   read by the fake origin; the over-limit request returns 413 before the origin is
   invoked or consumes body bytes. This exposed and required a surgical Content-Length
   guard in `LegacyProxyForwarder`, which uses the active
   `IHttpMaxRequestBodySizeFeature` value with the validated configured limit as its
   non-server fallback.
4. Exercise fail-closed host construction/startup for invalid Data Protection storage, cookie discriminator, key lifetime, plaintext production keys, and malformed/versioned Key Vault identifiers. Existing proxy startup rejection tests remain the proxy-settings gate.
5. Exercise runtime enforcement after the preflight path with a test-only
   `IHttpMaxRequestBodySizeFeature` override on the real Kestrel production pipeline:
   transmit complete exact-limit bodies with both `Content-Length` and chunked framing,
   and a complete limit-plus-one chunked body. The unknown-length overflow may contact
   the origin and deliver bytes up to the limit, but must finish as 413 rather than
   YARP's generic 502; production must remain streaming and retain its configured
   defaults. For chunked HTTP/1.1, assertions use Kestrel's wire-body accounting
   (payload plus chunk framing), verify the origin receives no `Content-Length`, and
   coordinate writes so origin contact and positive consumption are deterministic.
   Consumption may include the full decoded payload before framing pushes the wire
   body over the limit, so assertions must not depend on TCP packet boundaries.

The source changes are limited to the focused Facts files plus the request-limit defect
exposed in `src/NuGetGallery.net10/LegacyProxyForwarder.cs`.
