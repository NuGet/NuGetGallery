# 08-cross-host-regression-tests progress

## STATUS

COMPLETED

## Coverage audit and implementation

- Audited the Task 01-07 regression suites against every completion criterion and
  recorded the existing coverage and remaining gaps in `task.md` before editing
  source files.
- Kept production changes surgical: `LegacyProxyForwarder` now rejects a declared
  `Content-Length` above the active Kestrel/IIS
  `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` (falling back to the validated
  configured limit when the server does not expose the feature). This prevents YARP
  from converting that client error into a 502 after opening the destination request.
- Extended `ProgramFacts` with seven deterministic cases:
  - production shared-cookie options now explicitly assert the six-hour lifetime,
    HTTP-only flag, secure policy, path, and disabled Core-side sliding renewal;
  - a shared-format cookie exercises ASP.NET Core role and custom-claim policies,
    including 401 challenge, 403 forbid, and successful role/claim authorization;
  - request-limit startup validation now covers a value one byte below the legacy
    compatibility floor as well as unlimited and native-IIS overflow values;
  - host construction fails closed for invalid storage type, shared-cookie
    discriminator, key lifetime, plaintext production key storage, and a versioned
    Key Vault key identifier.
- Added the missing key-ring retention relationship test to
  `SharedDataProtectionConfigurationFacts`.
- Added two real-Kestrel production-pipeline tests without allocating or transmitting
  a 250 MB payload. A controlled streaming request declares exactly 262,144,000 bytes
  and proves the fake origin can read its first byte; a request declaring 262,144,001
  bytes receives 413 while the fake origin records zero requests and zero body bytes.
- Added four full-body cases using a test-only startup filter that lowers the active,
  writable `IHttpMaxRequestBodySizeFeature` for real Kestrel requests without changing
  production defaults:
  - a 32-byte known-length body is completely streamed at a 32-byte limit;
  - a 33-byte known-length body is rejected with 413 before origin contact;
  - a complete 43-byte chunked wire body (32 payload bytes plus framing) is accepted,
    reaches the origin without `Content-Length`, and is fully consumed;
  - a 44-byte chunked wire body is sent in two controlled writes, reaches the origin,
    permits positive consumption up to and including the full decoded payload, and
    then finishes as 413 when framing exceeds the wire-body limit. The assertion uses
    a `1..payloadLength` bound and is deliberately independent of TCP packet
    segmentation.
  The sink counts bytes without retaining them, so production and tests retain streaming
  behavior and avoid large allocation or transfer.
- Confirmed the existing suites already cover route precedence, proxy methods,
  multipart/request/response streaming, raw paths, redirects, errors, headers,
  cancellation/timeouts, renewal ownership, invalid-cookie fallback, bidirectional
  cookie claims/chunking/failure cases, filesystem and Blob enumeration and
  immutable writes, strict XML parsing, official Key Vault rotation/restoration,
  missing old versions, neutral download outcomes, and MVC result mapping.

## Modified files

- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/08-cross-host-regression-tests/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/08-cross-host-regression-tests/progress-details.md`
- `src/NuGetGallery.net10/LegacyProxyForwarder.cs`
- `tests/NuGetGallery.net10.Facts/LegacyProxyFacts.cs`
- `tests/NuGetGallery.net10.Facts/ProgramFacts.cs`
- `tests/NuGetGallery.Core.Facts/DataProtection/SharedDataProtectionConfigurationFacts.cs`

## Validation

- Independent authoritative `NuGetGallery.net10.Facts` runs:
  - Debug: 48 passed; 0 failed; 0 skipped.
  - Release: 48 passed; 0 failed; 0 skipped.
- Independent authoritative `NuGetGallery.Core.Facts` run:
  - 1,194 passed; 0 failed; 19 skipped.
- Independent authoritative `NuGet.Services.KeyVault.Tests` run:
  - 100 passed; 0 failed; 0 skipped.
- Independent authoritative full `NuGetGallery.Facts` run:
  - 11,085 passed; 0 failed; 2 skipped.
- `tests\NuGetGallery.CookieInteropTests\run-tests.ps1`:
  - 3 of 3 stages passed: `legacy-issue`, `core-exchange`, and `legacy-accept`.
- The six request-body-limit cases passed 6 of 6 when repeated three times in both
  Debug and Release configurations. The chunked overflow invariant remains HTTP 413,
  origin contacted, no `Content-Length`, and origin consumption in the stable range
  `1..full decoded payload`.
- Visual Studio MSBuild of `NuGetGallery.sln`:
  - 0 errors; 78 pre-existing warnings.
- `git diff --check`: passed.

## Warnings and environment notes

- Successful SDK builds retain the repository's existing `NU1507` warning for
  three package sources under Central Package Management and the existing warning
  that `Microsoft.CodeAnalysis.NetAnalyzers` 8.0.0 is older than the .NET 10 SDK
  analyzers. No new warning was introduced.
- Visual Studio MSBuild completed the legacy solution successfully. Its 78 warnings
  are pre-existing and unrelated to Task 08.
- All tests use TestServer, loopback Kestrel, in-memory fakes, mocked Azure
  abstractions, or temporary paths beneath test output. No Azure resource,
  LocalDB, EF migration, production secret, or Aspire dependency was used.
- Pre-existing workflow/assessment artifacts were preserved. No commit was
  created.
