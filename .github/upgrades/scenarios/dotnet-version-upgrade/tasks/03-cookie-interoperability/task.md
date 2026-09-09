# 03-cookie-interoperability: Prove bidirectional shared-cookie compatibility

Build a focused executable harness or integration suite before production startup is changed. Establish shared constants in a `netstandard2.1`-visible part of `NuGetGallery.Core` for `.AspNet.LocalUser`, authentication scheme/type, Data Protection application discriminator and purpose chain, path, and relevant claims. Exercise Katana `AspNetTicketDataFormat`/`DataProtectorShim` and ASP.NET Core cookie authentication with compatible package versions and chunking behavior.

Prove legacy-issued cookies authenticate under ASP.NET Core and ASP.NET Core-issued or refreshed cookies authenticate under Katana with equivalent username, name identifier, roles, authentication-method claim, and NuGet custom claims. Cover expiration, tampering, wrong key/purpose, oversized/chunked cookies, and the expected inability of the previous machine-key format to interoperate. Preserve the six-hour lifetime and sliding-expiration semantics while recording the accepted one-time reauthentication boundary.

**Done when**: Self-contained cross-framework tests pass on `net472` and `net10.0`, package compatibility is demonstrated rather than assumed, all shared cookie invariants are explicit, and no production host registration or request-path behavior has changed.

## Repository research (2026-09-08)

- `src/NuGetGallery.Services/Authentication/Providers/LocalUser/LocalUserAuthenticator.cs` is the current Katana registration. It uses authentication type `LocalUser`, active mode, HTTP-only cookies, a six-hour expiration, sliding expiration, and the default cookie path. Because no name is set, Katana derives the deployed `.AspNet.LocalUser` name from the authentication type.
- `src/NuGetGallery.Services/Authentication/AuthenticationTypes.cs` currently owns the `LocalUser` string, but that project is not available to a future `net10.0` host. Shared wire-contract constants will therefore be added to `NuGetGallery.Core`, which already targets both `net472` and `netstandard2.1`, without changing the existing production registration.
- `src/NuGetGallery.Services/Authentication/AuthenticationService.cs` creates identities with the username as the default name and name-identifier claims, default role claims, an authentication-method claim, and the `LocalUser` authentication type. Its login flow can also emit the custom claims declared by `src/NuGetGallery.Core/Authentication/NuGetClaims.cs`: discontinued/password/external login, external identities, MFA enabled/performed, and external-login credential type.
- The repository uses Central Package Management in `Directory.Packages.props`. Existing relevant versions are Katana `4.2.2`, `Microsoft.Owin.Host.SystemWeb` `4.2.2`, xUnit `2.9.0`, and runner `2.8.2`. There are no existing Interop or shared Data Protection package references.
- The sharing-cookie skill requires Framework 4.6.2 or newer (satisfied by `net472`), `Microsoft.Owin.Security.Interop` `2.3.11`, `Microsoft.AspNetCore.DataProtection.Extensions` `2.3.10`, a secure compatible `System.Security.Cryptography.Xml` override (`10.0.11`), the purpose chain `Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware` / scheme / `v2`, and Core-compatible `chunks-N` cookies. The full OWIN migration skill is not applicable because production Katana is supported and remains the sign-in authority.
- No ASP.NET Core host exists yet; that is task 05. This task will therefore add a focused, multi-target executable interoperability harness under `tests` rather than changing startup. The `net472` leg will use `AspNetTicketDataFormat`, `DataProtectorShim`, and the Interop chunk manager. The `net10.0` leg will use the real ASP.NET Core cookie middleware with `TestServer`, exchange cookies through an isolated on-disk key ring, and return Core-issued and sliding-refreshed cookies to the Framework leg.
- The harness will verify both directions and equivalent username, name identifier, roles, authentication method, and NuGet custom claims. It will also exercise expired, tampered, wrong-key, wrong-purpose, oversized/chunked, and old `MachineKey` values. Its runner will remove generated key and exchange artifacts.

## Planned affected files

- `src/NuGetGallery.Core/Authentication/SharedCookieConstants.cs` — framework-neutral cookie wire-contract constants.
- `tests/NuGetGallery.CookieInteropTests/*` — cross-framework executable harness and runner.
- `Directory.Packages.props` — centrally managed, target-compatible harness package versions.
- This task and `progress-details.md` — research and completion evidence only.
