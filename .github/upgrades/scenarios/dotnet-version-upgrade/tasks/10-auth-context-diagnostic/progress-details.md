# Execution entry — 2026-10-06

## Changes

- Added `src/NuGetGallery.net10/Controllers/AuthContextController.cs`: real
  attribute-routed ControllerBase action, AllowAnonymous, Development guard,
  `ResponseCache(NoStore = true, Location = None)`.
- Updated `src/NuGetGallery.net10/Program.cs`: Development-only controller DI
  registration with explicit application part and `MapControllers`, before the
  existing last-order YARP catch-all. Non-Development reserves the diagnostic
  path locally for all methods with empty 404 and `Cache-Control: no-store`.
- Added `tests/NuGetGallery.net10.Facts/AuthContextFacts.cs`: eight cases.
- Updated existing `README.md` local-development documentation with prerequisites,
  exact URLs, example JSON, shared-key-directory command, and browser checks.
- Enriched task.md **before** source edits. Wrote breakdown-context.md and this
  progress-details.md. No existing progress entry existed to preserve.

No legacy controllers/routes, auth issuance/renewal, project files, packages,
Aspire/deployment or EF changes. No commits or branch rename. Preserved all
preexisting plan/preferences/scenario/task-list changes, launchSettings.json and
the untracked local `.data` directory.

## Exact route and output

GET `https://localhost:7150/_local/auth-context` (Development).

Anonymous, invalid, or expired authentication cookie:

```json
{"host":"NuGetGallery.net10","isAuthenticated":false,"authenticationType":null,"name":null,"nameIdentifier":null,"roles":[]}
```

Authenticated example:

```json
{"host":"NuGetGallery.net10","isAuthenticated":true,"authenticationType":"LocalUser","name":"interop-user","nameIdentifier":"interop-user","roles":["Administrators","PackageOwners"]}
```

Only the six explicit fields are serialized. Role names are sorted/distinct and
may be empty; actual usernames/roles come from the validated shared cookie.
No arbitrary claims, email, external credential identities, tokens, ticket
properties, headers, cookies or secrets are returned. No Set-Cookie is issued,
including a valid ticket older than half its six-hour lifetime.

## Validation and resolved failures

Commands:

1. `dotnet test tests/NuGetGallery.net10.Facts/NuGetGallery.net10.Facts.csproj --filter FullyQualifiedName~AuthContextFacts --verbosity minimal`
   Initial run exposed environment-DI disagreement in TestServer and missing
   staging PublicOrigins. Fixed production code to inject **IWebHostEnvironment**
   (the same environment used by Program's registration/mapping), and configured
   the test public origin explicitly. Did not weaken environment/auth guards.
2. `dotnet test tests/NuGetGallery.net10.Facts/NuGetGallery.net10.Facts.csproj --verbosity minimal`
   **56 passed, 0 failed, 0 skipped**, including existing proxy/auth host regressions.
3. `dotnet build src/NuGetGallery.net10/NuGetGallery.net10.csproj --verbosity minimal`
   **0 errors, 8 warning occurrences**, build succeeded using SDK 10.0.401.
4. `dotnet test tests/NuGetGallery.net10.Facts/NuGetGallery.net10.Facts.csproj --no-build --filter FullyQualifiedName~AuthContextFacts --verbosity minimal`
   **8 passed, 0 failed, 0 skipped** after the final host build.
5. `git diff --check`: passed. Both host and Facts assemblies verified under
   `bin/Debug/net10.0`.

No new C# or test-analyzer warnings. Existing warnings remain, intentionally not
suppressed/fixed per dispatch: NU1902 for Microsoft.Build.Tasks.Git 8.0.0 (host
build reports it at restore and build), SDK-vs-NetAnalyzers 8.0.0 version warnings
(six projects in host graph; seven in Facts graph), and Facts NU1507 multiple
package sources with central package management. These require separate package/
repository configuration work. The containing `NuGetGallery.sln` was subsequently built successfully through
Visual Studio MSBuild with 0 errors and 73 existing warnings. The full host suite
was independently rerun: 56 passed, 0 failed. No new compilation warnings were
reported in the modified host or Facts projects.

Coverage: anonymous/authenticated/invalid/expired real cookie middleware requests,
exact allowlisted JSON, exclusion of secret custom claims/properties/request data,
no-store, no issuance/renewal, actual MVC controller endpoint metadata and lower
order than actual YARP fallback, no live legacy origin required for local response.
Full Staging host returns empty local 404. Production/Staging/custom-environment
tests exercise the **same Program mapping helper** in an isolated TestServer host
with a marker fallback and also test the action's environment guard. GET and POST
cannot forward the reserved path; other paths still reach fallback.

Deviation: Production routing is tested in isolation rather than booting full
Production auth/storage, whose required Key Vault metadata validation calls Azure
at startup. No Azure dependency was added to these tests, nor was production
encryption/security relaxed. Staging exercises full Program registration/mapping.
The new projects are absent from the old assessment; query/dependency/route
helpers' limitations and unresolved inventory are recorded in task.md.

## Done-when evidence and manual browser check

- Real action and DI/mapping work: confirmed by requests and MVC metadata tests.
- Shared identity is reported: confirmed by real protected LocalUser cookie tests.
- Targeted host tests pass: 56/56, with focused 8/8 rerun.
- No diagnostic outside Development: environment mapping/action tests, empty
  no-store 404; Core never sends the reserved non-Development path to legacy.
- Actual legacy-sign-in/browser comparison is **pending user verification**:
  tests use synthetic protected cookies and do not claim to have signed into a
  live legacy Gallery. No legacy diagnostic was added.

Browser steps (also in README):

1. Start legacy Gallery on `https://localhost` with trusted localhost certificate.
   Resolve its actual `Gallery.FileStorageDirectory` (default physical directory
   `src/NuGetGallery/App_Data/Files`). Core `.data` is a different default.
2. From repository root:
   `dotnet run --project src/NuGetGallery.net10/NuGetGallery.net10.csproj --launch-profile NuGetGallery.net10 -- --DataProtection:StorageLocation "<absolute-shared-key-directory>"`
   Use HTTPS and the same hostname in both hosts. Do not expose this host publicly.
3. Fresh browser: visit `https://localhost:7150/_local/auth-context`, expect the
   anonymous JSON above and no-store, without Set-Cookie.
4. Same browser: sign in at `https://localhost/users/account/LogOn`; visit direct
   legacy `https://localhost/account` and read the existing signed-in header username.
5. Visit `https://localhost:7150/_local/auth-context`; compare `name` and
   `nameIdentifier` to that legacy username; isAuthenticated should be true.
6. Visit proxied `https://localhost:7150/account`, verify same signed-in legacy
   context. Sign out with the legacy UI and reload diagnostic: anonymous again.

The direct legacy `/account` comparison exercises its existing UIAuthorize
surface, not a public profile or a newly added diagnostic. Roles may vary.
Avoid recording/copying cookie/token values when inspecting headers.

## Exact files modified by this worker

- `src/NuGetGallery.net10/Controllers/AuthContextController.cs`
- `src/NuGetGallery.net10/Program.cs`
- `tests/NuGetGallery.net10.Facts/AuthContextFacts.cs`
- `README.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/10-auth-context-diagnostic/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/10-auth-context-diagnostic/breakdown-context.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/10-auth-context-diagnostic/progress-details.md`
