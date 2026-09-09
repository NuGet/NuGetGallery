# 09-build-integration progress

## STATUS

VALIDATION COMPLETE — RELEASE READY; WORKFLOW COMPLETION PENDING ORCHESTRATOR

All current-tree build, publish, startup, solution-boundary, and regression gates
passed. No source or build configuration change was needed. The foundation history was
rewritten without changing any product tree so both product-only foundation commits now
cherry-pick directly and independently onto unmodified `origin/dev`. Workflow state was
removed from all eight product commits and remains intact in the working tree for the
later Task 09 workflow commit. The product branch is release ready; only the workflow
task transition/final workflow commit remains for the orchestrator, because this task
was explicitly prohibited from calling workflow tools or committing.

## Build-surface results

Visual Studio MSBuild 18 was used for established legacy solution boundaries:

- `& 'C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe' '.\NuGetGallery.sln' /restore /t:Build /p:Configuration=Debug /v:minimal /nologo`
  - Passed with 0 errors.
  - Built the legacy Gallery, AccountDeleter, `AccountDeleter.Facts`, the new host,
    and the new host facts through their containing solution.
- `& 'C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe' '.\NuGet.Server.Common.sln' /restore /t:Build /p:Configuration=Debug /v:minimal /nologo`
  - Passed with 0 errors.
- `subst N: <repository-root>` followed by
  `& 'C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe' 'N:\NuGet.Jobs.sln' /restore /t:Build /p:Configuration=Debug /v:minimal /nologo`,
  then `subst N: /d`
  - Passed with 0 errors.
  - The short drive avoided the known validation-test `MAX_PATH` boundary.
  - The mapping was removed and verified absent.

No solution or project file needed modification. Existing warning signatures were
limited to repository-wide `NU1507`, pre-existing `NU1902`, the centrally pinned
analyzer package being older than the .NET 10 SDK analyzers, existing obsolete
Application Insights APIs, and existing analyzer/compiler warnings in unrelated
projects. `NU1902` identifies transitive `Microsoft.Build.Tasks.Git` 8.0.0 as having
the moderate-severity advisory
`https://github.com/advisories/GHSA-23fw-v26w-5fgq`. `dotnet nuget why` traced it to
the centrally pinned `Microsoft.SourceLink.GitHub` 8.0.0; that direct package/version
already exists unchanged on `origin/dev`. NuGet reported the repository's configured
`dotnet-tools`, `NuGet.org` (`https://api.nuget.org/v3/index.json`), and
`nuget-build` sources while resolving vulnerability data. This is an existing
dependency/advisory warning, not a new host package or Task 09 regression. It was not
suppressed. Task 09 changed no compiled project and introduced no warning.

## Host Debug, Release, publish, and startup

- `dotnet build src\NuGetGallery.net10\NuGetGallery.net10.csproj --configuration Debug --nologo --verbosity minimal`
  - Passed; output exists at
    `src\NuGetGallery.net10\bin\Debug\net10.0\NuGetGallery.net10.dll`.
- `dotnet build src\NuGetGallery.net10\NuGetGallery.net10.csproj --configuration Release --nologo --verbosity minimal`
  - Passed; 0 errors and 6 pre-existing analyzer-version warnings from the host and
    dependency projects.
- `dotnet publish src\NuGetGallery.net10\NuGetGallery.net10.csproj --configuration Release --output artifacts\09-build-integration-publish --nologo --verbosity minimal`
  - Passed.
  - Published DLL and `web.config` existed.
  - Published IIS settings retained `hostingModel="inprocess"` and
    `maxAllowedContentLength="4294967295"`.
- From the publish directory, with
  `ASPNETCORE_ENVIRONMENT=Development` and
  `ASPNETCORE_URLS=http://127.0.0.1:5199`:
  `dotnet .\NuGetGallery.net10.dll`
  - Started successfully without Aspire.
  - `GET /_health` returned 200 `Healthy`.
  - `GET /_ready` returned 200 `Healthy`.
  - `GET /_local` returned 200 `NuGetGallery.net10`.
  - The process was stopped and publish output removed.

An initial diagnostic launch invoked the published DLL while the process working
directory was the repository root. It correctly failed because the required
`appsettings.json` content root was wrong. Starting from the deployed publish directory
passed; production hosting must likewise preserve the deployed content root, as IIS
normally does.

## Test results

- `dotnet test tests\NuGetGallery.net10.Facts\NuGetGallery.net10.Facts.csproj --configuration Debug --no-restore --no-build --nologo --verbosity minimal`
  - 48 passed; 0 failed; 0 skipped.
- The same command with `--configuration Release`
  - 48 passed; 0 failed; 0 skipped.
- `dotnet test tests\NuGetGallery.Core.Facts\NuGetGallery.Core.Facts.csproj --configuration Debug --no-restore --no-build --nologo --verbosity minimal`
  - 1,194 passed; 0 failed; 19 skipped integration tests.
