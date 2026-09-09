# 09-build-integration: Validate repository build boundaries and release readiness

Update only the build surfaces needed for the new SDK-style projects while preserving the Visual Studio/MSBuild path for the legacy solution. Validate every solution affected by the two foundations, explicitly including `NuGetGallery.sln`, `NuGet.Server.Common.sln`, `NuGet.Jobs.sln`, and AccountDeleter's containing build surface. Do not add the new host to unrelated solutions or alter frameworks/packages in Gallery Core, Gallery Services, jobs, validation, or functional-test projects except for narrowly required compatible cookie and foundation dependencies.

Perform the final dependency-order validation: foundations remain independently cherry-pickable and behavior-preserving; the new host starts without Aspire; local endpoints beat the proxy; unmatched requests reach the configured legacy origin; bidirectional shared cookies preserve relevant identity; renewal is legacy-owned; and invalid cookies do not block anonymous fallback. Document as post-upgrade work—rather than execute—production resource provisioning, topology/DNS/cutover, controller migration, EF Core, and eventual legacy-project removal.

**Done when**: All affected solutions restore and build through their established toolchains without new warnings, all targeted and applicable repository tests pass, no EF Core/schema/Aspire/deployment changes are present, and the side-by-side host satisfies the approved completion criteria.

## Researched build surfaces and dependency boundaries

- Full Visual Studio MSBuild is required for the established solution boundary because
  `NuGetGallery.sln`, `NuGet.Server.Common.sln`, and `NuGet.Jobs.sln` contain legacy
  .NET Framework projects. The installed tool is
  `C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe`.
- `NuGetGallery.sln` is the only solution that contains the new
  `src\NuGetGallery.net10\NuGetGallery.net10.csproj` and
  `tests\NuGetGallery.net10.Facts\NuGetGallery.net10.Facts.csproj`. Neither project is
  present in `NuGet.Server.Common.sln` or `NuGet.Jobs.sln`.
- `NuGetGallery.sln` is also AccountDeleter's containing build surface. It contains both
  `src\AccountDeleter\AccountDeleter.csproj` and
  `tests\AccountDeleter.Facts\AccountDeleter.Facts.csproj`. AccountDeleter remains an
  SDK-style `net472` executable and references `NuGetGallery.Services` and
  `Validation.Common.Job`; no conversion or target-framework change is needed.
- The new Web SDK host targets only `net10.0`, references
  `NuGet.Services.Configuration` and `NuGetGallery.Core`, and adds only the YARP package.
  Its SDK-style facts project targets only `net10.0` and references the host.
- The established legacy solution builds are:
  - `MSBuild.exe NuGetGallery.sln /restore /t:Build /p:Configuration=Debug /v:minimal`
  - `MSBuild.exe NuGet.Server.Common.sln /restore /t:Build /p:Configuration=Debug /v:minimal`
  - map the repository root to a temporary drive, then run
    `MSBuild.exe <drive>:\NuGet.Jobs.sln /restore /t:Build /p:Configuration=Debug /v:minimal`
    to avoid the known `MAX_PATH` boundary in
    `Validation.PackageSigning.RevalidateCertificate.Tests`; remove the mapping afterward.
- The modern-host release boundary is:
  - `dotnet build src\NuGetGallery.net10\NuGetGallery.net10.csproj -c Debug`
  - `dotnet build src\NuGetGallery.net10\NuGetGallery.net10.csproj -c Release`
  - `dotnet publish src\NuGetGallery.net10\NuGetGallery.net10.csproj -c Release`
  - start the published output without Aspire and probe `/_health`, `/_ready`, and
    `/_local`.
