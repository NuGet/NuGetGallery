# 03-cookie-interoperability progress

## Status

Completed on 2026-09-08. No production host registration or request-path code was changed.

## Implemented

- Added `SharedCookieConstants` to the `netstandard2.1`-visible `NuGetGallery.Core` authentication namespace. It fixes the shared name (`.AspNet.LocalUser`), `LocalUser` scheme/type, `/` path, `NuGetGallery` application discriminator, Cookie middleware/scheme/`v2` purpose chain, six-hour lifetime, sliding-expiration setting, standard identity claims, and local-user NuGet claims.
- Added a focused multi-target executable harness. Its `net472` leg runs real Katana cookie middleware with `AspNetTicketDataFormat`, `DataProtectorShim`, and `Microsoft.Owin.Security.Interop.ChunkingCookieManager`. Its `net10.0` leg runs real ASP.NET Core cookie authentication middleware under `TestServer`.
- The legs exchange cookies through a shared on-disk Data Protection key ring and validate both directions. ASP.NET Core accepts a Katana-issued cookie and sliding-refreshes it; Katana accepts both Core-issued and Core-refreshed cookies.
- Both hosts validate equivalent username, name identifier, roles, authentication method, authentication type, and the discontinued/password/external-login, external identity, MFA, and external credential-type NuGet claims.
- Negative/edge coverage rejects expiration, tampering, wrong key ring, wrong purpose, and a real Katana v1 ticket protected by `MachineKey`. Both directions issue, reassemble, and authenticate a 20 KB claim ticket using the required `chunks-N` marker.
- The harness checks the explicit `/` path and six-hour ticket lifetime. Generated keys and exchange files are removed in a `finally` block.

## Package compatibility

Central versions and successful resolved versions:

- `Microsoft.AspNetCore.DataProtection.Extensions` 2.3.10 (`net472`)
- `Microsoft.Owin.Security.Interop` 2.3.11 (`net472`)
- `Microsoft.Owin.Testing` 4.2.2 (`net472`)
- `System.Security.Cryptography.Xml` 10.0.11 (`net472`, secure transitive override)
- `Microsoft.AspNetCore.TestHost` 10.0.0 (`net10.0`)

`dotnet list ... package --vulnerable --include-transitive` found no vulnerability in these interoperability packages. It reported the repository's existing global `Microsoft.Build.Tasks.Git` 8.0.0 transitive package (GHSA-23fw-v26w-5fgq) for both targets; this task did not alter that unrelated build dependency.

## Validation

- `.\tests\NuGetGallery.CookieInteropTests\run-tests.ps1` — passed:
  - `legacy-issue passed.`
  - `core-exchange passed.`
  - `legacy-accept passed.`
  - `Cookie interoperability harness passed on net472 and net10.0.`
- `dotnet build src\NuGetGallery.Core\NuGetGallery.Core.csproj --framework netstandard2.1 --configuration Debug --no-restore --verbosity minimal` — succeeded, 0 errors.
- `dotnet list tests\NuGetGallery.CookieInteropTests\NuGetGallery.CookieInteropTests.csproj package` — confirmed the exact versions above.
- Source search after implementation found production cookie registration only in the pre-existing `OwinStartup.cs` and `LocalUserAuthenticator.cs`; neither file changed.
- `git diff --check` passed for tracked changes, a separate trailing-whitespace scan passed for every new task file, and generated harness artifacts were absent.

Build output retains the repository's existing NU1507 multi-source Central Package Management warning and .NET 10 SDK warning that the centrally pinned .NET analyzers 8.0.0 are older than the SDK analyzers. There were no harness compilation warnings or errors attributable to this task.

## Changed files

- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/03-cookie-interoperability/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/03-cookie-interoperability/progress-details.md`
- `Directory.Packages.props`
- `src/NuGetGallery.Core/Authentication/SharedCookieConstants.cs`
- `tests/NuGetGallery.CookieInteropTests/NuGetGallery.CookieInteropTests.csproj`
- `tests/NuGetGallery.CookieInteropTests/Program.cs`
- `tests/NuGetGallery.CookieInteropTests/LegacyCookieRunner.cs`
- `tests/NuGetGallery.CookieInteropTests/CoreCookieRunner.cs`
- `tests/NuGetGallery.CookieInteropTests/run-tests.ps1`