- `dotnet test tests\NuGet.Services.KeyVault.Tests\NuGet.Services.KeyVault.Tests.csproj --configuration Debug --no-restore --no-build --nologo --verbosity minimal`
  - 100 passed; 0 failed; 0 skipped.
- `dotnet test tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj --configuration Debug --no-restore --no-build --nologo --verbosity minimal`
  - 11,085 passed; 0 failed; 2 skipped existing timing tests.
- `dotnet test tests\AccountDeleter.Facts\AccountDeleter.Facts.csproj --configuration Debug --no-restore --no-build --nologo --verbosity minimal`
  - 24 passed; 0 failed; 0 skipped.
- `.\tests\NuGetGallery.CookieInteropTests\run-tests.ps1`
  - Passed `legacy-issue`, `core-exchange`, and `legacy-accept` on `net472` and
    `net10.0`, including invalid-format rejection, legacy-to-Core acceptance,
    Core-to-legacy acceptance, claims, expiration, and chunked cookies.

The passing host facts include the approved local-route-before-proxy ordering,
unmatched legacy fallback, proxy transport behavior, shared cookie authorization,
legacy-only renewal, and invalid-cookie anonymous fallback.

### Final independent validation counts

- Current rewritten branch: 3 of 3 required Visual Studio solution builds passed;
  host Debug, Release, and publish passed; 3 of 3 no-Aspire startup probes returned
  HTTP 200.
- Current rewritten branch tests: 48 of 48 host facts passed in Debug and 48 of 48
  in Release; 1,194 Core facts passed with 19 skipped; 100 Key Vault tests passed;
  11,085 Gallery facts passed with 2 skipped; 24 AccountDeleter facts passed; all
  3 cookie interoperability stages passed.
- Independently cherry-picked storage foundation: 3 of 3 affected solution builds
  passed; 1,152 Core facts passed with 19 skipped, 11,076 Gallery facts passed with
  2 skipped, and 24 AccountDeleter facts passed.
- Independently cherry-picked Key Vault foundation: both target frameworks built,
  `NuGet.Server.Common.sln` passed, and 93 of 93 Key Vault tests passed.

## Dependency, solution, and diff audit

- Rewritten commit order remains foundation-first:
  `45cccf972` storage, `83b4c4db3` Key Vault, `2df5f1835` cookie proof,
  `73073345b` shared Data Protection, `ed575acb6` host,
  `264057f84` proxy, `68ce60f93` legacy cutover, and `3a7757b96`
  regression completion.
- `git show --name-only` found no workflow-state, `NuGetGallery.net10`, proxy,
  shared-cookie, Aspire, migration/schema, EF Core, or deployment path in either
  rewritten foundation commit.
- Storage foundation practical independence:
  - `git worktree add --detach .foundation-validation\storage origin/dev`
  - `git cherry-pick 45cccf972df1b11aa4288bd3851755cbd8749250`
  - The direct cherry-pick completed without conflict.
  - Through temporary `N:`, Visual Studio MSBuild 18 passed `NuGetGallery.sln`,
    `NuGet.Server.Common.sln`, and `NuGet.Jobs.sln`.
  - 1,152 Core facts passed with 19 skipped integration tests; 11,076 Gallery facts
    passed with 2 existing skipped timing tests; 24 AccountDeleter facts passed.
- Key Vault foundation practical independence:
  - `git worktree add --detach .foundation-validation\keyvault origin/dev`
  - `git cherry-pick 83b4c4db3937a2c609e58b54e8e94710266367aa`
  - The direct cherry-pick completed without conflict.
  - The Key Vault project Release build passed for `net472` and `netstandard2.0`;
    all 93 Key Vault tests passed.
  - `NuGet.Server.Common.sln` first reproduced the known nested-worktree
    `PathTooLongException`; its retry through temporary `N:` passed.
- The new host and facts occur only in `NuGetGallery.sln`. They do not occur in
  `NuGet.Server.Common.sln`, `NuGet.Jobs.sln`, or the Aspire solution.
- The `origin/dev...HEAD` path audit found no EF Core, schema/migration, Aspire,
  infrastructure-as-code, or deployment changes. No Azure resource was contacted.
- `git diff --check` passed.
- Both detached validation worktrees, all mappings, and generated publish output were
  removed. Unrelated workflow/assessment artifacts were preserved.

## Release blocker resolution and remaining risks

The dedicated feature branch was reconstructed non-interactively with `git commit-tree`
from the original eight commits. Each reconstructed tree removes only
`.github/upgrades`; parent order, subjects, complete messages/trailers, authors,
committers, and timestamps were retained. Per-commit diffs outside `.github/upgrades`
were empty. The old-to-new mapping is:

| Original | Rewritten | Task |
| --- | --- | --- |
| `838a0bf2b5b522371f854aed9d57e3651c55f62a` | `45cccf972df1b11aa4288bd3851755cbd8749250` | Storage foundation |
| `4cae9f415873e192862da95f30fce36576bc9edc` | `83b4c4db3937a2c609e58b54e8e94710266367aa` | Key Vault foundation |
| `f226de756884fbac14609c6582c93ebb4ff769f6` | `2df5f1835829c05c731257e226b941e84126d0da` | Cookie interoperability |
| `e97dc5a6011fcb84ed4b1176f53529b1df5ff435` | `73073345b555091b8231543f72f8eca9aff4cdf5` | Shared Data Protection |
| `eae63f8009858d7ccbabc4b41fdfea3ad0fbde79` | `ed575acb6b4bd78d753e9b543125ac099b7258c8` | .NET 10 host |
| `a400f9b8e8f3548540aa7b50db0d9a2cee10419b` | `264057f8493bc881d77ec66d893159e6a3eaa859` | YARP fallback |
| `57e181a21d43d13351d20770e3f3b0a69aaa3acd` | `68ce60f93335f0ac35dcbfefa3e5532664ae1e3d` | Legacy cookie cutover |
| `3f7ecb7ecb8a634cb9cb568e774c4fef6b85f905` | `3a7757b96006d3a0b64e556892d8d9969e0824f9` | Cross-host regressions |

Safety and final verification:

- `task09-history-backup-20260909` preserves the original head.
- The branch ref and index were updated with `git update-ref` and `git read-tree`;
  no reset, amend, squash, or branch rename was used.
- The complete workflow directory was hash-snapshotted before the rewrite and left
  in the working tree after the rewrite. It includes Task 09 and assessment artifacts,
  so upgrade state was not lost.
- The rewritten current branch passed `NuGetGallery.sln` through Visual Studio
  MSBuild 18 on temporary `N:`. Its product tree is byte-for-byte equivalent to the
  original head outside `.github/upgrades`.
- Direct `git cherry-pick` of
  `45cccf972df1b11aa4288bd3851755cbd8749250` and
  `83b4c4db3937a2c609e58b54e8e94710266367aa` in separate detached
  `origin/dev` worktrees completed without conflict. This proves the rewritten commit
  objects—not only their source diffs—are independently consumable.

Remaining risks:

1. Existing repository warnings remain technical debt. No Task 09 warning was added
   or suppressed.
2. Production configuration and infrastructure were intentionally not exercised.
   Deployment must set the deployed content root and validate real trusted
   proxy/public-origin allow-lists, secrets, identities, storage, and Key Vault access.
3. The accepted cookie-format cutover can require a one-time sign-in on slot swap or
   rollback.

## Cleanup and final repository status

- Removed the validation-created root helpers
  `run_git_commands.ps1`, `run_git_commands_temp.sh`, and
  `temp_git_diagnostics.ps1`.
- `.task09-validation-keyvault` was not a registered Git worktree and no process held
  either remaining file according to Windows Restart Manager. The persistent MSBuild
  servers were shut down cleanly before retrying deletion.
- Two validation copies denied ACL/owner reads and deletion under the current
  non-elevated token:
  - `C:\Users\angrigor\source\repos\copilot-worktrees\nugetgallery-core2\agr-scaling-carnival\.task09-validation-keyvault\tests\NuGetGallery.Facts\Framework\TestAuditingService.cs`
  - `C:\Users\angrigor\source\repos\copilot-worktrees\nugetgallery-core2\agr-scaling-carnival\.task09-validation-keyvault\tests\NuGetGallery.Facts\OData\SearchService\SearchHijackerFacts.cs`
  Both had only the `Archive` attribute; neither was read-only; their owner was
  unreadable (`Get-Acl`, `icacls`, and `takeown` returned access denied), and no
  locking process was reported. To avoid unsafe elevation or broad deletion, the exact
  temporary root was atomically moved out of the repository to the session cleanup
  quarantine at
  `C:\Users\angrigor\.copilot\session-state\880ad3d7-52a7-4bf5-b918-843585f75b20\files\cleanup-quarantine-task09-validation-keyvault`.
  The repository path no longer exists. The quarantined copies require an elevated
  cleanup outside this repository; no workflow artifact or source path was changed.
- Final `git status --short` contains only `?? .github/upgrades/`, the intentionally
  preserved workflow/assessment/task artifacts awaiting orchestrator completion.

## Post-upgrade work (not executed)

- Provision production storage, Key Vault keys/secrets, access policies/RBAC, managed
  identities, certificates, and configuration through existing Gallery practices.
- Finalize App Service topology, trusted proxy/public host configuration, health
  probes, DNS, slot swap, traffic ramp, telemetry, rollback, and cutover runbooks.
- Migrate controllers/routes incrementally after parity is measured.
- Plan EF Core and any schema work as a separate migration.
- Remove the legacy project only after all traffic, authentication renewal, operational
  behavior, and rollback requirements are retired.

## Exact files modified by Task 09

- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/09-build-integration/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/09-build-integration/progress-details.md`

No commit was created.