- Targeted/applicable test boundaries are the full
  `NuGetGallery.net10.Facts`, `NuGetGallery.Core.Facts`,
  `NuGet.Services.KeyVault.Tests`, `NuGetGallery.Facts`, and `AccountDeleter.Facts`
  projects plus `tests\NuGetGallery.CookieInteropTests\run-tests.ps1`. These cover the
  host/proxy, shared Data Protection storage, Key Vault integration, legacy cookie and
  storage consumers, AccountDeleter's dependency closure, and bidirectional cookie
  compatibility.

## Pre-edit findings and audit plan

- The storage foundation is commit `838a0bf2b`; the Key Vault foundation is commit
  `4cae9f415`. Both precede all migration-specific commits. Their changed-file lists
  contain no `NuGetGallery.net10` host, proxy, cookie-cutover, Aspire, EF Core, schema,
  or deployment files. Final validation will additionally apply each commit to a
  detached `origin/dev` worktree and build/test its affected boundary, proving practical
  cherry-pick independence rather than relying only on history inspection.
- The current branch adds the new host only to `NuGetGallery.sln`. The final diff audit
  will reject host entries in unrelated solutions and changes involving EF Core,
  migrations/schema, Aspire, or deployment infrastructure.
- Existing regression suites already assert local-route precedence over the catch-all,
  unmatched fallback, shared-cookie claims in both directions, legacy-only renewal,
  and anonymous fallback for invalid cookies. They will be rerun in the final boundary
  rather than duplicated.
- Build/project configuration will be changed only when an actual reproducible failure
  is caused by this migration. Existing warnings will be recorded separately; warnings
  in files/projects changed by this task are release-blocking and will not be suppressed.
- Post-upgrade only: provision production storage/Key Vault resources and identities;
  finalize App Service topology, trusted proxy/public-host allow-lists, DNS, slot/cutover
  and rollback procedures; migrate controllers incrementally; plan EF Core/schema work
  independently; and remove the legacy application only after traffic, authentication,
  and operational parity are proven.

## Release-blocker resolution

The feature history was safely reconstructed after final validation because the original
Key Vault commit modified workflow paths introduced by the storage commit and therefore
could not be cherry-picked directly onto `origin/dev`. The rewrite removed only
`.github/upgrades` from all eight product commits, preserved their order, full commit
messages/trailers, identities, timestamps, and every product-code tree, and left the
complete upgrade workflow state in the working tree for the final Task 09 commit.

The foundation mapping is `838a0bf2b5b522371f854aed9d57e3651c55f62a` to
`45cccf972df1b11aa4288bd3851755cbd8749250` for storage and
`4cae9f415873e192862da95f30fce36576bc9edc` to
`83b4c4db3937a2c609e58b54e8e94710266367aa` for Key Vault. Direct, independent
cherry-picks of each rewritten commit onto detached `origin/dev` worktrees succeeded.
The storage worktree passed all three affected solutions plus Core, Gallery, and
AccountDeleter facts. The Key Vault worktree passed its multi-target project build,
`NuGet.Server.Common.sln`, and all 93 Key Vault tests. The rewritten branch also passed
`NuGetGallery.sln`; detailed evidence and the complete eight-commit mapping are in
`progress-details.md`.

## Final validation disposition

The product branch is release ready: all required current-tree and independent
foundation validation passed, the rewritten foundation commits cherry-pick cleanly,
and the repository contains no validation helper or worktree paths. Task 09 validation
is complete, but the workflow task remains awaiting orchestrator completion because
this execution must not call workflow state tools or create the final workflow commit.

The restore warning `NU1902` is pre-existing and was not suppressed. It reports the
transitive package `Microsoft.Build.Tasks.Git` 8.0.0 at moderate severity for
`GHSA-23fw-v26w-5fgq`. The dependency comes from the centrally pinned
`Microsoft.SourceLink.GitHub` 8.0.0, which is unchanged from `origin/dev`; NuGet
consulted the configured `dotnet-tools`, `NuGet.org`, and `nuget-build` sources, and
the advisory record is published at `https://github.com/advisories/GHSA-23fw-v26w-5fgq`.
