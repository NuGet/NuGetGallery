## Implementation

- Added `DataProtection__StorageLocation=../NuGetGallery/App_Data/Files` to the .NET 10 Development `Project` launch profile. Preserved its existing localhost certificate-selection settings and left production/appsettings configuration unchanged.
- Added a profile regression test that loads the checked-in launch profile from test output, verifies the standard `Project` profile has no working-directory override, and resolves its relative base from the host project directory to the exact legacy `Gallery.FileStorageDirectory` base (not the `data-protection` child).
- Strengthened the production `LocalUserAuthenticator` test: its `AspNetTicketDataFormat` cookie is now unprotected by a separately created Data Protection provider sharing only the isolated test key-ring path and application name. This avoids a same-provider-only round trip.
- Updated the root README with stop/restart order, both launch methods, relative-path semantics, and manual sign-in verification instructions. No local host was started and no sign-in was performed.

## Validation

- `dotnet build src\NuGetGallery.net10\NuGetGallery.net10.csproj --no-restore` — succeeded, 0 errors. Existing build warnings: NU1902 for `Microsoft.Build.Tasks.Git` 8.0.0 and the SDK/`Microsoft.CodeAnalysis.NetAnalyzers` version advisory.
- `dotnet test tests\NuGetGallery.net10.Facts\NuGetGallery.net10.Facts.csproj --no-restore` — passed, 57 passed, 0 failed, 0 skipped. Includes the new profile test. Existing NU1902 and analyzer-version warnings remain.
- `MSBuild.exe tests\NuGetGallery.Facts\NuGetGallery.Facts.csproj /t:Build /p:Configuration=Debug /p:Restore=false /v:minimal` using Visual Studio 18 — succeeded. `vstest.console.exe tests\NuGetGallery.Facts\bin\Debug\net472\NuGetGallery.Facts.dll /TestCaseFilter:FullyQualifiedName~LocalUserAuthenticatorFacts` — passed, 4 passed, 0 failed.
- The legacy test build also reported existing analyzer-version warnings, two CS0618 Application Insights instrumentation-key warnings, and six CS8625 warnings in the unrelated `FederatedCredentialServiceFacts.cs`; none are in the changed test file. The net10 host/test warnings likewise originate from existing dependency/security-audit and analyzer-version findings, not the new code; no unrelated package changes or warning suppressions were made.
- `git diff --check` — passed. The copied launch profile is present in the test output. The existing local `.data` and legacy key-ring file counts remained one each; their contents were not opened or modified.

## Notes

- The first launch-profile test run exposed that the host assembly is copied to the test output directory; the helper was corrected to locate the repository host project from the test assembly's ancestor directories. The complete suite passed after the correction.
- A direct `dotnet test` attempt for `NuGetGallery.Facts` failed because the dotnet SDK MSBuild lacked `Microsoft.WebApplication.targets`. Building through Visual Studio MSBuild and running the targeted test assembly with the Visual Studio test runner succeeded.
- `task.md` research enrichment was completed before source changes. Decomposition verdict: atomic; evaluated scenario `execution.md` and `breakdown-hints\common.md` plus `breakdown-hints\test.md`.
- `launchSettings.json` contained user-local TLS settings before this task. They were retained without change; the file's pre-existing working-tree modification is not attributable to this task. The pre-existing untracked `.data` key ring was also left untouched.

## Files changed for this task

- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/11-local-shared-key-ring/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/11-local-shared-key-ring/progress-details.md`
- `README.md`
- `src/NuGetGallery.net10/Properties/launchSettings.json`
- `tests/NuGetGallery.Facts/Authentication/Providers/LocalUser/LocalUserAuthenticatorFacts.cs`
- `tests/NuGetGallery.net10.Facts/NuGetGallery.net10.Facts.csproj`
- `tests/NuGetGallery.net10.Facts/LaunchSettingsFacts.cs`
