# 10-auth-context-diagnostic: Add a development-only authentication diagnostic controller

Add a test controller/action to the .NET 10 Gallery that allows a developer signed in through the legacy Gallery to inspect the shared authentication context. Inspect existing identity, claims, local routing, and test patterns before implementation. Make the diagnostic endpoint available only in Development, prevent response caching, expose only the identity details needed to compare authentication in the two hosts, and never return cookies, tokens, or secrets. Register controller services and routes so the diagnostic action takes precedence over the YARP fallback without migrating existing controllers or changing the legacy application. Document exact local validation URLs and steps using existing legacy authentication surfaces. Cover anonymous and authenticated requests, local-route precedence, and non-Development unavailability with focused tests. Preserve legacy-only sign-in and renewal ownership.

**Done when**: The new controller can be exercised locally after legacy sign-in, the response reports the expected shared identity, targeted host tests pass, and the endpoint does not expose diagnostic information outside Development.

## Research confirmed before source edits (2026-10-06)

- Affected units: `src/NuGetGallery.net10/Program.cs`, a new attribute-routed
  `Controllers/AuthContextController.cs`, focused TestServer tests in
  `tests/NuGetGallery.net10.Facts`, and the local-development section of `README.md`.
  Both projects already target `net10.0`; no project/reference/package edits are needed.
- The host references Configuration and Gallery.Core. MVC comes from the existing
  Web SDK shared framework. Existing central versions are YARP 2.3.0, TestHost
  10.0.0, xUnit 2.9.0/adapter 2.8.2, Test SDK 17.10.0. Installed SDK is 10.0.401.
- `Program.BuildApplication` permits builder customization and uses authentication,
  authorization, local endpoints, then a YARP forwarder endpoint with
  `Order = int.MaxValue`. No controller services or controller mapping currently exist.
  Existing tests generate protected LocalUser tickets, use TestServer, and create
  isolated filesystem key rings. Follow that pattern without a runtime sign-in endpoint.
- Shared cookie constants specify `.AspNet.LocalUser`, scheme `LocalUser`, name,
  name-identifier and role claim types. `AuthenticationService.CreateIdentity` in
  Gallery.Services uses the username for both name and name identifier. Legacy
  `Views/Shared/Gallery/Header.cshtml` displays `User.Identity.Name`; `/account`
  uses `Users.Account`, and `/users/account/LogOn` uses existing authentication routing.
  Do not return arbitrary claims (external credential identities can be sensitive).
- `SharedDataProtectionServiceCollectionExtensions` disables Core sliding expiration.
  Diagnostic requests must not issue/renew cookies; tests will use a ticket older
  than half its lifetime and check the absence of Set-Cookie.
- Assessment scope clarification and the embedded NuGetGallery.csproj detail were read:
  System.Web/OWIN remain legacy-only; legacy binding-redirect findings are unrelated.
  Both per-project assessment queries report project-not-found because these new
  projects were added after assessment. No assessment package actions apply here.
- Required dependency helper was attempted for Program.cs but could not load its
  Roslyn compilation. Route ownership was attempted with both absolute project paths:
  proxyStatus Unreadable, with 1 MalformedSource (Core project not loaded in workspace),
  1 OpaqueRegistrar, 21 UnresolvedSymbol, 9 UnsupportedRegistrationApi gaps.
  This is not proof of forwarding or permission to remove legacy routes. No existing
  routes are moved; TestServer requests will establish this new endpoint's precedence.
- No `// STUB:` markers occur in either affected project. Already-done check: no
  diagnostic/controller exists, so implementation is necessary.

## Execution reference and decomposition verdict

Loaded scenario `execution.md` and `breakdown-hints/common.md` plus `test.md`.
Verdict: **atomic** — one specific new diagnostic endpoint with its wiring, tests,
and documentation; no code movement, framework/package change, or independent concerns.
Framework migration/web hints do not apply because legacy code is not modified.
Execution extensions: none apply.

Implement `GET /_local/auth-context` as a real ControllerBase action. Register MVC
and map controllers only in Development, with an explicit application part so tests
do not accidentally rely on the test process entry assembly. Reserve this path with
a local no-store 404 for all methods outside Development so it cannot fall through
to the legacy proxy. Include an action-level environment guard as defense in depth.
Allow anonymous inspection; return only host marker, authenticated flag, auth type,
username, name identifier and roles. No claims dump, authentication properties,
cookies, headers, tokens, or secrets. Apply no-store response caching.

Tests: anonymous, valid reader-only cookie, invalid/expired cookie, exact allowlisted
JSON shape, MVC endpoint metadata and local precedence without a live legacy host,
Staging/Production/custom-environment unavailability and zero cookie issuance.
Use production encryption configuration without connecting to Azure in anonymous
non-development tests. Build/test via `dotnet` against host + Facts only; preserve
known unrelated baseline warnings/failures without suppression or fixes.
Browser validation is a manual user check (actual legacy sign-in credentials and
running legacy origin required), not something synthetic ticket tests can prove.
